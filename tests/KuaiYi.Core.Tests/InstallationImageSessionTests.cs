using KuaiYi.Core.Installation;
using System.Text;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed partial class InstallationSessionTests
{
    private const string ImagePath="C:\\public\\合成 image.gif";
    private static async Task SendControl(Port port,string line)
    {await port.Children.Single().StandardInput.WriteLineAsync(line);await port.Children.Single().StandardInput.FlushAsync();}
    [Fact]public async Task ColdImageUsesTheHealthyOwnedChildOnceAndTheOriginalCleanSessionExit()
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);await using var coordinator=port.Coordinator(r);
        Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,imagePath:ImagePath);
        try
        {
            await port.ImageQueued.Task.WaitAsync(Limit);await peer.Send("WAIT_IMAGE");string row=(await peer.Row())!;
            Assert.StartsWith("PUBLIC_IMAGE|",row);Assert.EndsWith("|"+ImagePath,row);r.Fence();Assert.False(run.IsCompleted);
            foreach(string p in Directory.GetFiles(r.App,"*.json"))Assert.DoesNotContain(ImagePath,File.ReadAllText(p));
            await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));Assert.Equal(1,port.SessionIoCalls);Assert.False(File.Exists(r.Owner));
            Assert.Single(File.ReadAllLines(Path.Combine(r.Root,"PUBLIC_IMAGE_QUEUED")));Assert.Single(Directory.GetFiles(r.App,"launch-*-finished.json"));Assert.Equal("PUBLIC_OFF",File.ReadAllText(Path.Combine(r.Data,"PUBLIC_SENTINEL")));
        }
        finally{if(!run.IsCompleted){await peer.Send("CLOSE");try{await run.WaitAsync(Limit);}catch(Exception){/* Exact run remains held by coordinator disposal. */}}}
    }
    [Theory][InlineData("duplicate")][InlineData("new-request")][InlineData("handoff")]
    public async Task ConsumedImageCannotBeReplayedReplacedOrConvertedToHandoff(string fault)
    {
        using var r=new Rig();var selected=await r.Prepare(fault=="handoff"?"held-close":"healthy");await using var peer=new Peer(r.Pipe);var port=new Port(output);var coordinator=port.Coordinator(r);
        Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,imagePath:ImagePath);await port.ImageQueued.Task.WaitAsync(Limit);
        await peer.Send("WAIT_IMAGE");string row=(await peer.Row())!;Guid request=Guid.ParseExact(row.Split('|')[1],"N");var proof=port.Proof!;
        string command=fault=="handoff"?"OFFER|PUBLIC_INVALID_OFFER": "IMAGE|"+new InstallationImageOffer(proof.Launch,proof.Fingerprint,fault=="duplicate"?request:Guid.NewGuid(),ImagePath).Encode();
        await SendControl(port,command);
        if(fault=="handoff")
        {
            await peer.Send("WAIT_IMAGE_REVOKED");Assert.Equal("PUBLIC_IMAGE_REVOKED",await peer.Row());
            await peer.Send("WAIT_CLEANUP");Assert.Equal("PUBLIC_CLEANUP_ENTERED",await peer.Row());
            await peer.Send("PING");Assert.Equal("PUBLIC_SHELL|Closing|ControlFault=InvalidDataException",await peer.Row());
            await peer.Send("RELEASE_CLOSE");
        }
        Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row());
        await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
        Assert.Single(File.ReadAllLines(Path.Combine(r.Root,"PUBLIC_IMAGE_QUEUED")));Assert.Equal(new[]{0},port.CleanupExits);r.Fence();
    }
    [Theory][InlineData("launch")][InlineData("proof")][InlineData("record")][InlineData("unknown")][InlineData("oversize")]
    public async Task InvalidBoundImageNeverQueuesAndRetainsTheOriginalFailureFence(string fault)
    {
        using var r=new Rig();var selected=await r.Prepare();await using var peer=new Peer(r.Pipe);var port=new Port(output);var coordinator=port.Coordinator(r);
        Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);await port.WaitForHealthyAsync(run);await port.WaitForBrowserPublishedAsync(run);var proof=port.Proof!;
        var offer=new InstallationImageOffer(fault=="launch"?Guid.NewGuid():proof.Launch,fault=="proof"?new string('A',64):proof.Fingerprint,Guid.NewGuid(),ImagePath);
        string encoded=offer.Encode();if(fault=="record")File.WriteAllText(r.Owner,"PUBLIC_CHANGED_RETAIN");
        if(fault=="unknown"){string json=Encoding.UTF8.GetString(Convert.FromBase64String(encoded));encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(json[..^1]+",\"unknown\":1}"));}
        if(fault=="oversize")encoded=new string('A',49153);
        await SendControl(port,"IMAGE|"+encoded);Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row());
        await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
        Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_IMAGE_QUEUED")));Assert.Equal(new[]{0},port.CleanupExits);r.Fence();
    }
    [Fact]public async Task ControlEofRevokesTheQueuedImageWhileTheOriginalCloseCleanupIsStillHeld()
    {
        using var r=new Rig();var selected=await r.Prepare("held-close");await using var peer=new Peer(r.Pipe);var port=new Port(output);var coordinator=port.Coordinator(r);
        Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,imagePath:ImagePath);await port.ImageQueued.Task.WaitAsync(Limit);
        port.Children.Single().StandardInput.Close();await peer.Send("WAIT_IMAGE_REVOKED");Assert.Equal("PUBLIC_IMAGE_REVOKED",await peer.Row());
        Assert.False(run.IsCompleted);Assert.False(port.Children.Single().HasExited);r.Fence();await peer.Send("RELEASE_CLOSE");Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row());
        await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();Assert.Equal(new[]{0},port.CleanupExits);r.Fence();
    }
    [Fact]public async Task CleanStopMustJoinTheOriginalImageCancellationCallbackBeforeAttestingExit()
    {
        using var r=new Rig();var selected=await r.Prepare("held-image-cancel");await using var peer=new Peer(r.Pipe);var port=new Port(output);await using var coordinator=port.Coordinator(r);
        Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,imagePath:ImagePath);
        try
        {
            await port.ImageQueued.Task.WaitAsync(Limit);await SendControl(port,"STOP");
            await peer.Send("WAIT_IMAGE_REVOKED");Assert.Equal("PUBLIC_IMAGE_REVOKED",await peer.Row());
            await peer.Send("WAIT_CLEANUP");Assert.Equal("PUBLIC_CLEANUP_ENTERED",await peer.Row());
            Assert.False(run.IsCompleted);Assert.False(port.Children.Single().HasExited);r.Fence();
        }
        finally{await peer.Send("RELEASE_CANCEL");}
        Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row());Assert.Equal(0,await run.WaitAsync(Limit));Assert.False(File.Exists(r.Owner));Assert.Equal(1,port.SessionIoCalls);
    }
}
