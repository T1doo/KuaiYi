using System.Diagnostics;
using System.IO.Pipes;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationSessionTests
{
    private sealed class FixtureRetirementUnconfirmedException(string message):IOException(message);
    private static void AddOriginal(List<Exception> errors,Exception error)
    {if(!errors.Any(original=>ReferenceEquals(original,error)))errors.Add(error);}
    private static async Task<List<Exception>> CaptureFixtureBody(Func<Task> body)
    {List<Exception> errors=[];try{await body();}catch(Exception error){errors.Add(error);}return errors;}
    private static async Task WithParentBody(Parent parent,Func<Parent,Task> body)
    {
        var errors=await CaptureFixtureBody(()=>body(parent));
        try{await parent.DisposeAsync();}catch(Exception error){AddOriginal(errors,error);}
        ThrowBrowserErrors(errors);
    }

    private async Task WithPeer(Rig rig,Func<Peer,Task> body)
    {
        var peer=new Peer(rig.Pipe){RequiresOriginalRetirement=true};
        var errors=await CaptureFixtureBody(()=>body(peer));
        try{await peer.SettleFixtureAsync(rig.Mode,errors.Count!=0);}catch(Exception error){AddOriginal(errors,error);}
        try{await peer.DisposeAsync();}catch(Exception error){AddOriginal(errors,error);}
        if(errors.Count!=0)rig.Keep=true;
        ThrowBrowserErrors(errors);
    }

    private sealed class Peer:IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;
        private readonly StreamReader reader;private StreamWriter? writer;
        private readonly CancellationTokenSource connectingStop=new();
        private readonly object disposalSync=new();private Task? disposal;
        private readonly List<Task> originalIo=[];
        private readonly HashSet<string> cleanupRequests=[];
        private readonly List<Port> ports=[];
        private readonly List<(InstallationUpgradeCoordinator Coordinator,bool DisposeOnSuccess)> coordinators=[];
        private readonly List<Parent> parents=[];
        private readonly List<Parent> browsers=[];
        private readonly List<OriginalRun> runs=[];
        private static readonly List<Peer> Retained=[];
        private sealed class OriginalRun {internal Task? Task;internal bool InvocationFinished;}
        internal readonly Task Connected;
        internal bool RequiresOriginalRetirement,OriginalRetirementConfirmed,ClosedReceiptSeen,CloseRequested;
        internal Func<Task>? BeforeResourceCloseForTest;
        internal CancellationTokenSource? CancelAfterExitRequest;
        internal readonly List<Exception> ExitRequestFailures=[];
        internal readonly List<Exception> ObservedRunFailures=[];
        internal int ResourceCloseAttempts{get;private set;}
        internal readonly List<string> SentCommands=[];
        internal Peer(string name)
        {
            pipe=new(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            reader=new(pipe,leaveOpen:true);Connected=pipe.WaitForConnectionAsync(connectingStop.Token);
        }
        internal Port Own(Port port){ports.Add(port);return port;}
        internal Parent OwnParent(Parent parent){parents.Add(parent);return parent;}
        internal Parent OwnBrowser(Parent parent){browsers.Add(parent);return parent;}
        internal InstallationUpgradeCoordinator Own(InstallationUpgradeCoordinator coordinator,bool disposeOnSuccess=false)
        {coordinators.Add((coordinator,disposeOnSuccess));return coordinator;}
        internal T Observe<T>(Func<T> start) where T:Task
        {
            var original=new OriginalRun();runs.Add(original); // Owner exists before the actual operation starts.
            try{return (T)(original.Task=start());}
            catch(Exception error){original.Task=Task.FromException(error);throw;}
            finally{original.InvocationFinished=true;}
        }
        internal Task Send(string row)
        {
            if(row=="CLOSE")CloseRequested=true;
            SentCommands.Add(row);
            var original=SendCore(row);originalIo.Add(original);return original;
        }
        private async Task SendCore(string row)
        {await Connected.WaitAsync(Limit);writer??=new(pipe,leaveOpen:true);await writer.WriteLineAsync(row);await writer.FlushAsync();}
        internal async Task<string?> Row()
        {
            var original=reader.ReadLineAsync();originalIo.Add(original);
            string? row=await original.WaitAsync(Limit);if(row=="PUBLIC_SHELL_CLOSED")ClosedReceiptSeen=true;return row;
        }
        internal async Task Close(){await Send("CLOSE");Assert.Equal("PUBLIC_SHELL_CLOSED",await Row());}
        private async Task RequestOnce(string command)
        {if(cleanupRequests.Add(command))await Send(command);}
        internal async Task RequestFixtureExitAsync(string mode)
        {
            // This is an unblock/request plan, never a retirement proof. No CLOSE follows RELEASE_EXIT.
            if(mode=="held-after-close")
            {
                if(!CloseRequested&&!ClosedReceiptSeen)await RequestOnce("CLOSE");
                if(!ClosedReceiptSeen)Assert.Equal("PUBLIC_SHELL_CLOSED",await Row());
                await RequestOnce("RELEASE_EXIT");return;
            }
            if(mode.StartsWith("warm-",StringComparison.Ordinal))
                foreach(string command in new[]{"RELEASE_WARM_WORK","RELEASE_WARM_CALLBACK","RELEASE_WARM_WRITE","RELEASE_WARM_FINALIZE"})
                    if(!ClosedReceiptSeen)await RequestOnce(command);
            if(!CloseRequested&&!ClosedReceiptSeen)await RequestOnce("CLOSE");
            if(mode=="held-close"&&!ClosedReceiptSeen)await RequestOnce("RELEASE_CLOSE");
            if(mode=="held-image-cancel"&&!ClosedReceiptSeen)await RequestOnce("RELEASE_CANCEL");
        }
        internal async Task SettleFixtureAsync(string mode,bool bodyFailed)
        {
            List<Exception> errors=[];
            bool live=ports.SelectMany(port=>port.OriginalExits).Any(exit=>!exit.Exit.IsCompleted)
                ||parents.Any(parent=>!parent.OriginalExit.IsCompleted);
            if(live&&Connected.IsCompletedSuccessfully)
                try{await RequestFixtureExitAsync(mode);}catch(Exception error){ExitRequestFailures.Add(error);AddOriginal(errors,error);}
            if(CancelAfterExitRequest!=null)
                try{CancelAfterExitRequest.Cancel();}catch(Exception error){AddOriginal(errors,error);}
            foreach(var entry in coordinators)
                if(bodyFailed||entry.DisposeOnSuccess)
                    try{await entry.Coordinator.DisposeAsync();}catch(Exception error){AddOriginal(errors,error);}
            foreach(var run in runs)
            {
                if(!run.InvocationFinished||run.Task==null){errors.Add(new FixtureRetirementUnconfirmedException("Original fixture invocation is unknown."));continue;}
                try{await run.Task.WaitAsync(Limit);}catch(Exception error)
                {
                    // A completed negative run is observed, not a new cleanup fault. Existing body assertions own its expected outcome.
                    ObservedRunFailures.Add(error);if(bodyFailed||!run.Task.IsCompleted)AddOriginal(errors,error);
                }
            }
            bool settled=true;
            foreach(var port in ports)
            {
                foreach(var exit in port.OriginalExits)
                    try{await exit.Exit.WaitAsync(Limit);}catch(Exception error){settled=false;AddOriginal(errors,error);}
                foreach(var io in port.OriginalIo)
                    try{await io.WaitAsync(Limit);}catch(Exception error){ObservedRunFailures.Add(error);if(bodyFailed||!io.IsCompleted)AddOriginal(errors,error);if(!io.IsCompleted)settled=false;}
                if(port.OriginalExits.Count!=0&&(runs.Count==0||port.OriginalIo.Count==0))settled=false;
            }
            foreach(var parent in parents)
            {
                try{await parent.JoinOriginalAsync();}catch(Exception error){settled=false;AddOriginal(errors,error);}
                if(!parent.ChildRetirementObserved)settled=false;
            }
            if(runs.Any(run=>run.Task?.IsCompleted!=true))settled=false;
            OriginalRetirementConfirmed=settled&&(ports.Any(port=>port.OriginalExits.Count!=0)||parents.Any(parent=>parent.ChildWasObserved));
            foreach(var browser in browsers)
                try{await browser.DisposeAsync();}catch(Exception error){AddOriginal(errors,error);}
            foreach(var parent in parents)
                try{await parent.DisposeAsync();}catch(Exception error){AddOriginal(errors,error);}
            if(Connected.IsCompletedSuccessfully&&!OriginalRetirementConfirmed)
                errors.Add(new FixtureRetirementUnconfirmedException("Original application exit and original IO retirement were not confirmed."));
            ThrowBrowserErrors(errors);
        }
        public ValueTask DisposeAsync()
        {lock(disposalSync)return new(disposal??=DisposeCore());}
        private async Task DisposeCore()
        {
            List<Exception> errors=[];
            if(!Connected.IsCompletedSuccessfully)
                try{connectingStop.Cancel();}catch(Exception error){AddOriginal(errors,error);}
            try{await Connected.WaitAsync(Limit);}catch(OperationCanceledException)when(Connected.IsCanceled&&connectingStop.IsCancellationRequested){}catch(Exception error){AddOriginal(errors,error);}
            if(!Connected.IsCompleted||(Connected.IsCompletedSuccessfully&&RequiresOriginalRetirement&&!OriginalRetirementConfirmed))
            {
                lock(Retained)Retained.Add(this);
                errors.Add(new FixtureRetirementUnconfirmedException("Original fixture connection/owner remains unconfirmed; resources retained."));
                ThrowBrowserErrors(errors);return;
            }
            if(BeforeResourceCloseForTest!=null)try{await BeforeResourceCloseForTest();}catch(Exception error){AddOriginal(errors,error);}
            // Resource retirement sends no protocol. Every close is attempted independently.
            foreach(IDisposable item in new IDisposable?[]{writer,reader,pipe,connectingStop}.OfType<IDisposable>())
                try{ResourceCloseAttempts++;item.Dispose();}catch(Exception error){AddOriginal(errors,error);}
            foreach(var original in originalIo)
                try{await original.WaitAsync(Limit);}catch(Exception error){AddOriginal(errors,error);}
            if(originalIo.Any(original=>!original.IsCompleted)){lock(Retained)Retained.Add(this);}
            ThrowBrowserErrors(errors);
        }
    }
}
