using System.Diagnostics;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionPhaseTimingTests
{
    private static long?[] Data()=>[0,200,200,210,210,240,240,245,245,251,251,252,252,253,253,254,254,255];
    [Fact]public void CumulativeDifferenceAndFirstCrossingAreNotLargestDuration()
    {var p=SelectionPhaseTimingProjection.Project(Data())!.Value;Assert.Equal(9,p.Length);Assert.Equal("Focus",p.First(x=>x.EndMs>=250).Stage);Assert.Equal("Preflight",p.MaxBy(x=>x.DurationMs)!.Stage);Assert.Equal(6,p[4].DurationMs);}
    [Theory][InlineData(-1)][InlineData(601)][InlineData(long.MaxValue)][InlineData(long.MinValue)]
    public void OutOfBoundTimesBecomeOnlyFixedOverLimit(long value)
    {var d=Data();d[0]=value;var p=SelectionPhaseTimingProjection.Project(d)!.Value;Assert.Equal("OverLimit",p[0].State);Assert.Null(p[0].StartMs);Assert.Null(p[0].EndMs);Assert.Null(p[0].DurationMs);}
    [Fact]public void UnexecutedIsNullAndMalformedOrReversedArrayIsDiscarded()
    {var d=new long?[18];Assert.All(SelectionPhaseTimingProjection.Project(d)!.Value,x=>{Assert.Equal("NotRun",x.State);Assert.Null(x.StartMs);});Assert.Null(SelectionPhaseTimingProjection.Project(new long?[19]));d[0]=0;Assert.Null(SelectionPhaseTimingProjection.Project(d));d=Data();d[3]=190;Assert.Null(SelectionPhaseTimingProjection.Project(d));}
    [Fact]public void PrefixGapIsRejectedAndOverLimitDisablesLaterNumericInterpretation()
    {var d=Data();d[2]=d[3]=null;Assert.Null(SelectionPhaseTimingProjection.Project(d));d=Data();d[2]=601;var rows=SelectionPhaseTimingProjection.Project(d)!.Value;Assert.All(rows.Skip(1),x=>{Assert.Equal("OverLimit",x.State);Assert.Null(x.DurationMs);});}
    private sealed class Clock:TimeProvider {public long Tick=10000;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Tick;}
    private sealed class Starter(string fault):IOwnedProcessStarter
    {
        public Process Start(ProcessStartInfo original){var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.CaptureControlFixture/bin/Release/net10.0/KuaiYi.CaptureControlFixture.dll"));var info=OwnedProcessSession.Info(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",null);info.ArgumentList.Clear();info.ArgumentList.Add(root);info.ArgumentList.Add("--selection-fixture");info.ArgumentList.Add("phase-"+fault);foreach(var arg in original.ArgumentList.Take(9))info.ArgumentList.Add(arg);return Process.Start(info)!;}
    }
    private static SelectionGesture Gesture()=>new(new(123,456,789,"public.exe"),new(1,1),new(2,2),1,0,true);
    [Theory][InlineData("normal",SelectionCaptureStatus.Success,true)][InlineData("soft",SelectionCaptureStatus.SoftTimeout,true)][InlineData("identity",SelectionCaptureStatus.ProtocolFailure,false)][InlineData("extra",SelectionCaptureStatus.ProtocolFailure,false)][InlineData("crash",SelectionCaptureStatus.Crash,false)][InlineData("hang",SelectionCaptureStatus.HardTimeout,false)]
    public async Task OnlyNormalCompleteIdentityAndDeadlineValidatedExitPublishes(string fault,SelectionCaptureStatus expected,bool published)
    {await using var client=new SelectionBrokerClient("synthetic",new Starter(fault),boundaryDiagnostic:true,timingDiagnostic:true);var value=await client.CaptureAsync(Gesture(),default);Assert.Equal(expected,value.Status);Assert.Equal(published,client.LastPhaseTimings!=null);await client.DisposeAsync();Assert.Null(client.ExitState);}
    [Theory][InlineData("cancel")][InlineData("late")][InlineData("dispose")]
    public async Task LateCancellationDeadlineOrDisposalCannotPublish(string reason)
    {var clock=new Clock();using var cancel=new CancellationTokenSource();await using var client=new SelectionBrokerClient("synthetic",new Starter("normal"),clock,boundaryDiagnostic:true,timingDiagnostic:true);client.BeforeTimingDeliveryForTest=()=>{if(reason=="cancel")cancel.Cancel();else if(reason=="late")clock.Tick+=600;else client.DisposeAsync().AsTask().GetAwaiter().GetResult();};var result=await client.CaptureAsync(Gesture(),cancel.Token);Assert.Equal(reason=="late"?SelectionCaptureStatus.HardTimeout:SelectionCaptureStatus.Cancelled,result.Status);Assert.Null(client.LastPhaseTimings);await client.DisposeAsync();Assert.Null(client.ExitState);}
    [Fact]public async Task DiagnosticOffDoesNotPublishEvenIfFixtureHasTiming()
    {await using var client=new SelectionBrokerClient("synthetic",new Starter("normal"));Assert.Equal(SelectionCaptureStatus.Success,(await client.CaptureAsync(Gesture(),default)).Status);Assert.Null(client.LastPhaseTimings);}
}
