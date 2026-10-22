using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
namespace KuaiYi.Provider.Tests;

// Synthetic local Worker port; the real coordinator, output validation, approval and HTTP are unchanged.
internal static class SmartOcrFixture
{
    internal sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    internal sealed class Worker:ILocalOcrCapabilitiesWorker
    {
        internal bool Hold,Finalized=true,Disposed,FinalizeThrows;
        internal ImageFailure Failure;
        internal string[] Languages=["en-US","zh-Hans"];
        internal uint Maximum=4096;
        internal string Text="PUBLIC APPROVED OCR TEXT";
        internal int Calls,Diagnoses;internal CancellationToken Cancellation;internal ReadOnlyMemory<byte> Loan;
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<OcrDiagnosticReply> DiagnoseAsync(OcrDiagnosticJob job,CancellationToken cancellation=default)
        {Diagnoses++;return Task.FromResult(new OcrDiagnosticReply(job,new(new(true,true,PackageIdentity.DevelopmentName+"_PUBLIC",PackageIdentity.DevelopmentName+"_PUBLIC!ImageWorker",ImageFailure.None),Failure==ImageFailure.None?Languages:[],Failure==ImageFailure.None?Maximum:0,Failure)));}
        public async Task<ImageReply> RunAsync(ReadOnlyMemory<byte> bytes,ImageJob job,CancellationToken cancellation=default)
        {Calls++;Loan=bytes;Cancellation=cancellation;Entered.TrySetResult();if(Hold)await Release.Task;var size=ImagePolicy.InspectPng(bytes.Span);return new(job,Failure,Failure==ImageFailure.None?new(size.Width,size.Height,job.Language,Text,[]):null);}
        public ValueTask<bool> TryFinalizeAsync()=>FinalizeThrows?ValueTask.FromException<bool>(new IOException("PUBLIC_OCR_CLEANUP")):ValueTask.FromResult(Finalized);
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    internal static OcrOnlyCoordinator Create(RuntimeGate gate,Func<SettingsSnapshot?> settings,Guid version,ImageSourceOwner image,Func<Guid,Guid,bool> current,Worker worker,SynchronizationContext? destination=null)
        =>new(gate,settings,current,destination??new Immediate(),()=>worker);
    internal static OcrImageApproval? Approve(RuntimeGate gate,Func<SettingsSnapshot?> settings,Guid version,ImageSourceOwner image,Func<Guid,Guid,bool> current)
    {
        // Legacy synchronous UI call port only. Never block an OCR continuation behind a held Gate.
        var sync=typeof(RuntimeGate).GetField("sync",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(gate)!;
        if(Monitor.IsEntered(sync))throw new InvalidOperationException("Await synthetic OCR approval outside the Gate.");
        return ApproveAsync(gate,settings,version,image,current).GetAwaiter().GetResult();
    }
    internal static async Task<OcrImageApproval?> ApproveAsync(RuntimeGate gate,Func<SettingsSnapshot?> settings,Guid version,ImageSourceOwner image,Func<Guid,Guid,bool> current)
    {
        var local=new OcrOnlyCoordinator(gate,settings,current,new Immediate(),()=>new Worker());
        if(!local.Discover(version,image))return null;await local.Running.ConfigureAwait(false);
        if(local.Capabilities is not {} caps||!local.Start(caps,"en-US"))return null;
        await local.Running.ConfigureAwait(false);return local.Preview is {} preview?local.ApproveForImage(preview):null;
    }
}
