using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ClipboardCopyEvidenceTests
{
    private sealed class Starter(string fault):IOwnedProcessStarter
    {
        public int Starts;public Process Start(ProcessStartInfo original){Starts++;string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.CaptureControlFixture/bin",configuration,"net10.0/KuaiYi.CaptureControlFixture.dll"));var info=OwnedProcessSession.Info(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",null);info.ArgumentList.Clear();info.ArgumentList.Add(assembly);info.ArgumentList.Add("--selection-fixture");info.ArgumentList.Add(fault);foreach(string value in original.ArgumentList)info.ArgumentList.Add(value);return Process.Start(info)??throw new IOException();}
    }
    [Theory][InlineData("copy-status",SelectionCaptureStatus.ProtocolFailure)][InlineData("copy-good",SelectionCaptureStatus.EmptyOrUnsupported)][InlineData("copy-focus",SelectionCaptureStatus.ProtocolFailure)][InlineData("copy-runtime",SelectionCaptureStatus.ProtocolFailure)][InlineData("copy-control",SelectionCaptureStatus.ProtocolFailure)][InlineData("copy-association",SelectionCaptureStatus.ProtocolFailure)]
    public async Task ActualUiaProtocolMustProduceTrustedBoundedCopyEvidenceBeforeCompatibility(string fault,SelectionCaptureStatus expected)
    {
        var starter=new Starter(fault);await using var client=new SelectionBrokerClient("public-fixture",starter);var gesture=new SelectionGesture(new(123,456,789,@"C:\Public\public.exe"),new(3,3),new(70,3),1000,1);var result=await client.CaptureAsync(gesture,default);Assert.Equal(expected,result.Status);Assert.Equal("",result.Text);Assert.Equal(1,starter.Starts);if(fault=="copy-good"){Assert.NotNull(result.CopyEvidence);Assert.True(SelectionTextPolicy.CopyTrusted(gesture,result.CopyEvidence));}else Assert.Null(result.CopyEvidence);await client.DisposeAsync();Assert.Null(client.ExitState);
    }
}
