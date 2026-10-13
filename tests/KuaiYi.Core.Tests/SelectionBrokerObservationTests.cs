using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionBrokerObservationTests
{
    [Fact]
    public void Observation_preserves_protected_status_without_messages_or_content()
    {
        var observation=SelectionBrokerClient.ObserveMetadata(new("UnsupportedOrProtected",0,0,1,
            Diagnostic:new(SelectionBrokerStage.Root,SelectionBrokerException.Com,unchecked((int)0x80070005))));
        Assert.Equal("UnsupportedOrProtected",observation.Status);Assert.Equal("Root",observation.Stage);
        Assert.Equal("Com",observation.Exception);Assert.Equal(unchecked((int)0x80070005),observation.HResult);
    }
    [Fact]
    public void Unknown_values_cannot_export_arbitrary_strings_as_diagnostics()
    {
        var observation=SelectionBrokerClient.ObserveMetadata(new("PRIVATE_SENTINEL",0,0,1,
            Diagnostic:new((SelectionBrokerStage)999,(SelectionBrokerException)999)));
        Assert.Equal("Unknown",observation.Status);Assert.Equal("Unknown",observation.Stage);Assert.Equal("Unknown",observation.Exception);Assert.Null(observation.HResult);
        var old=SelectionBrokerClient.ObserveMetadata(new("SensitiveOrElevated",0,0,1));
        Assert.Equal("SensitiveOrElevated",old.Status);Assert.Equal("Unknown",old.Stage);
    }
    [Fact]
    public async Task Precancelled_attempt_cannot_inherit_observation_or_start_process()
    {
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await using var client=new SelectionBrokerClient("unused-public-fixture");
        var result=await client.CaptureAsync(new(new(123,456,789,"public.exe"),new(0,0),new(0,0),0,0,true),cancellation.Token);
        Assert.Equal(SelectionCaptureStatus.Cancelled,result.Status);Assert.Null(client.LastObservation);Assert.Null(client.ExitState);
    }
}
