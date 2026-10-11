using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ImageJsonTests
{
    [Fact] public void DuplicateIdentityFieldRejected() => Assert.Throws<InvalidDataException>(()=>ImageJson.Read<ImageJob>("{\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"version\":1,\"version\":2,\"epoch\":1,\"language\":\"en-US\",\"byteLength\":1}"u8.ToArray()));
    [Fact] public void UnknownPrivateControlFieldRejected() => Assert.Throws<System.Text.Json.JsonException>(()=>ImageJson.Read<ImageJob>("{\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"version\":1,\"epoch\":1,\"language\":\"en-US\",\"byteLength\":1,\"command\":\"ShowSettings\"}"u8.ToArray()));
    [Fact] public void NestedDuplicateResultRejected() => Assert.Throws<InvalidDataException>(()=>ImageJson.Read<OcrOutput>("{\"width\":1,\"height\":1,\"language\":\"en-US\",\"text\":\"\",\"lines\":[{\"text\":\"\",\"text\":\"spoof\",\"words\":[]}]}"u8.ToArray()));
    [Fact] public void MainAndWorkerDoNotInventIdentityOnLinux() { if (OperatingSystem.IsWindows()) { Assert.True(PackageIdentity.Read("KuaiYi").Windows);return; }Assert.Equal(ImageFailure.UnsupportedPlatform,PackageIdentity.Read("KuaiYi").Failure);Assert.Equal(ImageFailure.UnsupportedPlatform,NativeOcr.Diagnose().Failure); }
}
