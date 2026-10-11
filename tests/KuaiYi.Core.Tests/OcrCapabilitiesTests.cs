using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OcrCapabilitiesTests
{
    private static WorkerDiagnostic Valid() => new(new(true,true,"public-test-identity","public-test!ImageWorker",ImageFailure.None),["en-US"],2048,ImageFailure.None);
    [Fact] public void AvailableLanguageAndExactDimensionAccepted() => OcrCapabilitiesPolicy.Require(Valid(),"en-us",new(2048,2048));
    [Theory] [InlineData("zh-CN")] [InlineData("")] public void MissingLanguageExplicit(string language) => Assert.Equal(ImageFailure.LanguageUnavailable,Assert.Throws<ImageJobException>(()=>OcrCapabilitiesPolicy.Require(Valid(),language,new(640,160))).Code);
    [Fact] public void EmptyInstalledLanguageListIsNotEnglishFallback() => Assert.Equal(ImageFailure.LanguageUnavailable,Assert.Throws<ImageJobException>(()=>OcrCapabilitiesPolicy.Require(Valid() with {Languages=[]},"en-US",new(640,160))).Code);
    [Theory] [InlineData(ImageFailure.IdentityMissing)] [InlineData(ImageFailure.IdentityMismatch)] [InlineData(ImageFailure.NativeUnavailable)] [InlineData(ImageFailure.UnsupportedPlatform)]
    public void ExplicitCapabilityFailurePreserved(ImageFailure failure) => Assert.Equal(failure,Assert.Throws<ImageJobException>(()=>OcrCapabilitiesPolicy.Require(Valid() with {Failure=failure},"en-US",new(640,160))).Code);
    [Fact] public void SuccessWithoutActualIdentityRejected() => Assert.Equal(ImageFailure.IdentityMissing,Assert.Throws<ImageJobException>(()=>OcrCapabilitiesPolicy.Require(Valid() with {Identity=new(true,false,null,null,ImageFailure.IdentityMissing)},"en-US",new(640,160))).Code);
    [Fact] public void ZeroMaximumIsBrokenCapabilityNotNoLimit() => Assert.Equal(ImageFailure.NativeFailure,Assert.Throws<ImageJobException>(()=>OcrCapabilitiesPolicy.Require(Valid() with {MaxImageDimension=0},"en-US",new(640,160))).Code);
    [Fact] public void EngineMaximumCheckedIndependentlyFromGlobalImageMaximum() => Assert.Equal(ImageFailure.OcrDimensionExceeded,Assert.Throws<ImageJobException>(()=>OcrCapabilitiesPolicy.Require(Valid(),"en-US",new(2049,160))).Code);
}
