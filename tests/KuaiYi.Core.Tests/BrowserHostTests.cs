using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class BrowserHostTests
{
    private sealed class Child: IAsyncDisposable
    {
        private readonly CancellationTokenSource deadline=new(TimeSpan.FromSeconds(8));
        public Process Process {get;}
        public CancellationToken Token=>deadline.Token;
        public Child(string mode,bool production=false)
        {
            string config=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            string relative=production?$"../../../../../src/KuaiYi.NativeHost/bin/{config}/net10.0/win-x64/KuaiYi.NativeHost.dll":$"../../../../../tools/KuaiYi.BrowserHostFixture/bin/{config}/net10.0/KuaiYi.BrowserHostFixture.dll";
            var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            info.ArgumentList.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,relative)));info.ArgumentList.Add(mode);
            Process=Process.Start(info)??throw new InvalidOperationException("Fixture start failed.");
        }
        public async Task<JsonDocument> Exchange(object message,string type)
        {
            await NativeMessagingCodec.WriteAsync(Process.StandardInput.BaseStream,message,Token);
            return await Read(type);
        }
        public async Task<JsonDocument> Read(string type)
        {
            var doc=await NativeMessagingCodec.ReadAsync(Process.StandardOutput.BaseStream,Token);Assert.NotNull(doc);Assert.Equal(type,doc.RootElement.GetProperty("type").GetString());return doc;
        }
        public async Task<JsonDocument> Finish(int code)
        {
            Process.StandardInput.Close();await Process.WaitForExitAsync(Token);Assert.Equal(code,Process.ExitCode);
            Assert.Null(await NativeMessagingCodec.ReadAsync(Process.StandardOutput.BaseStream,Token));
            var lines=(await Process.StandardError.ReadToEndAsync(Token)).Trim().Split('\n');
            foreach(string line in lines[..^1])Assert.Contains(line.Trim(),new[]{"NativeHost desktop cleanup unavailable.","NativeHost response unavailable."});
            return JsonDocument.Parse(lines[^1]);
        }
        public async ValueTask DisposeAsync(){if(!Process.HasExited){Process.Kill();await Process.WaitForExitAsync();}Process.Dispose();deadline.Dispose();}
    }
    private static byte[] Png()=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"kuaiyi-test.png"));
    private static object Begin(Guid id,byte[] bytes,string? digest=null,int? length=null,string mime="image/png")=>new{type="BeginImage",transferId=id,totalLength=length??bytes.Length,mime,sha256=digest??Convert.ToHexString(SHA256.HashData(bytes))};
    private static async Task Offer(Child child,Guid id)
    {
        using var reply=await child.Exchange(new{type="OfferImage",operationId=id},"Ready");Assert.Equal(id,reply.RootElement.GetProperty("operationId").GetGuid());Assert.Equal(1,reply.RootElement.GetProperty("protocol").GetInt32());
    }
    private static async Task Chunk(Child child,Guid id,byte[] bytes,int index=0)
    {
        using var reply=await child.Exchange(new{type="Chunk",transferId=id,index,data=Convert.ToBase64String(bytes)},"ChunkAccepted");Assert.Equal(index,reply.RootElement.GetProperty("index").GetInt32());
    }
    [Theory][InlineData(1)][InlineData(3)][InlineData(20*1024*1024)]
    public async Task ActualHostLoopOffersChunksCommitsOnlyCompletePng(int length)
    {
        await using var child=new Child("on");Guid id=Guid.NewGuid();byte[] fixture=Png();byte[] bytes=length==1?fixture:length==3?[..fixture,..new byte[ImageTransferAssembler.ChunkLimit*2]]:[..fixture,..new byte[length-fixture.Length]];
        await Offer(child,id);using(await child.Exchange(Begin(id,bytes),"Begun")){}
        int index=0;for(int offset=0;offset<bytes.Length;offset+=ImageTransferAssembler.ChunkLimit)await Chunk(child,id,bytes.AsSpan(offset,Math.Min(ImageTransferAssembler.ChunkLimit,bytes.Length-offset)).ToArray(),index++);
        using var committed=await child.Exchange(new{type="CommitImage",transferId=id},"Committed");Assert.Equal(id,committed.RootElement.GetProperty("operationId").GetGuid());
        using var counts=await child.Finish(0);Assert.Equal(1,counts.RootElement.GetProperty("Submitted").GetInt32());Assert.Equal(bytes.Length,counts.RootElement.GetProperty("Bytes").GetInt32());Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)),counts.RootElement.GetProperty("Digest").GetString());Assert.True(counts.RootElement.GetProperty("Disposed").GetBoolean());
    }
    [Theory][InlineData("off","ActivationRequired")][InlineData("wrong-grant","DesktopUntrusted")]
    public async Task DesktopAuthorityMustGrantBeforeBegin(string mode,string code)
    {
        await using var child=new Child(mode);using var error=await child.Exchange(new{type="OfferImage",operationId=Guid.NewGuid()},"Error");Assert.Equal(code,error.RootElement.GetProperty("code").GetString());
        using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());
    }
    [Theory][InlineData("stale-off")][InlineData("stale-b")][InlineData("locked")]
    public async Task GateInvalidationBeforeSubmissionCannotDeliverOldImage(string mode)
    {
        await using var child=new Child(mode);Guid id=Guid.NewGuid();byte[] bytes=Png();await Offer(child,id);using(await child.Exchange(Begin(id,bytes),"Begun")){}await Chunk(child,id,bytes);
        using var error=await child.Exchange(new{type="CommitImage",transferId=id},"Error");Assert.Equal("Stale",error.RootElement.GetProperty("code").GetString());using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());Assert.Equal(1,counts.RootElement.GetProperty("Cancelled").GetInt32());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancelBeforeOrAfterChunkNeverSubmits(bool chunk)
    {
        await using var child=new Child("on");Guid id=Guid.NewGuid();await Offer(child,id);if(chunk){byte[] bytes=Png();using(await child.Exchange(Begin(id,bytes),"Begun")){}await Chunk(child,id,bytes);}
        using(await child.Exchange(new{type="CancelImage",operationId=id},"Cancelled")){}
        using var counts=await child.Finish(0);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());Assert.Equal(1,counts.RootElement.GetProperty("Cancelled").GetInt32());Assert.True(counts.RootElement.GetProperty("Disposed").GetBoolean());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task EofClearsUnfinishedOperation(bool chunk)
    {
        await using var child=new Child("on");Guid id=Guid.NewGuid();await Offer(child,id);if(chunk){byte[] bytes=Png();using(await child.Exchange(Begin(id,bytes),"Begun")){}await Chunk(child,id,bytes);}
        child.Process.StandardInput.Close();using var error=await child.Read("Error");Assert.Equal("Disconnected",error.RootElement.GetProperty("code").GetString());using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());Assert.Equal(1,counts.RootElement.GetProperty("Cancelled").GetInt32());
    }
    [Fact] public async Task AbsoluteDeadlineEndsIdleConnectionAndDisposesGrant()
    {
        await using var child=new Child("timeout");await Offer(child,Guid.NewGuid());using var error=await child.Read("Error");Assert.Equal("Timeout",error.RootElement.GetProperty("code").GetString());using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());Assert.Equal(1,counts.RootElement.GetProperty("Cancelled").GetInt32());
    }
    [Theory][InlineData("digest")][InlineData("incomplete")][InlineData("order")][InlineData("duplicate")][InlineData("wrong-id")][InlineData("oversize")][InlineData("extra-field")][InlineData("wrong-cancel")][InlineData("wrong-commit")]
    public async Task ActualFrameErrorsAbortWithoutDesktopSubmission(string fault)
    {
        await using var child=new Child("on");Guid id=Guid.NewGuid();byte[] bytes=Png();await Offer(child,id);
        using(await child.Exchange(Begin(id,bytes,fault=="digest"?new string('0',64):null),"Begun")){}
        object frame;
        if(fault is "digest" or "wrong-commit"){await Chunk(child,id,bytes);frame=new{type="CommitImage",transferId=fault=="wrong-commit"?Guid.NewGuid():id};}
        else if(fault=="incomplete")frame=new{type="CommitImage",transferId=id};
        else if(fault=="wrong-cancel")frame=new{type="CancelImage",operationId=Guid.NewGuid()};
        else if(fault=="extra-field")frame=new{type="Chunk",transferId=id,index=0,data=Convert.ToBase64String(bytes),url="https://public-fixture.invalid"};
        else
        {
            if(fault=="duplicate")await Chunk(child,id,bytes);
            frame=new{type="Chunk",transferId=fault=="wrong-id"?Guid.NewGuid():id,index=fault=="order"?1:0,data=Convert.ToBase64String(fault=="oversize"?new byte[ImageTransferAssembler.ChunkLimit+1]:bytes)};
        }
        using var error=await child.Exchange(frame,"Error");Assert.Equal("InvalidMessage",error.RootElement.GetProperty("code").GetString());using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());Assert.Equal(1,counts.RootElement.GetProperty("Cancelled").GetInt32());
    }
    [Theory][InlineData("duplicate-offer")][InlineData("duplicate-begin")][InlineData("no-offer")][InlineData("length")][InlineData("mime")][InlineData("activation-claim")][InlineData("url")]
    public async Task UnsupportedDeclarationCannotObtainPrivilege(string fault)
    {
        await using var child=new Child("on");Guid id=Guid.NewGuid();byte[] bytes=Png();object frame;
        if(fault!="no-offer")await Offer(child,id);
        if(fault=="duplicate-offer")frame=new{type="OfferImage",operationId=id};
        else if(fault=="activation-claim")frame=new{type="BeginImage",transferId=id,totalLength=bytes.Length,mime="image/png",sha256=Convert.ToHexString(SHA256.HashData(bytes)),activation="On"};
        else if(fault=="url")frame=new{type="TranslateUrl",url="https://public-fixture.invalid"};
        else{if(fault=="duplicate-begin")using(await child.Exchange(Begin(id,bytes),"Begun")){}frame=Begin(id,bytes,length:fault=="length"?ImageTransferAssembler.ImageLimit+1:null,mime:fault=="mime"?"image/jpeg":"image/png");}
        using var error=await child.Exchange(frame,"Error");Assert.Equal("InvalidMessage",error.RootElement.GetProperty("code").GetString());using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());
    }
    [Theory][InlineData(0u)][InlineData(uint.MaxValue)][InlineData(262145u)]
    public async Task DeclaredUnsignedLengthRejectedBeforePayloadAllocation(uint length)
    {
        await using var child=new Child("on");await child.Process.StandardInput.BaseStream.WriteAsync(BitConverter.GetBytes(length),child.Token);
        using var error=await child.Read("Error");Assert.Equal("InvalidMessage",error.RootElement.GetProperty("code").GetString());using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Offers").GetInt32());
    }
    [Theory][InlineData("truncated")][InlineData("utf8")][InlineData("duplicate-field")]
    public async Task BinaryStdioMalformedInputIsNotLoggedOrEchoed(string fault)
    {
        await using var child=new Child("on");byte[] payload=fault=="utf8"?[0xff,0xfe]:Encoding.UTF8.GetBytes("{\"type\":\"OfferImage\",\"type\":\"OfferImage\",\"operationId\":\""+Guid.NewGuid()+"\"}");
        await child.Process.StandardInput.BaseStream.WriteAsync(BitConverter.GetBytes((uint)(fault=="truncated"?payload.Length+1:payload.Length)),child.Token);await child.Process.StandardInput.BaseStream.WriteAsync(payload,child.Token);child.Process.StandardInput.Close();
        using var error=await child.Read("Error");Assert.Equal(fault=="truncated"?"Disconnected":"InvalidMessage",error.RootElement.GetProperty("code").GetString());using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());
    }
    [Fact]public async Task PngPixelLimitCheckedAfterMatchingDigestBeforeDelivery()
    {
        await using var child=new Child("on");Guid id=Guid.NewGuid();byte[] bytes=Png();System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16),16385);await Offer(child,id);using(await child.Exchange(Begin(id,bytes),"Begun")){}await Chunk(child,id,bytes);using var error=await child.Exchange(new{type="CommitImage",transferId=id},"Error");Assert.Equal("InvalidImage",error.RootElement.GetProperty("code").GetString());using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());
    }
    [Theory][InlineData("ipc-wrong-connection","DesktopUntrusted")][InlineData("ipc-off-at-begin","Stale")][InlineData("ipc-wrong-begun-id","Stale")][InlineData("ipc-disconnect","Disconnected")]
    public async Task RealInternalPipeRejectsPeerReplyOrLostAuthority(string mode,string code)
    {
        await using var child=new Child(mode);Guid id=Guid.NewGuid();byte[] bytes=Png();JsonDocument error;
        if(mode=="ipc-wrong-connection")error=await child.Exchange(new{type="OfferImage",operationId=id},"Error");
        else{await Offer(child,id);using(await child.Exchange(Begin(id,bytes),"Begun")){}await Chunk(child,id,bytes);error=await child.Exchange(new{type="CommitImage",transferId=id},"Error");}
        using(error){Assert.Equal(code,error.RootElement.GetProperty("code").GetString());}using var counts=await child.Finish(3);Assert.Equal(0,counts.RootElement.GetProperty("Submitted").GetInt32());Assert.True(counts.RootElement.GetProperty("realLocalPipe").GetBoolean());
    }
    [Theory][InlineData("chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/")][InlineData("chrome-extension://*/")][InlineData("chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/?token=fixture")][InlineData("--probe")]
    public async Task ProductionHostNeverUsesFixtureOrUnconfiguredOrigin(string origin)
    {
        await using var child=new Child(origin,true);using var error=await child.Read("Error");Assert.Equal(InstalledHostPolicy.IsOrigin(origin)?OperatingSystem.IsWindows()?"HostNotConfigured":"UnsupportedPlatform":"OriginRejected",error.RootElement.GetProperty("code").GetString());
        child.Process.StandardInput.Close();await child.Process.WaitForExitAsync(child.Token);Assert.Equal(2,child.Process.ExitCode);Assert.Null(await NativeMessagingCodec.ReadAsync(child.Process.StandardOutput.BaseStream,child.Token));Assert.Equal("",await child.Process.StandardError.ReadToEndAsync(child.Token));
    }
}
