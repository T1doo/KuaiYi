using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

// Envelope/protocol component evidence only. No G/B qualification, real Installer source or child process.
public sealed partial class InstallationLauncherSettlementTests
{
    private static readonly TimeSpan SourceLimit=TimeSpan.FromSeconds(5);
    private static IEnumerable<Exception> SourceErrors(Exception error)=>error is AggregateException aggregate?aggregate.Flatten().InnerExceptions:new[]{error};
    private static InstallationStableSelection SourceSelection()
    {
        var(selection,input)=Entry();input.Dispose();
        return selection with{Bootstrap=new(Convert.ToBase64String("PUBLIC_OWNER_DATA"u8),Convert.ToBase64String("PUBLIC_ACTIVE_DATA"u8))};
    }
    private static JsonObject SourceClaim(InstallationStableSelection selection)=>new()
    {
        ["kind"]="ClaimedOriginalInstallerSource",["nonce"]=selection.Nonce.ToString("N"),["root"]=selection.Root,["userData"]=selection.UserData,
        ["generationSha256"]=selection.Version.CatalogSha256,["bootstrapSha256"]=new string('0',64),
        ["owner"]=selection.Bootstrap!.Owner,["active"]=selection.Bootstrap.Active,["retired"]=true
    };
    private static string SourceEnvelope(JsonNode value)=>Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value));
    private static async Task RefusedBeforeSourceCallback(InstallationStableSelection selection,string row,bool identityMismatch=false,Guid? nonce=null,string? directory=null,string? data=null)
    {
        using var input=new Input(Encoding.ASCII.GetBytes(row+"\n"));using var output=new Output();var entered=Signal();int calls=0;
        Task<int> original=InstallationControllerBridge.RunAsync(input,output,nonce??selection.Nonce,directory??selection.Version.DirectoryPath,data??selection.UserData,
            (_,_,_)=>{calls++;entered.TrySetResult();return Task.FromResult(0);});
        try
        {
            Assert.Same(original,await Task.WhenAny(original,entered.Task).WaitAsync(SourceLimit));
            Exception? error=await Record.ExceptionAsync(()=>original.WaitAsync(SourceLimit));Assert.NotNull(error);
            if(identityMismatch)Assert.IsType<InstallationRecoveryException>(error);else Assert.IsType<InvalidDataException>(error);
            Assert.Equal(0,calls);Assert.Equal(0,output.Writes);Assert.True(original.IsCompleted);
            Assert.False(Directory.Exists(selection.Root));Assert.False(Directory.Exists(selection.UserData));
        }
        finally
        {
            input.Tail.TrySetResult([]);input.End.TrySetResult();output.Release.TrySetResult();
            await Record.ExceptionAsync(()=>original);
        }
    }
    [Theory][InlineData("StartupQualified")][InlineData("StartupSources")][InlineData("GenerationSourceSha256")][InlineData("BootstrapSha256")][InlineData("RetiredInstaller")]
    public async Task OrdinarySelectionCannotAcquireStartupSourceFromExtraSerializedClaims(string field)
    {
        var selection=SourceSelection();var json=JsonNode.Parse(selection.Bytes())!.AsObject();
        Assert.Equal(selection.Bytes(),JsonSerializer.SerializeToUtf8Bytes(json)); // Isolate the extra field as the only invalid input.
        json[field]=field=="StartupQualified"?JsonValue.Create(true):field.EndsWith("Sha256",StringComparison.Ordinal)?JsonValue.Create(selection.Version.CatalogSha256):SourceClaim(selection);
        await RefusedBeforeSourceCallback(selection,"LAUNCH_CURRENT|"+SourceEnvelope(json));
    }
    [Theory][InlineData("StartupQualified")][InlineData("StartupSources")]
    public async Task OrdinaryBootstrapReceiptEnvelopeCannotHideAnAdditionalStartupAuthority(string field)
    {
        var selection=SourceSelection();var json=JsonNode.Parse(selection.Bytes())!.AsObject();
        json["Bootstrap"]!.AsObject()[field]=field=="StartupQualified"?JsonValue.Create(true):SourceClaim(selection);
        await RefusedBeforeSourceCallback(selection,"LAUNCH_CURRENT|"+SourceEnvelope(json));
    }
    [Theory][InlineData("LAUNCH_CURRENT_TRUSTED")][InlineData("LAUNCH_STARTUP")][InlineData("STARTUP_SOURCES")]
    public async Task OrdinaryEntryCannotChangeItsSourceByInventingANewInitialVerb(string verb)
    {
        var selection=SourceSelection();await RefusedBeforeSourceCallback(selection,verb+"|"+selection.Encode());
    }
    [Theory][InlineData("nonce")][InlineData("directory")][InlineData("data")]
    public async Task OrdinarySourceDataStillRequiresTheOriginalParentChildBinding(string mismatch)
    {
        var selection=SourceSelection();await RefusedBeforeSourceCallback(selection,"LAUNCH_CURRENT|"+selection.Encode(),true,
            nonce:mismatch=="nonce"?Guid.NewGuid():null,directory:mismatch=="directory"?selection.Version.DirectoryPath+"-other":null,data:mismatch=="data"?selection.UserData+"-other":null);
    }
    [Theory][InlineData("STARTUP_SOURCES")][InlineData("STARTUP_READY")][InlineData("STARTUP_BEGIN")]
    public async Task InFlightOrdinarySessionRejectsALateStartupGrantAndJoinsItsOriginalWork(string verb)
    {
        var selection=SourceSelection();using var input=new Input(Encoding.ASCII.GetBytes("LAUNCH_CURRENT|"+selection.Encode()+"\n")){DeferredTail=true};using var output=new Output();
        var entered=Signal();var cancelled=Signal();var session=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationTokenRegistration registration=default;
        var bodyFailure=new IOException("PUBLIC_ORDINARY_ORIGINAL_SESSION_FAILURE");int calls=0,cancellations=0;
        Task<int> original=InstallationControllerBridge.RunAsync(input,output,selection.Nonce,selection.Version.DirectoryPath,selection.UserData,(_,_,token)=>
        {calls++;registration=token.Register(()=>{Interlocked.Increment(ref cancellations);cancelled.TrySetResult();});entered.TrySetResult();return session.Task;});
        try
        {
            await entered.Task.WaitAsync(SourceLimit);Assert.Equal(1,calls);Assert.False(cancelled.Task.IsCompleted);
            input.Tail.SetResult(Encoding.ASCII.GetBytes(verb+"|"+selection.Nonce.ToString("N")+"|"+selection.Fingerprint+"|"+SourceEnvelope(SourceClaim(selection))+"\n"));
            await cancelled.Task.WaitAsync(SourceLimit);Assert.False(original.IsCompleted);Assert.False(session.Task.IsCompleted);Assert.Equal(0,output.Writes);
            session.SetException(bodyFailure);Exception? error=await Record.ExceptionAsync(()=>original.WaitAsync(SourceLimit));Assert.NotNull(error);
            var errors=SourceErrors(error).ToArray();Assert.Contains(bodyFailure,errors);Assert.Contains(errors,e=>e is InvalidDataException invalid&&invalid.Message=="Unexpected parent lifecycle line.");
            Assert.Equal(1,cancellations);Assert.True(session.Task.IsCompleted);Assert.True(original.IsCompleted);
            Assert.Single(output.Lines,row=>row.StartsWith("CONTROLLER_CLOSING|",StringComparison.Ordinal));Assert.DoesNotContain(output.Lines,row=>row.StartsWith("CONTROLLER_EXIT_READY|",StringComparison.Ordinal));
            Assert.False(Directory.Exists(selection.Root));Assert.False(Directory.Exists(selection.UserData));
        }
        finally
        {
            input.Tail.TrySetResult([]);input.End.TrySetResult();session.TrySetException(bodyFailure);output.Release.TrySetResult();
            await Record.ExceptionAsync(()=>original);registration.Dispose();
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CanonicalOrdinaryEnvelopeStillPublishesReadyAndExitsAfterClosingEOF(bool bootstrap)
    {
        var selection=SourceSelection();if(!bootstrap)selection=selection with{Bootstrap=null};
        using var input=new Input(Encoding.ASCII.GetBytes("LAUNCH_CURRENT|"+selection.Encode()+"\n"));using var output=new Output{HoldClosing=true};int calls=0;
        Task<int> original=InstallationControllerBridge.RunAsync(input,output,selection.Nonce,selection.Version.DirectoryPath,selection.UserData,async(actual,publish,token)=>
        {
            calls++;Assert.Equal(selection,actual);await publish(new(Guid.NewGuid(),"KuaiYi.App.exe",actual.Root,actual.UserData,actual.Version,"","",null),token);return 0;
        });
        try
        {
            await output.Held.Task.WaitAsync(SourceLimit);Assert.Equal(1,calls);Assert.False(original.IsCompleted);Assert.Contains(output.Lines,row=>row.StartsWith("BOUND_READY|",StringComparison.Ordinal));
            input.End.SetResult();output.Release.SetResult();Assert.Equal(0,await original.WaitAsync(SourceLimit));
            Assert.Equal(new[]{"BOUND_READY","CONTROLLER_CLOSING","CONTROLLER_EXIT_READY"},output.Lines.Select(row=>row.Split('|')[0]));
            Assert.All(output.Lines,row=>Assert.Contains("|"+selection.Nonce.ToString("N")+"|"+selection.Fingerprint,row));Assert.False(Directory.Exists(selection.Root));
        }
        finally{input.End.TrySetResult();output.Release.TrySetResult();await Record.ExceptionAsync(()=>original);}
    }
    [Fact]public async Task ExactOrdinaryStopRevokesTheOriginalSessionWithoutBecomingASourceProtocolError()
    {
        var selection=SourceSelection();using var input=new Input(Encoding.ASCII.GetBytes("LAUNCH_CURRENT|"+selection.Encode()+"\n")){DeferredTail=true};using var output=new Output();
        var entered=Signal();var cancelled=Signal();var session=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationTokenRegistration registration=default;
        Task<int> original=InstallationControllerBridge.RunAsync(input,output,selection.Nonce,selection.Version.DirectoryPath,selection.UserData,(_,_,token)=>
        {registration=token.Register(()=>cancelled.TrySetResult());entered.TrySetResult();return session.Task;});
        try
        {
            await entered.Task.WaitAsync(SourceLimit);input.Tail.SetResult(Encoding.ASCII.GetBytes($"STOP|{selection.Nonce:N}|{selection.Fingerprint}\n"));
            await cancelled.Task.WaitAsync(SourceLimit);Assert.False(original.IsCompleted);session.SetCanceled();
            Exception? error=await Record.ExceptionAsync(()=>original.WaitAsync(SourceLimit));Assert.NotNull(error);
            Assert.Contains(SourceErrors(error),e=>e is OperationCanceledException);Assert.DoesNotContain(SourceErrors(error),e=>e is InvalidDataException);
            Assert.DoesNotContain(output.Lines,row=>row.StartsWith("CONTROLLER_EXIT_READY|",StringComparison.Ordinal));Assert.True(original.IsCompleted);
        }
        finally{input.Tail.TrySetResult([]);input.End.TrySetResult();session.TrySetCanceled();output.Release.TrySetResult();await Record.ExceptionAsync(()=>original);registration.Dispose();}
    }
}
