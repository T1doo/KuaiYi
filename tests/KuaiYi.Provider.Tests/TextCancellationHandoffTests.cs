using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class TextCancellationHandoffTests
{
    private sealed class Immediate:SynchronizationContext
    {public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    private sealed class Rig:IAsyncDisposable
    {
        internal readonly RuntimeGate Gate=new(requireProfileBinding:true);
        internal readonly ProviderClient Provider;
        internal readonly OverlayResults Results;
        internal readonly TextTranslationCoordinator Flow;
        internal readonly SettingsSnapshot Settings;
        internal int Dials,SecretReads;internal bool HoldCleanup;internal Exception? ExpectedDisposeFailure;
        internal readonly TaskCompletionSource SecretEntered=new(TaskCreationOptions.RunContinuationsAsynchronously),
            CleanupEntered=new(TaskCreationOptions.RunContinuationsAsynchronously),ReleaseCleanup=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Rig()
        {
            Settings=new(new(){ActiveProfileId="synthetic",Profiles=[new(){Id="synthetic",Model="PUBLIC_SYNTHETIC",BaseUrl="http://127.0.0.1:1/",Authentication=ApiAuthentication.Bearer,SecretRef="key/"+new string('a',32)}]},"synthetic-settings",SettingsLoadState.Current);
            Gate.ObserveSettingsMetadata(Settings);var origin=new EndpointOrigin(new Uri(Settings.Data.Profiles[0].BaseUrl));Gate.ConsentTo(origin);Assert.True(Gate.TryEnable(origin,true));Results=new(Gate);
            Provider=new(null,null,null,(_,_)=>{Interlocked.Increment(ref Dials);throw new InvalidOperationException("No connection permitted.");});
            Flow=new(Gate,()=>Settings,(_,_)=>true,Provider,new Immediate(),Results,_=>{},async(_,_,ct)=>
                {Interlocked.Increment(ref SecretReads);SecretEntered.TrySetResult();
                    try{await Task.Delay(Timeout.Infinite,ct);return new KuaiYi.Providers.SecretLease("SYNTHETIC_ONLY"u8);}
                    finally{if(HoldCleanup){CleanupEntered.TrySetResult();await ReleaseCleanup.Task;}}},
                transportFactory:p=>new(KuaiYi.Providers.ApiProtocol.OpenAIChat,new(p.BaseUrl),"chat/completions",p.Model,Authentication.Bearer,AllowLoopbackHttp:true));
        }
        internal SelectionSourceSnapshot Source(string text)=>new(new(17,123,456,@"C:\Synthetic\editor.exe"),text,text,[new(0,0,2,2)],[1],Settings.Version,text.Length,false,SelectionSourceKind.Uia);
        internal TextTranslationOffer Start(string text)
        {var offer=Assert.IsType<TextTranslationOffer>(Flow.Prepare(Source(text)));Assert.Equal(TextSubmission.Started,Flow.Submit(offer));return offer;}
        internal TextTranslationOffer Pending(bool baseVersion=false)
        {
            if(baseVersion)
            {var prior=Start("base");var id=prior.Identity!.Value;Assert.True(Results.Append(id,"retained base"));Assert.True(Results.Complete(id));}
            var offer=Assert.IsType<TextTranslationOffer>(Flow.Prepare(Source(new string('x',12001))));
            Assert.Equal(TextSubmission.ConfirmationRequired,Flow.Submit(offer));return offer;
        }
        internal CancellationTokenSource Link(TextTranslationOffer offer)
        {Assert.True(Gate.TryLinkRequestCancellation(offer.Identity!.Value,CancellationToken.None,out var linked));return linked!;}
        public async ValueTask DisposeAsync()
        {ReleaseCleanup.TrySetResult();Gate.Disable();
            try{await Flow.DisposeAsync();}catch(Exception error)when(ReferenceEquals(error,ExpectedDisposeFailure)){}
            finally{Provider.Dispose();}Assert.Equal(0,Dials);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancellingPendingConfirmationCannotEraseAWinningOfferFromItsCallback(bool changeStyle)
    {
        await using var r=new Rig();var pending=r.Pending(changeStyle);TextTranslationOffer? winner=null;
        using var linked=r.Link(pending);using var registration=linked.Token.Register(()=>winner=r.Start("winner"));
        if(changeStyle)Assert.True(r.Flow.SetSessionStyle(TranslationPreset.Plain));else r.Flow.RejectConfirmation(pending);
        Assert.NotNull(winner);Assert.True(r.Flow.IsOfferCurrent(winner!));Assert.True(r.Gate.CanApply(winner!.Identity!.Value));
        var repeat=Assert.IsType<TextTranslationOffer>(r.Flow.Prepare(r.Source("winner")));
        Assert.Equal(TextSubmission.Reopened,r.Flow.Submit(repeat));Assert.Equal(winner.Identity,r.Results.Identity);
    }
    [Fact]public async Task StyleIsCommittedBeforeCancellationAndDoesNotOverwriteTheCallbacksLaterStyle()
    {
        await using var r=new Rig();var pending=r.Pending(true);TranslationPreset? observed=null;
        using var linked=r.Link(pending);using var registration=linked.Token.Register(()=>
            {observed=r.Flow.SessionStyle;r.Start("winner");Assert.True(r.Flow.SetSessionStyle(TranslationPreset.Explain));});
        Assert.True(r.Flow.SetSessionStyle(TranslationPreset.Plain));Assert.Equal(TranslationPreset.Plain,observed);Assert.Equal(TranslationPreset.Explain,r.Flow.SessionStyle);Assert.Equal(TranslationPreset.Faithful,r.Flow.SelectedSnapshot!.Preset);
    }
    [Fact]public async Task ThrowingCancellationStillInvalidatesTheOriginalOfferAndPreservesTheException()
    {
        await using var r=new Rig();var pending=r.Pending();var primary=new InvalidOperationException("PUBLIC_CANCEL_FAILURE");
        using var linked=r.Link(pending);using var registration=linked.Token.Register(()=>throw primary);
        var failure=Assert.Throws<AggregateException>(()=>r.Flow.RejectConfirmation(pending));
        Assert.Contains(primary,failure.Flatten().InnerExceptions);Assert.False(r.Flow.IsOfferCurrent(pending));
        var fresh=Assert.IsType<TextTranslationOffer>(r.Flow.Prepare(r.Source("fresh")));Assert.True(r.Flow.IsOfferCurrent(fresh));
    }
    [Fact]public async Task RejectingAnObsoleteConfirmationDoesNotAffectTheCurrentOffer()
    {
        await using var r=new Rig();var old=r.Pending();var fresh=Assert.IsType<TextTranslationOffer>(r.Flow.Prepare(r.Source("fresh")));
        r.Flow.RejectConfirmation(old);Assert.True(r.Flow.IsOfferCurrent(fresh));Assert.Equal(TextSubmission.Started,r.Flow.Submit(fresh));
    }
    [Fact]public async Task ClearRejectsCallbackAdmissionAndNestedClearThenAllowsFreshTranslation()
    {
        await using var r=new Rig();var active=r.Start("active");var prepared=Assert.IsType<TextTranslationOffer>(r.Flow.Prepare(r.Source("prepared")));
        using var linked=r.Link(active);using var registration=linked.Token.Register(()=>
        {
            Assert.Null(r.Flow.Prepare(r.Source("must not start")));Assert.Equal(TextSubmission.Rejected,r.Flow.Submit(prepared));
            Assert.Null(r.Flow.PrepareRegeneration());Assert.False(r.Flow.SetSessionStyle(TranslationPreset.Explain));r.Flow.Clear();
        });
        await r.Flow.ClearAsync().WaitAsync(TimeSpan.FromSeconds(5));Assert.Empty(r.Results.Versions);Assert.Equal(0,r.Flow.RetainedSnapshotCount);Assert.Equal(TextSubmission.Rejected,r.Flow.Submit(prepared));
        var fresh=r.Start("fresh after clear");Assert.True(r.Gate.CanApply(fresh.Identity!.Value));
    }
    [Fact]public async Task ThrowingClearCancellationStillSettlesOriginalJobAndReopensAdmission()
    {
        await using var r=new Rig();r.HoldCleanup=true;var active=r.Start("active");var originalJob=r.Flow.Running;await r.SecretEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var linked=r.Link(active);using var registration=linked.Token.Register(()=>throw new InvalidOperationException("PUBLIC_CLEAR_CANCEL_FAILURE"));
        var clear=r.Flow.ClearAsync();await r.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        bool premature=clear.IsCompleted;r.ReleaseCleanup.TrySetResult();
        await Assert.ThrowsAsync<AggregateException>(()=>clear);Assert.False(premature);Assert.True(originalJob.IsCompleted);Assert.Empty(r.Results.Versions);
        var fresh=r.Start("fresh");Assert.True(r.Gate.CanApply(fresh.Identity!.Value));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task DisposeWaitsAndSharesItsOriginalCompletionEvenWhenCancellationThrows(bool throwsAfter)
    {
        await using var r=new Rig();r.HoldCleanup=true;var active=r.Start("dispose active");var original=r.Flow.Running;
        await r.SecretEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));var prepared=Assert.IsType<TextTranslationOffer>(r.Flow.Prepare(r.Source("prepared")));
        var primary=new InvalidOperationException("PUBLIC_DISPOSE_CANCEL_FAILURE");
        using var linked=r.Link(active);Task? nested=null;using var registration=linked.Token.Register(()=>
        {
            Assert.Null(r.Flow.Prepare(r.Source("callback refused")));Assert.Null(r.Flow.PrepareRegeneration());
            Assert.Equal(TextSubmission.Rejected,r.Flow.Submit(prepared));nested=r.Flow.DisposeAsync().AsTask();
            if(throwsAfter)throw primary;
        });
        var first=r.Flow.DisposeAsync().AsTask();var repeated=r.Flow.DisposeAsync().AsTask();
        await r.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));bool premature=first.IsCompleted||repeated.IsCompleted||nested!.IsCompleted;
        r.ReleaseCleanup.TrySetResult();
        if(throwsAfter)
        {
            var error=await Assert.ThrowsAsync<AggregateException>(()=>first);r.ExpectedDisposeFailure=error;
            Assert.Contains(primary,error.Flatten().InnerExceptions);
            Assert.Same(error,await Assert.ThrowsAsync<AggregateException>(()=>repeated));
            Assert.Same(error,await Assert.ThrowsAsync<AggregateException>(()=>nested!));
        }
        else await first;
        Assert.False(premature);Assert.Same(first,repeated);Assert.Same(first,nested);
        Assert.True(original.IsCompleted);Assert.Equal(0,r.Flow.RetainedSnapshotCount);
        Assert.Null(r.Flow.Prepare(r.Source("still disposed")));Assert.False(r.Results.Append(active.Identity!.Value,"late"));
    }

}
