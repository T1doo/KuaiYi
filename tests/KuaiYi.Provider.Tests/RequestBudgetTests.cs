using System.Net.Http.Json;
using System.Collections.Immutable;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.MockProvider;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class RequestBudgetTests(ServerFixture fixture):IClassFixture<ServerFixture>
{
    private sealed class Clock:TimeProvider
    {
        private readonly object sync=new();private long timestamp;private readonly List<Timer> timers=[];
        public override long TimestampFrequency=>1000;public override long GetTimestamp(){lock(sync)return timestamp;}
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
        {var timer=new Timer(this,callback,state);lock(sync)timers.Add(timer);timer.Change(dueTime,period);return timer;}
        public void Advance(TimeSpan duration)
        {List<Timer> due;lock(sync){timestamp+=checked((long)duration.TotalMilliseconds);due=timers.Where(t=>t.Due<=timestamp).ToList();foreach(var timer in due)timer.Due=long.MaxValue;}foreach(var timer in due)timer.Fire();}
        private sealed class Timer(Clock clock,TimerCallback callback,object? state):ITimer
        {
            public long Due=long.MaxValue;private bool disposed;
            public bool Change(TimeSpan dueTime,TimeSpan period){lock(clock.sync){if(disposed)return false;Due=dueTime==Timeout.InfiniteTimeSpan?long.MaxValue:clock.timestamp+checked((long)dueTime.TotalMilliseconds);return true;}}
            public void Fire(){if(!disposed)callback(state);}
            public void Dispose(){lock(clock.sync){disposed=true;Due=long.MaxValue;}}
            public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
        }
    }
    private async Task Barrier(string run)
    {using var http=new HttpClient();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(true){var reply=await http.GetFromJsonAsync<System.Text.Json.JsonElement>(new Uri(fixture.BaseUri,$"barriers/{run}"),timeout.Token);if(reply.GetProperty("ready").GetBoolean())return;await Task.Delay(10,timeout.Token);}}
    [Theory] [InlineData(60)] [InlineData(120)] [InlineData(180)]
    public async Task ProductionTransportHonorsFrozenLessEqualAndGreaterBudgets(int seconds)
    {
        string run=Guid.NewGuid().ToString("N");var clock=new Clock();var settingsProfile=new KuaiYi.Core.Settings.ApiProfile{Id="p",BaseUrl=new Uri(fixture.BaseUri,$"runs/{run}/held-headers/").AbsoluteUri,Model="PUBLIC_FIXTURE_MODEL",Authentication=ApiAuthentication.None,AllowLoopbackHttp=true,TimeoutSeconds=seconds};
        var transport=ApiProfileMapper.Plan(settingsProfile).Transport;Assert.Equal(seconds,transport.TotalTimeoutSeconds);using var provider=new ProviderClient(clock:clock);var gate=new RuntimeGate();var origin=new EndpointOrigin(transport.ResolveEndpoint());gate.ConsentTo(origin);gate.TryEnable(origin,true);gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);var events=new List<ModelEvent>();
        async Task Run(){await foreach(var value in provider.GenerateAsync(new(transport,ApiConnectionTestContent.System,ApiConnectionTestContent.Source),gate,id,null,ct))events.Add(value);}
        var job=Run();await Barrier(run);clock.Advance(TimeSpan.FromSeconds(seconds)-TimeSpan.FromMilliseconds(1));Assert.False(job.IsCompleted);clock.Advance(TimeSpan.FromMilliseconds(1));await job.WaitAsync(TimeSpan.FromSeconds(6));Assert.Contains(events,e=>e.Kind==ModelEventKind.Failed&&e.Reason=="REQUEST_TIMEOUT");Assert.DoesNotContain(events,e=>e.Kind==ModelEventKind.Completed);
    }
    [Theory] [InlineData(60)] [InlineData(120)] [InlineData(180)]
    public void SharedBudgetNeverRestartsAfterCredentialAcquisition(int seconds)
    {
        var clock=new Clock();var budget=new ProviderRequestBudget(seconds,clock);using var first=budget.Link(CancellationToken.None);clock.Advance(TimeSpan.FromSeconds(seconds-1));Assert.Equal(TimeSpan.FromSeconds(1),budget.Remaining);using var transport=budget.Link(CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(999));Assert.False(first.IsCancellationRequested);Assert.False(transport.IsCancellationRequested);clock.Advance(TimeSpan.FromMilliseconds(1));Assert.True(first.IsCancellationRequested);Assert.True(transport.IsCancellationRequested);Assert.Throws<OperationCanceledException>(budget.Check);
    }
    [Fact] public async Task ExpiredSharedBudgetCannotStartAnHttpRequest()
    {
        string run=Guid.NewGuid().ToString("N");var clock=new Clock();var profile=new KuaiYi.Providers.ApiProfile(KuaiYi.Providers.ApiProtocol.OpenAIChat,new(fixture.BaseUri,$"runs/{run}/fragmented/"),"chat/completions","PUBLIC",Authentication.None,AllowLoopbackHttp:true,TotalTimeoutSeconds:180);var budget=new ProviderRequestBudget(180,clock);clock.Advance(TimeSpan.FromSeconds(180));using var provider=new ProviderClient(clock:clock);
        var gate=new RuntimeGate();var origin=new EndpointOrigin(profile.ResolveEndpoint());gate.ConsentTo(origin);gate.TryEnable(origin,true);gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct);var events=new List<ModelEvent>();await foreach(var e in provider.GenerateAsync(new(profile,ApiConnectionTestContent.System,ApiConnectionTestContent.Source),gate,id,null,ct,requestBudget:budget))events.Add(e);
        Assert.Contains(events,e=>e.Reason=="REQUEST_TIMEOUT");using var http=new HttpClient();Assert.Empty((await http.GetFromJsonAsync<CapturedRequest[]>(new Uri(fixture.BaseUri,$"requests/{run}")))!);
    }
}
