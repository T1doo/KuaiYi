using KuaiYi.Core.Installation;
using System.Text;
using System.Text.Json;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class InstallationBootstrapPreparationTests
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
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-b-prepare-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"app");internal string Data=>Path.Combine(Root,"data");
        internal readonly MaterializedPayloadSource Generation;
        internal readonly BootstrapSource Bootstrap;
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal Rig(){using var initial=Store.EnterRoleAdmission();Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_OFF"),"PUBLIC_PRESERVED");Generation=new(Path.Combine(Root,"generation-source"));Bootstrap=new(Path.Combine(Root,"bootstrap-source"));}
        internal async Task<(InstallationDeploymentScope Scope,InstallationGenerationCandidate G)> Begin(CancellationToken cancellation=default,InstallationDeploymentFaults? faults=null,string? bootstrapPin=null)
        {var scope=new InstallationDeploymentScope(App,Data,Generation.Digest,bootstrapPin??Bootstrap.Digest,cancellation,faults:faults);await scope.AcquireAsync();await scope.CaptureBeforeAsync(new Slots());return(scope,await scope.PrepareGenerationAsync(Generation.Root));}
        internal string Intent(InstallationDeploymentScope scope)=>Path.Combine(App,"deployment-bootstrap-"+scope.Operation+".json");
        internal string Staging(InstallationDeploymentScope scope)=>Path.Combine(App,"bootstrap-staging-"+scope.Bundle);
        internal string Destination(InstallationDeploymentScope scope)=>Path.Combine(App,"bootstrap-candidates",scope.Bundle);
        internal void NoPublication(){foreach(string path in new[]{Path.Combine(App,"current.json"),InstallationBootstrapDeployment.OwnerPath(App),InstallationBootstrapDeployment.ActivePath(App),Path.Combine(App,"host-registration.json")})Assert.False(File.Exists(path));Assert.False(Directory.Exists(InstallationBootstrapDeployment.BundlesPath(App)));}
        public void Dispose(){Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(Data,"PUBLIC_OFF")));Directory.Delete(Root,true);}
    }
    private static IReadOnlyList<Exception> Errors(Exception error)=>error is AggregateException a?a.Flatten().InnerExceptions:[error];
    [Fact]public async Task OriginalBReadAndCloseFailuresKeepBothErrorsAndSameHandleAcrossSharedLedgerFinalization()
    {
        using var r=new Rig();var(scope,g)=await r.Begin();var primary=new IOException("PUBLIC_B_READ");var close=new IOException("PUBLIC_B_CLOSE");FileStream? original=null;int reads=0;
        var faults=new InstallationPreparationFaults{Acquired=(kind,path,value)=>{if(kind=="read"&&path==Path.Combine(r.Bootstrap.Root,"package.json")){reads++;original=Assert.IsType<FileStream>(value);throw primary;}},BeforeClose=(kind,path)=>{if(kind=="read"&&path==Path.Combine(r.Bootstrap.Root,"package.json"))throw close;}};
        try
        {
            var error=await Record.ExceptionAsync(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g,faults));Assert.NotNull(error);Assert.Contains(primary,Errors(error));Assert.Contains(close,Errors(error));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.False(scope.CleanupConfirmed);Assert.False(original!.SafeFileHandle.IsClosed);Assert.True(File.Exists(r.Store.PendingPath));r.NoPublication();
        }
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.Equal(1,reads);Assert.True(original!.SafeFileHandle.IsClosed);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
    }

    [Fact]public async Task FullyVerifiedBindsOriginalGAndRetainsOnlyInactiveFilesWithoutBlockingLegacyDeploymentAfterAbort()
    {
        using var r=new Rig();var(scope,g)=await r.Begin();string container=Path.GetDirectoryName(r.Destination(scope))!;Directory.CreateDirectory(container);string foreign=Path.Combine(container,"PUBLIC_UNRELATED");File.WriteAllText(foreign,"PUBLIC_UNTOUCHED");var acquired=new List<(string Kind,string Path,IDisposable Value)>();int closed=0;
        var b=await scope.PrepareBootstrapAsync(r.Bootstrap.Root,g,new(){Acquired=(kind,path,value)=>acquired.Add((kind,path,value)),BeforeClose=(_,_)=>closed++});
        Assert.Same(g,b.Generation);Assert.True(b.IsLiveFor(scope));Assert.Equal(r.Destination(scope),b.DirectoryPath);Assert.Equal(r.Bootstrap.Digest,b.CatalogSha256);Assert.Equal(MaterializedPayloadSource.Revision,b.Revision);Assert.Equal(6,Directory.GetFiles(b.DirectoryPath).Length);Assert.Equal(r.Bootstrap.Digest,MaterializedPayloadSource.Hash(Path.Combine(b.DirectoryPath,"package.json")));
        Assert.Equal(b.Revision,InstallationBootstrapPackage.Read(b.DirectoryPath,b.CatalogSha256,g.Version,default).Revision);Assert.Contains(acquired,x=>x.Kind=="hash-input"&&x.Path==Path.Combine(g.Version.DirectoryPath,"KuaiYi.Core.dll"));Assert.Contains(acquired,x=>x.Kind=="binding-input"&&x.Path==Path.Combine(r.Bootstrap.Root,"KuaiYi.Launcher.exe"));Assert.Contains(acquired,x=>x.Kind=="pe-reader"&&x.Path==Path.Combine(r.Bootstrap.Root,"KuaiYi.Core.dll"));Assert.Contains(acquired,x=>x.Kind=="read"&&x.Path==Path.Combine(r.Bootstrap.Root,"KuaiYi.Launcher.deps.json"));Assert.Equal(acquired.Count,closed);Assert.All(acquired.Where(x=>x.Value is FileStream),x=>Assert.True(((FileStream)x.Value).SafeFileHandle.IsClosed));r.NoPublication();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g));var result=await scope.AbortBeforePreparedAsync();Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,result.Status);Assert.Empty(result.Errors);Assert.False(b.IsLiveFor(scope));Assert.True(scope.CleanupConfirmed);Assert.Equal("PUBLIC_UNTOUCHED",File.ReadAllText(foreign));r.NoPublication();
        using(var terminal=JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-failed-*.json"))))){Assert.Equal("VerifiedInactive",terminal.RootElement.GetProperty("inactiveGeneration").GetProperty("State").GetString());Assert.Equal("VerifiedInactive",terminal.RootElement.GetProperty("inactiveBootstrap").GetProperty("State").GetString());Assert.Equal(JsonValueKind.Null,terminal.RootElement.GetProperty("preparedSha256").ValueKind);}
        int wasClosed=closed;Assert.Empty(await scope.SettleOriginalsAsync());Assert.Equal(wasClosed,closed);
        // Production pure filesystem path, still structural G native/runtime fixtures, never a Windows execution claim.
        await r.Store.ApplyAsync(r.Generation.Root,r.Generation.Digest);Assert.Equal(BootstrapOutcome.Deployed,await new InstallationBootstrapDeployment(r.App,r.Data).DeployAsync(r.Bootstrap.Root,r.Bootstrap.Digest));Assert.True(File.Exists(InstallationBootstrapDeployment.ActivePath(r.App)));Assert.True(Directory.Exists(b.DirectoryPath));
    }

    [Theory][InlineData("foreign")][InlineData("retired")][InlineData("canceled")]
    public async Task OnlyTheLiveOriginalGCanAdmitOneBootstrapProducer(string state)
    {
        using var r=new Rig();using var other=new Rig();using var stop=new CancellationTokenSource();var(scope,g)=await r.Begin(stop.Token);var(otherScope,otherG)=await other.Begin();
        try
        {
            if(state=="retired")await scope.AbortBeforePreparedAsync();if(state=="canceled")stop.Cancel();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,state=="foreign"?otherG:g));Assert.False(File.Exists(r.Intent(scope)));Assert.False(Directory.Exists(r.Staging(scope)));r.NoPublication();
        }
        finally{await scope.AbortBeforePreparedAsync();await otherScope.AbortBeforePreparedAsync();}
    }

    [Theory][InlineData("pin")][InlineData("core-byte")][InlineData("launcher-revision")][InlineData("apphost-binding")]
    public async Task PinnedSourceStillMustMatchActualGCoreRevisionAndApphostContract(string fault)
    {
        using var r=new Rig();if(fault is "core-byte" or "launcher-revision")
        {
            string path=Path.Combine(r.Bootstrap.Root,fault=="core-byte"?"KuaiYi.Core.dll":"KuaiYi.Launcher.dll");byte[] bytes=File.ReadAllBytes(path);
            if(fault=="core-byte")bytes[^1]^=1;else{int at=bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(MaterializedPayloadSource.Revision));Assert.True(at>=0);bytes[at]=bytes[at]=='0'?(byte)'1':(byte)'0';}File.WriteAllBytes(path,bytes);r.Bootstrap.Catalog();
        }
        if(fault=="apphost-binding")
        {
            string path=Path.Combine(r.Bootstrap.Root,"KuaiYi.Launcher.exe");byte[] bytes=File.ReadAllBytes(path);int at,count=0;while((at=bytes.AsSpan().IndexOf("KuaiYi.Launcher.dll"u8))>=0){bytes[at]=(byte)'X';count++;}Assert.True(count>0);File.WriteAllBytes(path,bytes);r.Bootstrap.Catalog();
        }
        var(scope,g)=await r.Begin(bootstrapPin:fault=="pin"?new string('0',64):null);var error=await Assert.ThrowsAsync<InvalidDataException>(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g));Assert.Equal(fault switch{"pin"=>"Package catalog digest mismatch.","apphost-binding"=>"Launcher apphost binding differs.",_=>"Bootstrap and generation Core/build differ."},error.Message);Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,(await scope.Finalization).Status);Assert.False(File.Exists(r.Intent(scope)));Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Fact]public async Task HistoricalGCredentialDoesNotReplaceFreshPayloadValidation()
    {
        using var r=new Rig();var(scope,g)=await r.Begin();File.AppendAllText(Path.Combine(g.Version.DirectoryPath,"KuaiYi.Core.dll"),"PUBLIC_CHANGED_G");Assert.True(g.IsLiveFor(scope));Assert.NotNull(await Record.ExceptionAsync(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g)));Assert.False(File.Exists(r.Intent(scope)));Assert.True(scope.CleanupConfirmed);Assert.False(g.IsLiveFor(scope));r.NoPublication();
    }

    [Theory][InlineData("intent","write")][InlineData("catalog","write")][InlineData("intent","flush")][InlineData("catalog","close")]
    public async Task BMetadataPartialFlushAndCloseKeepGlobalUnknownAndOriginalOwnership(string phase,string fault)
    {
        using var r=new Rig();var(scope,g)=await r.Begin();string target=phase=="intent"?r.Intent(scope):Path.Combine(r.Staging(scope),"package.json");var failure=new IOException("PUBLIC_B_METADATA_"+fault);FileStream? original=null;
        var faults=new InstallationPreparationFaults{Acquired=(kind,path,value)=>{if(kind=="metadata-output"&&path==target)original=Assert.IsType<FileStream>(value);},WriteMetadata=(path,stream,bytes)=>{if(fault=="write"&&path==target){stream.Write(bytes.Span[..9]);throw failure;}stream.Write(bytes.Span);},BeforeFlush=(path,_)=>{if(fault=="flush"&&path==target)throw failure;},BeforeClose=(kind,path)=>{if(fault=="close"&&kind=="metadata-output"&&path==target)throw failure;}};
        try
        {
            var error=await Record.ExceptionAsync(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g,faults));Assert.NotNull(error);Assert.Contains(failure,Errors(error));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.True(File.Exists(target));if(fault=="write")Assert.Equal(9,new FileInfo(target).Length);Assert.Equal(fault!="close",original!.SafeFileHandle.IsClosed);Assert.Equal(fault!="close",scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));r.NoPublication();
        }
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(original!.SafeFileHandle.IsClosed);Assert.True(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.True(File.Exists(r.Store.PendingPath));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task OwnedBPayloadPartialOrFlushFailureMayAbortOnlyAfterItsOriginalIOSettles(bool flush)
    {
        using var r=new Rig();var(scope,g)=await r.Begin();var failure=new IOException("PUBLIC_B_PAYLOAD");string? partial=null;FileStream? original=null;
        var error=await Record.ExceptionAsync(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g,new(){WritePayload=(path,stream,bytes)=>{partial=path;original=stream;if(!flush){stream.Write(bytes.Span[..7]);throw failure;}stream.Write(bytes.Span);},BeforeFlush=(path,_)=>{if(flush&&path==partial)throw failure;}}));Assert.NotNull(error);Assert.Contains(failure,Errors(error));Assert.True(original!.SafeFileHandle.IsClosed);Assert.True(File.Exists(partial));if(!flush)Assert.Equal(7,new FileInfo(partial!).Length);Assert.True(File.Exists(r.Intent(scope)));Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,(await scope.Finalization).Status);Assert.True(scope.CleanupConfirmed);r.NoPublication();
        using var terminal=JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-failed-*.json"))));Assert.Equal("IncompleteInactive",terminal.RootElement.GetProperty("inactiveBootstrap").GetProperty("State").GetString());Assert.Equal("VerifiedInactive",terminal.RootElement.GetProperty("inactiveGeneration").GetProperty("State").GetString());
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancelAfterLastValidationOrDeliveryRetiresBothOriginalCredentials(bool beforeDelivery)
    {
        using var r=new Rig();using var stop=new CancellationTokenSource();var(scope,g)=await r.Begin(stop.Token);
        if(beforeDelivery){var error=await Record.ExceptionAsync(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g,new(){Stage=s=>{if(s==GenerationPreparationStage.BeforeCredential)stop.Cancel();}}));Assert.NotNull(error);Assert.Contains(Errors(error),x=>x is OperationCanceledException);}
        else{var b=await scope.PrepareBootstrapAsync(r.Bootstrap.Root,g);Assert.True(b.IsLiveFor(scope));stop.Cancel();Assert.False(b.IsLiveFor(scope));await scope.AbortBeforePreparedAsync();}
        Assert.False(g.IsLiveFor(scope));Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Theory][InlineData("read")][InlineData("copy-output")][InlineData("flush")]
    public async Task CancellationAndAbortJoinTheSameBReadCopyOrFlushBeforeClosing(string phase)
    {
        using var r=new Rig();using var stop=new CancellationTokenSource();using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);bool blocked=false;var(scope,g)=await r.Begin(stop.Token);
        void Hold(){if(blocked)return;blocked=true;entered.SetResult();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("PUBLIC_B_BARRIER");}
        var prepare=scope.PrepareBootstrapAsync(r.Bootstrap.Root,g,new(){Acquired=(kind,path,_)=>{if(kind==phase&&(phase!="read"||path==Path.Combine(r.Bootstrap.Root,"package.json")))Hold();},BeforeFlush=(path,_)=>{if(phase=="flush"&&path==Path.Combine(r.Staging(scope),"KuaiYi.Core.dll"))Hold();}});await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));Task<InstallationDeploymentOutcome>? final=null;
        try{stop.Cancel();final=scope.AbortBeforePreparedAsync();Assert.False(prepare.IsCompleted);Assert.False(final.IsCompleted);Assert.False(scope.CleanupConfirmed);await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g));r.NoPublication();}
        finally{release.Set();}
        var error=await Record.ExceptionAsync(async()=>await prepare);Assert.NotNull(error);Assert.Contains(Errors(error),x=>x is OperationCanceledException);Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,(await final!).Status);Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task BMoveUnknownNeverReplaysOrReinterpretsItsInactiveCandidate(bool moved)
    {
        using var r=new Rig();var(scope,g)=await r.Begin();var failure=new IOException("PUBLIC_B_MOVE");int moves=0;var error=await Record.ExceptionAsync(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g,new(){Move=(from,to)=>{moves++;if(moved)Directory.Move(from,to);throw failure;}}));Assert.NotNull(error);Assert.Contains(failure,Errors(error));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.Equal(moved,Directory.Exists(r.Destination(scope)));Assert.Equal(!moved,Directory.Exists(r.Staging(scope)));Assert.Empty(await scope.SettleOriginalsAsync());Assert.Same(scope.Finalization,scope.AbortBeforePreparedAsync());Assert.Equal(1,moves);Assert.True(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Fact]public async Task ClosingNewBReadAndCloseAreDispatchedToOriginalChannelAndSamePendingSettlement()
    {
        using var r=new Rig();bool closing=false;var(scope,g)=await r.Begin(faults:new(){AfterClosing=()=>{closing=true;return Task.CompletedTask;}});var read=new IOException("PUBLIC_B_FINAL_READ");var close=new IOException("PUBLIC_B_FINAL_CLOSE");FileStream? original=null;int reads=0;
        var faults=new InstallationPreparationFaults{Acquired=(kind,path,value)=>{if(closing&&kind=="read"&&path==r.Intent(scope)){reads++;original=Assert.IsType<FileStream>(value);throw read;}},BeforeClose=(kind,path)=>{if(closing&&kind=="read"&&path==r.Intent(scope))throw close;}};var b=await scope.PrepareBootstrapAsync(r.Bootstrap.Root,g,faults);
        try{var result=await scope.AbortBeforePreparedAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(read,result.Errors.SelectMany(Errors));Assert.Contains(close,result.Errors.SelectMany(Errors));Assert.False(original!.SafeFileHandle.IsClosed);Assert.False(scope.CleanupConfirmed);Assert.False(b.IsLiveFor(scope));Assert.True(File.Exists(r.Store.PendingPath));}
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.Equal(1,reads);Assert.True(original!.SafeFileHandle.IsClosed);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));r.NoPublication();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ForeignBIntentOrCatalogCannotBeClearedAtClosing(bool catalog)
    {
        using var r=new Rig();var(scope,g)=await r.Begin();var b=await scope.PrepareBootstrapAsync(r.Bootstrap.Root,g);string path=catalog?Path.Combine(b.DirectoryPath,"package.json"):r.Intent(scope);File.WriteAllText(path,"PUBLIC_FOREIGN_B");Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.AbortBeforePreparedAsync()).Status);Assert.Equal("PUBLIC_FOREIGN_B",File.ReadAllText(path));Assert.True(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task UnknownBStagingOrReservedBundleCannotBeAdoptedOrDeleted(bool destination)
    {
        using var r=new Rig();var(scope,g)=await r.Begin();string path=destination?r.Destination(scope):r.Staging(scope);Directory.CreateDirectory(path);string marker=Path.Combine(path,"PUBLIC_FOREIGN");File.WriteAllText(marker,"PUBLIC_UNKNOWN");Assert.NotNull(await Record.ExceptionAsync(()=>scope.PrepareBootstrapAsync(r.Bootstrap.Root,g)));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.Equal("PUBLIC_UNKNOWN",File.ReadAllText(marker));Assert.True(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);r.NoPublication();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void StreamedApphostBindingChecksCrossChunkNeedleAndExact32MiBBound(bool oversize)
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-b-binding-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string input=Path.Combine(root,"PUBLIC_APPHOST");byte[] needle="KuaiYi.Launcher.dll"u8.ToArray();
        try
        {
            using(var output=new FileStream(input,FileMode.CreateNew,FileAccess.Write)){output.SetLength(32*1024*1024+(oversize?1:0));output.Position=65536-needle.Length/2;output.Write(needle);}
            FileStream? original=null;var io=new InstallationPreparationIO(root,Path.Combine(root,"stage"),Path.Combine(root,"final"),Path.Combine(root,"intent"),default,new(){Acquired=(kind,_,value)=>{if(kind=="binding-input")original=Assert.IsType<FileStream>(value);}});
            if(oversize)Assert.Throws<InvalidDataException>(()=>io.ContainsPayload(input,needle));else Assert.True(io.ContainsPayload(input,needle));Assert.True(io.CleanupConfirmed);Assert.True(original!.SafeFileHandle.IsClosed);
        }
        finally{Directory.Delete(root,true);}
    }
}
