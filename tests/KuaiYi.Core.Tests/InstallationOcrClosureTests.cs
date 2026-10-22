using System.Buffers.Binary;
using System.Text.Json;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

// Only the package/receipt contract. Native/runtime fixture PE bytes are never executed or issued as release pins.
public sealed class InstallationOcrClosureTests
{
    [Theory][InlineData("missing")][InlineData("truncated")][InlineData("wrong-machine")]
    public async Task RePinnedSourceStillMustSupplyAQualifiedOcrNativeDll(string fault)
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-ocr-closure-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=new MaterializedPayloadSource(Path.Combine(root,"source"));byte[] native=MaterializedPayloadSource.NativeFixture();source.Put("KuaiYi.OcrNative.dll",native);
            if(fault=="missing")File.Delete(Path.Combine(source.Root,"KuaiYi.OcrNative.dll"));
            if(fault=="truncated")source.Put("KuaiYi.OcrNative.dll","PUBLIC_TRUNCATED_OCR_PE"u8.ToArray());
            if(fault=="wrong-machine"){BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(0x84),0x14c);source.Put("KuaiYi.OcrNative.dll",native);}
            source.Catalog();var store=new InstallationStore(Path.Combine(root,"application"),Path.Combine(root,"data"),()=>true);
            await Assert.ThrowsAsync<InvalidDataException>(()=>store.ApplyAsync(source.Root,source.Digest));Assert.False(Directory.Exists(Path.Combine(root,"application")));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]public async Task ActualApplyPinsOcrNativeInTheMaterializedReceiptAndRejectsItsLaterMutation()
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-ocr-trust-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=new MaterializedPayloadSource(Path.Combine(root,"source"));source.Put("KuaiYi.OcrNative.dll",MaterializedPayloadSource.NativeFixture());source.Catalog();string expected=MaterializedPayloadSource.Hash(Path.Combine(source.Root,"KuaiYi.OcrNative.dll"));
            var store=new InstallationStore(Path.Combine(root,"application"),Path.Combine(root,"data"),()=>true);var actual=await store.ApplyAsync(source.Root,source.Digest);
            using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(actual.DirectoryPath,InstallationPayload.TrustName)));var files=doc.RootElement.GetProperty("files");
            Assert.True(files.TryGetProperty("KuaiYi.OcrNative.dll",out var pin));Assert.Equal(expected,pin.GetString());Assert.Equal(expected,InstalledRoleFiles.Verify(actual.DirectoryPath,files)["KuaiYi.OcrNative.dll"]);
            File.WriteAllText(Path.Combine(actual.DirectoryPath,"KuaiYi.OcrNative.dll"),"PUBLIC_CHANGED_OCR_BYTES");Assert.Equal(BrowserFailure.DesktopUntrusted,Assert.Throws<BrowserException>(()=>InstalledRoleFiles.Verify(actual.DirectoryPath,files)).Code);Assert.Throws<InvalidDataException>(()=>store.ReadActive());
        }
        finally{Directory.Delete(root,true);}
    }
}
