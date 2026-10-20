using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;

public sealed partial class ProductionScreenshotTranslationTests
{
    private static void AdoptAreaPattern(Rig r)
    {r.Image.Dispose();r.Version=Guid.NewGuid();r.Raw=ScreenshotAreaRoutingTests.Pattern();r.Image=new(new(new(-3841,50,-1,56),r.Raw));}
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task GeneralScreenshotRoutesTheSameBoundedPngAndPhysicalMappingIntoBothStages(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol,auth:true);AdoptAreaPattern(r);var raw=r.Raw.ToArray();await r.Generate();var snapshot=r.Flow.SelectedSnapshot!;var map=Assert.IsType<ImageTransform>(snapshot.Transform);
        Assert.Equal(ScreenshotNormalizationProof.AreaWhiteCompositeDownscale,snapshot.NormalizationProof);Assert.Equal(new PixelRect(-3841,50,-1,56),map.OriginalCrop);Assert.Equal(new ImageSize(2560,4),map.NormalizedSize);Assert.Equal(new DesktopImageRectangle(-3841,50,3840,6),map.NormalizedToDesktop(new NormalizedImageRectangle(0,0,2560,4)));
        var first=Body(protocol,r.Script.Bodies.First());var last=Body(protocol,r.Script.Bodies.Last());Assert.Equal(first.Image,last.Image);Assert.InRange(last.Image.Length,1,4194304);Assert.Equal(new ImageSize(2560,4),ImagePolicy.InspectPng(last.Image));var rows=ScreenshotNormalizationTests.DecodeRgb(last.Image);Assert.Equal(new byte[]{20,20,20,87,87,87},rows.AsSpan(1,6).ToArray());Assert.Equal(new byte[]{33,33,33,100,100,100},rows.AsSpan(3*(2560*3+1)+1,6).ToArray());
        Assert.Equal(new DesktopImageRectangle(-1921,53,1920,3),map.NormalizedToDesktop(map.RelativeToNormalized(new(.5,.5,.5,.5))));Assert.Throws<ArgumentOutOfRangeException>(()=>map.DesktopToNormalized(new DesktopImagePoint(Math.BitIncrement(-1d),56)));
        Assert.All(r.Updates.Where(u=>u.Kind is ProductionScreenshotEvent.Prepared or ProductionScreenshotEvent.Classifying or ProductionScreenshotEvent.Generating or ProductionScreenshotEvent.Completed),u=>Assert.Same(map,u.Snapshot.Transform));Assert.DoesNotContain("-3841",first.System+last.System);Assert.DoesNotContain("ImageTransform",first.System+last.System);Assert.Equal(raw,r.Raw);Assert.Equal(2,r.SecretReads);Assert.Null(r.Flow.Prepare(r.Version,r.Image));Assert.Equal("IMAGE_REUSE_DISABLED",r.Flow.LastRejection);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task UnreadableDownscaledClassificationKeepsOldResultAndCannotOpenGenerationSecretOrSendAgain(Protocol protocol)
    {
        await using var r=await Rig.Create(protocol,auth:true);await r.Generate();string oldText=r.Results.Text;AdoptAreaPattern(r);int before=r.Script.Requests,reads=r.SecretReads;Assert.True(r.Flow.Submit(r.Offer(),true));var identity=r.Results.Identity!.Value;await r.Requests(before+1);await r.Reply(ImageClassificationTests.Json(readability:"unreadable"));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        var snapshot=r.Flow.SnapshotForVersion(identity.VersionId)!;Assert.Equal(ScreenshotNormalizationProof.AreaWhiteCompositeDownscale,snapshot.NormalizationProof);Assert.Equal(new ImageSize(2560,4),ImagePolicy.InspectPng(Body(protocol,r.Script.Bodies.Last()).Image));Assert.Equal(before+1,r.Script.Requests);Assert.Equal(reads+1,r.SecretReads);Assert.Equal("CLEARER_IMAGE_REQUIRED",r.Flow.FailureForVersion(identity.VersionId));Assert.Null(snapshot.GenerationUsage);Assert.Null(snapshot.Plan);Assert.Contains(r.Results.Versions,v=>v.Complete&&v.Text==oldText);Assert.DoesNotContain(r.Updates,u=>u.Identity==identity&&u.Kind==ProductionScreenshotEvent.Generating);
    }
    [Theory][InlineData("stop")][InlineData("clear")][InlineData("source")][InlineData("off")][InlineData("lock")]
    public async Task GeneralPreparedReentryRevokesSourceBeforeAnySecretOrClassification(string action)
    {
        await using var r=await Rig.Create(auth:true);AdoptAreaPattern(r);bool acted=false;r.OnUpdate=u=>
        {if(u.Kind!=ProductionScreenshotEvent.Prepared)return;acted=true;switch(action){case "stop":r.Flow.Stop();break;case "clear":r.Flow.Clear();break;case "source":r.Current=false;break;case "off":r.Gate.Disable();break;case "lock":r.Gate.SetSessionLocked(true);break;}};
        Assert.True(r.Flow.Submit(r.Offer(),true));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(acted);Assert.Equal(0,r.SecretReads);Assert.Equal(0,r.Script.Requests);Assert.False(r.Gate.HasRunningGeneration);Assert.False(r.Image.IsDisposed);Assert.DoesNotContain(r.Updates,u=>u.Kind is ProductionScreenshotEvent.Generating or ProductionScreenshotEvent.Completed);
    }
}
