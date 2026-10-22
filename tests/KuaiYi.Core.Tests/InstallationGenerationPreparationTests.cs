using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class InstallationGenerationPreparationTests
{
    private sealed class Slots:IInstallationDeploymentSnapshotRead
    {
        public bool CleanupConfirmed=>true;
        public Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync()=>Task.FromResult(new InstallationDeploymentRegistrationSnapshot(new(false,null),new(false,null)));
        public Task VerifyAsync()=>Task.CompletedTask;
        public Task CloseOriginalsAsync()=>Task.CompletedTask;
    }
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-generation-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"app");internal string Data=>Path.Combine(Root,"data");internal string Source=>Path.Combine(Root,"source");
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal Rig(){using var initial=Store.EnterRoleAdmission();Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_OFF"),"PUBLIC_PRESERVED");}
        internal string GenericSource()
        {
            Directory.CreateDirectory(Source);File.WriteAllText(Path.Combine(Source,"PUBLIC.bin"),"PUBLIC_NEUTRAL");
            var bytes=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version="1.0",files=new[]{new{path="PUBLIC.bin",bytes=new FileInfo(Path.Combine(Source,"PUBLIC.bin")).Length,sha256=MaterializedPayloadSource.Hash(Path.Combine(Source,"PUBLIC.bin"))}}});File.WriteAllBytes(Path.Combine(Source,"package.json"),bytes);return Convert.ToHexString(SHA256.HashData(bytes));
        }
        internal MaterializedPayloadSource FullSource()=>new(Source);
        internal string Intent(InstallationDeploymentScope scope)=>Path.Combine(App,"deployment-generation-"+scope.Operation+".json");
        internal string Staging(InstallationDeploymentScope scope)=>Path.Combine(App,"staging-"+scope.Generation);
        internal string Destination(InstallationDeploymentScope scope)=>Path.Combine(App,"versions",scope.Generation);
        internal void NoPublication(){foreach(string path in new[]{Path.Combine(App,"current.json"),InstallationBootstrapDeployment.OwnerPath(App),InstallationBootstrapDeployment.ActivePath(App),Path.Combine(App,"host-registration.json")})Assert.False(File.Exists(path));}
        internal async Task<InstallationDeploymentScope> Begin(string sha,CancellationToken cancellation=default,InstallationDeploymentFaults? faults=null)
        {var scope=new InstallationDeploymentScope(App,Data,sha,new string('B',64),cancellation,faults:faults);await scope.AcquireAsync();await scope.CaptureBeforeAsync(new Slots());return scope;}
        public void Dispose(){Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(Data,"PUBLIC_OFF")));Directory.Delete(Root,true);}
    }
    private static IReadOnlyList<Exception> Errors(Exception error)=>error is AggregateException a?a.Flatten().InnerExceptions:[error];
    [Fact]public async Task OriginalSourceAcquireAndCloseFailuresCannotArchiveWhileTheSameHandleIsUnsettled()
    {
        using var r=new Rig();var scope=await r.Begin(r.GenericSource());var primary=new IOException("PUBLIC_SOURCE_ACQUIRE");var close=new IOException("PUBLIC_SOURCE_CLOSE");FileStream? original=null;
        var faults=new InstallationPreparationFaults
        {
            Acquired=(kind,path,value)=>{if(kind=="read"&&path==Path.Combine(r.Source,"package.json")){original=Assert.IsType<FileStream>(value);throw primary;}},
            BeforeClose=(kind,path)=>{if(kind=="read"&&path==Path.Combine(r.Source,"package.json"))throw close;}
        };
        try
        {
            var error=await Record.ExceptionAsync(()=>scope.PrepareGenerationAsync(r.Source,faults));Assert.NotNull(error);Assert.Contains(primary,Errors(error));Assert.Contains(close,Errors(error));
            var result=await scope.Finalization;Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.False(scope.CleanupConfirmed);Assert.False(original!.SafeFileHandle.IsClosed);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
        }
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());original?.Dispose();}
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancellationAfterLastValidationOrAfterDeliveryPermanentlyRetiresCredential(bool beforeDelivery)
    {
        using var r=new Rig();var source=r.FullSource();using var stop=new CancellationTokenSource();var scope=await r.Begin(source.Digest,stop.Token);
        if(beforeDelivery)
        {
            var error=await Record.ExceptionAsync(()=>scope.PrepareGenerationAsync(r.Source,new(){Stage=stage=>{if(stage==GenerationPreparationStage.BeforeCredential)stop.Cancel();}}));
            if(error==null)await scope.AbortBeforePreparedAsync(); // Own and settle the red candidate before assertion.
            Assert.NotNull(error);Assert.Contains(Errors(error),x=>x is OperationCanceledException);
        }
        else
        {
            var candidate=await scope.PrepareGenerationAsync(r.Source);Assert.True(candidate.IsLiveFor(scope));stop.Cancel();bool live=candidate.IsLiveFor(scope);await scope.AbortBeforePreparedAsync();Assert.False(live);Assert.False(candidate.IsLiveFor(scope));
        }
        Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Fact]public async Task RealSourceFactoryProducesOnlyScopeBoundInactiveV3AndEveryNestedReadUsesOriginalLedger()
    {
        using var r=new Rig();var source=r.FullSource();string sourceSha=source.Digest;var scope=await r.Begin(sourceSha);var acquired=new List<(string Kind,string Path,IDisposable Original)>();int closes=0;
        var candidate=await scope.PrepareGenerationAsync(r.Source,new(){Acquired=(kind,path,original)=>acquired.Add((kind,path,original)),BeforeClose=(_,_)=>closes++});
        Assert.True(candidate.IsLiveFor(scope));Assert.Equal(r.Destination(scope),candidate.Version.DirectoryPath);Assert.Equal(sourceSha,candidate.SourceCatalogSha256);Assert.NotEqual(sourceSha,candidate.Version.CatalogSha256);Assert.Equal(sourceSha,source.Digest);
        var installed=InstallationPayload.RequireMaterialized(candidate.Version.DirectoryPath);Assert.Equal(3,installed.Schema);Assert.Equal(sourceSha,installed.SourceCatalogSha256);Assert.Equal(candidate.Version.CatalogSha256,installed.CatalogSha256);
        using(var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(candidate.Version.DirectoryPath,InstallationPayload.HostManifestName))))Assert.Equal(Path.Combine(candidate.Version.DirectoryPath,"KuaiYi.NativeHost.exe"),manifest.RootElement.GetProperty("path").GetString());
        foreach(string kind in new[]{"read","verify","hash","enumerator","pe-input","pe-reader","copy-input","copy-output","metadata-output"})Assert.Contains(acquired,x=>x.Kind==kind);
        Assert.Contains(acquired,x=>x.Kind=="read"&&x.Path==Path.Combine(r.Source,"identity/AppxManifest.xml"));Assert.Contains(acquired,x=>x.Kind=="read"&&x.Path==Path.Combine(r.Source,"browser-extension/manifest.json"));
        Assert.Equal(acquired.Count,closes);Assert.All(acquired.Where(x=>x.Original is FileStream),x=>Assert.True(((FileStream)x.Original).SafeFileHandle.IsClosed));r.NoPublication();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.PrepareGenerationAsync(r.Source));
        var result=await scope.AbortBeforePreparedAsync();Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,result.Status);Assert.Empty(result.Errors);Assert.False(candidate.IsLiveFor(scope));Assert.True(scope.CleanupConfirmed);r.NoPublication();
        using var terminal=JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-failed-*.json"))));var retained=terminal.RootElement.GetProperty("inactiveGeneration");Assert.Equal("VerifiedInactive",retained.GetProperty("State").GetString());Assert.Equal(candidate.Version.CatalogSha256,retained.GetProperty("InstalledCatalogSha256").GetString());Assert.Equal(JsonValueKind.Null,terminal.RootElement.GetProperty("preparedSha256").ValueKind);Assert.True(Directory.Exists(candidate.Version.DirectoryPath));
        int beforeSettle=closes;Assert.Empty(await scope.SettleOriginalsAsync());Assert.Equal(beforeSettle,closes);
    }

    [Theory][InlineData("generic")][InlineData("pin")][InlineData("permission")]
    public async Task QualificationRejectsBeforeAnyCandidateOwnershipOrPublication(string fault)
    {
        using var r=new Rig();string digest;
        if(fault=="generic")digest=r.GenericSource();
        else
        {
            var source=r.FullSource();
            if(fault=="permission"){var manifest=JsonNode.Parse(File.ReadAllBytes(Path.Combine(r.Source,"browser-extension/manifest.json")))!;manifest["optional_permissions"]=new JsonArray("tabs");source.Put("browser-extension/manifest.json",JsonSerializer.SerializeToUtf8Bytes(manifest));source.Catalog();}
            digest=fault=="pin"?new string('0',64):source.Digest;
        }
        var scope=await r.Begin(digest);await Assert.ThrowsAsync<InvalidDataException>(()=>scope.PrepareGenerationAsync(r.Source));Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,(await scope.Finalization).Status);Assert.True(scope.CleanupConfirmed);Assert.False(File.Exists(r.Intent(scope)));Assert.False(Directory.Exists(r.Staging(scope)));Assert.False(Directory.Exists(r.Destination(scope)));r.NoPublication();
    }

    [Theory][InlineData("intent","write")][InlineData("source","write")][InlineData("derived","write")][InlineData("catalog","write")][InlineData("intent","flush")][InlineData("derived","close")]
    public async Task AnyAttemptedMetadataPartialFlushOrCloseStaysUnconfirmedAndRetained(string phase,string failure)
    {
        using var r=new Rig();var source=r.FullSource();var scope=await r.Begin(source.Digest);var originalError=new IOException("PUBLIC_METADATA_"+failure);FileStream? held=null;
        bool Match(string path)=>phase switch{"intent"=>path==r.Intent(scope),"source"=>path==Path.Combine(r.Staging(scope),InstallationPayload.SourceCatalogName),"derived"=>path==Path.Combine(r.Staging(scope),InstallationPayload.ProvenanceName),_=>path==Path.Combine(r.Staging(scope),InstallationPackage.CatalogName)};
        var faults=new InstallationPreparationFaults
        {
            Acquired=(kind,path,value)=>{if(kind=="metadata-output"&&Match(path))held=Assert.IsType<FileStream>(value);},
            WriteMetadata=(path,stream,bytes)=>{if(failure=="write"&&Match(path)){stream.Write(bytes.Span[..Math.Min(9,bytes.Length)]);throw originalError;}stream.Write(bytes.Span);},
            BeforeFlush=(path,_)=>{if(failure=="flush"&&Match(path))throw originalError;},
            BeforeClose=(kind,path)=>{if(failure=="close"&&kind=="metadata-output"&&Match(path))throw originalError;}
        };
        try
        {
            var error=await Record.ExceptionAsync(()=>scope.PrepareGenerationAsync(r.Source,faults));Assert.NotNull(error);Assert.Contains(originalError,Errors(error));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));Assert.NotNull(held);Assert.Equal(failure!="close",held.SafeFileHandle.IsClosed);Assert.Equal(failure!="close",scope.CleanupConfirmed);r.NoPublication();
            string target=phase switch{"intent"=>r.Intent(scope),"source"=>Path.Combine(r.Staging(scope),InstallationPayload.SourceCatalogName),"derived"=>Path.Combine(r.Staging(scope),InstallationPayload.ProvenanceName),_=>Path.Combine(r.Staging(scope),InstallationPackage.CatalogName)};Assert.True(File.Exists(target));if(failure=="write")Assert.Equal(9,new FileInfo(target).Length);
        }
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(held!.SafeFileHandle.IsClosed);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task OwnedPayloadPartialMayAbortOnlyAfterOriginalIOActuallySettles(bool flush)
    {
        using var r=new Rig();var source=r.FullSource();var scope=await r.Begin(source.Digest);var failure=new IOException("PUBLIC_PAYLOAD");string? partial=null;FileStream? original=null;
        var error=await Record.ExceptionAsync(()=>scope.PrepareGenerationAsync(r.Source,new()
        {
            WritePayload=(path,stream,bytes)=>{partial=path;original=stream;if(!flush){stream.Write(bytes.Span[..7]);throw failure;}stream.Write(bytes.Span);},
            BeforeFlush=(path,_)=>{if(flush&&path==partial)throw failure;}
        }));Assert.NotNull(error);Assert.Contains(failure,Errors(error));Assert.True(original!.SafeFileHandle.IsClosed);Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,(await scope.Finalization).Status);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(partial));if(!flush)Assert.Equal(7,new FileInfo(partial!).Length);Assert.True(File.Exists(r.Intent(scope)));Assert.True(Directory.Exists(r.Staging(scope)));Assert.False(Directory.Exists(r.Destination(scope)));r.NoPublication();
        using var terminal=JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-failed-*.json"))));Assert.Equal("IncompleteInactive",terminal.RootElement.GetProperty("inactiveGeneration").GetProperty("State").GetString());
    }

    [Theory][InlineData("read")][InlineData("pe-reader")][InlineData("copy-output")]
    public async Task CancelAndAbortCannotAbandonAnOriginalReadPEOrCopyTask(string heldKind)
    {
        using var r=new Rig();var source=r.FullSource();using var stop=new CancellationTokenSource();using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var scope=await r.Begin(source.Digest,stop.Token);bool once=false;
        var prepare=scope.PrepareGenerationAsync(r.Source,new(){Acquired=(kind,_,_)=>{if(kind==heldKind&&!once){once=true;entered.SetResult();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("PUBLIC_TEST_BARRIER");}}});await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task<InstallationDeploymentOutcome>? final=null;
        try{stop.Cancel();final=scope.AbortBeforePreparedAsync();Assert.False(prepare.IsCompleted);Assert.False(final.IsCompleted);Assert.False(scope.CleanupConfirmed);await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.PrepareGenerationAsync(r.Source));r.NoPublication();}
        finally{release.Set();}
        var error=await Record.ExceptionAsync(async()=>await prepare);Assert.NotNull(error);Assert.Contains(Errors(error),x=>x is OperationCanceledException);Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,(await final!).Status);Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task DirectoryMoveUnknownNeverReplaysOrCreatesCredential(bool moved)
    {
        using var r=new Rig();var source=r.FullSource();var scope=await r.Begin(source.Digest);int moves=0;var failure=new IOException("PUBLIC_MOVE_UNCERTAIN");var error=await Record.ExceptionAsync(()=>scope.PrepareGenerationAsync(r.Source,new(){Move=(from,to)=>{moves++;if(moved)Directory.Move(from,to);throw failure;}}));
        Assert.NotNull(error);Assert.Contains(failure,Errors(error));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.Equal(moved,Directory.Exists(r.Destination(scope)));Assert.Equal(!moved,Directory.Exists(r.Staging(scope)));Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(await scope.SettleOriginalsAsync());Assert.Same(scope.Finalization,scope.AbortBeforePreparedAsync());Assert.Equal(1,moves);Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Fact]public async Task ClosingNewMetadataReadAndCloseFailuresBelongToOriginalFinalizationAndSameSettlement()
    {
        using var r=new Rig();var source=r.FullSource();bool closing=false;var scope=await r.Begin(source.Digest,faults:new(){AfterClosing=()=>{closing=true;return Task.CompletedTask;}});var read=new IOException("PUBLIC_FINAL_READ");var close=new IOException("PUBLIC_FINAL_CLOSE");FileStream? original=null;int opened=0;
        var faults=new InstallationPreparationFaults{Acquired=(kind,path,value)=>{if(closing&&kind=="read"&&path==r.Intent(scope)){opened++;original=Assert.IsType<FileStream>(value);throw read;}},BeforeClose=(kind,path)=>{if(closing&&kind=="read"&&path==r.Intent(scope))throw close;}};
        var candidate=await scope.PrepareGenerationAsync(r.Source,faults);
        try{var result=await scope.AbortBeforePreparedAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(read,result.Errors.SelectMany(Errors));Assert.Contains(close,result.Errors.SelectMany(Errors));Assert.False(original!.SafeFileHandle.IsClosed);Assert.False(scope.CleanupConfirmed);Assert.False(candidate.IsLiveFor(scope));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));}
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.Equal(1,opened);Assert.True(original!.SafeFileHandle.IsClosed);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));r.NoPublication();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ForeignOwnershipOrDerivedMetadataCannotBeArchivedAsKnownInactive(bool derived)
    {
        using var r=new Rig();var source=r.FullSource();var scope=await r.Begin(source.Digest);var candidate=await scope.PrepareGenerationAsync(r.Source);string path=derived?Path.Combine(candidate.Version.DirectoryPath,InstallationPayload.TrustName):r.Intent(scope);byte[] foreign="PUBLIC_FOREIGN"u8.ToArray();File.WriteAllBytes(path,foreign);
        Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.AbortBeforePreparedAsync()).Status);Assert.Equal(foreign,File.ReadAllBytes(path));Assert.True(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));r.NoPublication();
    }

    [Fact]public void UnsettledOriginalPreventsEveryNewDirectorySideEffect()
    {
        using var r=new Rig();r.GenericSource();string staging=Path.Combine(r.App,"PUBLIC_STAGE"),destination=Path.Combine(r.App,"versions/PUBLIC_GENERATION");var failure=new IOException("PUBLIC_HELD");var faults=new InstallationPreparationFaults{Acquired=(_,_,_)=>throw failure,BeforeClose=(_,_)=>throw failure};var io=new InstallationPreparationIO(r.Source,staging,destination,Path.Combine(r.App,"PUBLIC_INTENT"),default,faults);
        try
        {
            Assert.Throws<AggregateException>(()=>io.ReadDocument(Path.Combine(r.Source,"package.json"),1024*1024));Assert.False(io.CleanupConfirmed);
            Assert.Throws<InstallationRecoveryException>(()=>io.CreateStaging());Assert.Throws<InstallationRecoveryException>(()=>io.MoveToFinal());Assert.Throws<InstallationRecoveryException>(()=>io.CopyPayload(Path.Combine(r.Source,"PUBLIC.bin"),Path.Combine(staging,"nested/PUBLIC.bin"),new("nested/PUBLIC.bin",14,new string('A',64))));Assert.False(Directory.Exists(staging));Assert.False(Directory.Exists(Path.GetDirectoryName(destination)));
        }
        finally{faults.BeforeClose=null;io.CloseOriginals();}
        Assert.True(io.CleanupConfirmed);
    }

    [Theory][InlineData("pe-reader")][InlineData("enumerator")]
    public async Task NestedOriginalAcquisitionAndCloseErrorsRemainOwnedByTheSameScope(string heldKind)
    {
        using var r=new Rig();var source=r.FullSource();var scope=await r.Begin(source.Digest);var primary=new IOException("PUBLIC_NESTED_READ");var close=new IOException("PUBLIC_NESTED_CLOSE");int acquired=0;
        var faults=new InstallationPreparationFaults{Acquired=(kind,_,_)=>{if(kind==heldKind){acquired++;throw primary;}},BeforeClose=(kind,_)=>{if(kind==heldKind)throw close;}};
        try
        {
            var error=await Record.ExceptionAsync(()=>scope.PrepareGenerationAsync(r.Source,faults));Assert.NotNull(error);Assert.Contains(primary,Errors(error));Assert.Contains(close,Errors(error));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.False(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));Assert.False(File.Exists(r.Intent(scope)));r.NoPublication();
        }
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.Equal(1,acquired);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task UnknownReservedDirectoryCannotBeAdoptedRemovedOrUnblocked(bool destination)
    {
        using var r=new Rig();var source=r.FullSource();var scope=await r.Begin(source.Digest);string unknown=destination?r.Destination(scope):r.Staging(scope);Directory.CreateDirectory(unknown);string marker=Path.Combine(unknown,"PUBLIC_FOREIGN");File.WriteAllText(marker,"PUBLIC_FOREIGN_BYTES");
        Assert.NotNull(await Record.ExceptionAsync(()=>scope.PrepareGenerationAsync(r.Source)));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.Equal("PUBLIC_FOREIGN_BYTES",File.ReadAllText(marker));Assert.True(File.Exists(r.Store.PendingPath));Assert.True(File.Exists(r.Intent(scope)));Assert.True(scope.CleanupConfirmed);Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));r.NoPublication();
    }
}
