using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Imaging;
using KuaiYi.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Protocol=KuaiYi.Core.Settings.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class ProductionScreenshotSecretStoreTests
{
    private const string Credential="PUBLIC_SCREENSHOT_FIXTURE_CREDENTIAL";
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback action,object? state)=>action(state);}
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
    // Explicit portable AES fixture; neither DPAPI/ACL proof nor a real user's credential/configuration.
    private sealed class PublicProtector:ISecretProtector
    {
        private readonly byte[] key=Enumerable.Repeat((byte)83,32).ToArray();public int Reads;public readonly List<byte[]> Plain=[];
        public byte[] Protect(ReadOnlySpan<byte> plain){byte[] result=new byte[plain.Length+28];RandomNumberGenerator.Fill(result.AsSpan(0,12));using var cipher=new AesGcm(key,16);cipher.Encrypt(result.AsSpan(0,12),plain,result.AsSpan(28),result.AsSpan(12,16));return result;}
        public byte[] Unprotect(ReadOnlySpan<byte> encrypted){Reads++;byte[] plain=new byte[encrypted.Length-28];using var cipher=new AesGcm(key,16);cipher.Decrypt(encrypted[..12],encrypted[28..],encrypted.Slice(12,16),plain);Plain.Add(plain);return plain;}
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ActualSavedSyntheticSecretIsLeasedSeparatelyAndSentOnlyInBothStagesHeaders(Protocol protocol)
    {
        var requests=new ConcurrentQueue<(string Body,string Authorization,string ApiKey)>();int received=0;
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[]});builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(k=>k.Listen(IPAddress.Loopback,0));await using var app=builder.Build();
        app.MapPost("/vision/{**path}",async(HttpContext context)=>
        {
            using var reader=new StreamReader(context.Request.Body);string body=await reader.ReadToEndAsync(context.RequestAborted);requests.Enqueue((body,context.Request.Headers.Authorization.ToString(),context.Request.Headers["x-api-key"].ToString()));
            string text=Interlocked.Increment(ref received)==1?ImageClassificationTests.Json("diagram_or_mixed"):"public saved-secret screenshot output";
            context.Response.ContentType="application/json";await context.Response.WriteAsync(protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason="stop"}},usage=new{prompt_tokens=9,completion_tokens=4}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason="end_turn",usage=new{input_tokens=9,output_tokens=4}}),context.RequestAborted);
        });await app.StartAsync();var origin=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-production-screenshot-public-fixture-"+Guid.NewGuid().ToString("N"));var protector=new PublicProtector();var coordinator=new SettingsCoordinator(new(Path.Combine(root,"config")),new(Path.Combine(root,"secrets"),protector,new DirectoryPolicy()));
        var gate=new RuntimeGate(requireProfileBinding:true);using var link=new SettingsRuntimeLink(coordinator,gate);using var provider=new ProviderClient();var sourceVersion=Guid.NewGuid();using var image=new OwnedSourceImage(new ScreenshotPixels(new(0,0,8,8),Enumerable.Repeat((byte)255,256).ToArray()));var results=new OverlayResults(gate);var leases=new List<KuaiYi.Providers.SecretLease>();
        await using var flow=new ProductionScreenshotCoordinator(gate,()=>coordinator.Effective,(version,id)=>version==sourceVersion&&id==image.SourceId&&!image.IsDisposed,provider,new Immediate(),results,_=>{},async(id,snapshot,cancellation)=>
        {using var lease=await coordinator.OpenRequestSecretAsync(gate,id,snapshot.Binding,new(snapshot.Transport.ResolveEndpoint()),cancellation);var outgoing=new KuaiYi.Providers.SecretLease(lease.Bytes.Span);leases.Add(outgoing);return outgoing;});
        try
        {
            using(var draft=await coordinator.OpenDraftAsync())
            {draft.Update(s=>s with{ActiveProfileId="text",VisionProfileId="vision",Profiles=[new(){Id="text",BaseUrl="https://text.public.invalid/v1",Model="PUBLIC_TEXT",Authentication=ApiAuthentication.None},new(){Id="vision",Protocol=protocol,BaseUrl=new Uri(origin,"vision/").AbsoluteUri,Model="PUBLIC_VISION",AllowLoopbackHttp=true,Streaming=false,Authentication=protocol==Protocol.OpenAIChat?ApiAuthentication.Bearer:ApiAuthentication.ApiKey,Capabilities=new(){Vision=Capability.Supported}}]});draft.SetKey("vision",Encoding.UTF8.GetBytes(Credential));await coordinator.SaveAsync(draft);}
            Assert.Equal(0,protector.Reads);Assert.DoesNotContain(Credential,await File.ReadAllTextAsync(Path.Combine(root,"config","settings.json")));link.Initialize(coordinator.Effective!);var textOrigin=new EndpointOrigin(new("https://text.public.invalid"));gate.ConsentTo(textOrigin);Assert.True(gate.TryEnable(textOrigin,true));
            var offer=Assert.IsType<ProductionScreenshotOffer>(flow.Prepare(sourceVersion,image));Assert.Equal(0,protector.Reads);Assert.Empty(requests);Assert.True(flow.Submit(offer,true));await flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(2,protector.Reads);Assert.Equal(2,requests.Count);Assert.Equal("public saved-secret screenshot output",results.Text);Assert.True(Assert.Single(results.Versions).Complete);
            Assert.All(requests,r=>{Assert.DoesNotContain(Credential,r.Body);Assert.Equal(protocol==Protocol.OpenAIChat?"Bearer "+Credential:"",r.Authorization);Assert.Equal(protocol==Protocol.AnthropicMessages?Credential:"",r.ApiKey);});Assert.All(leases,l=>Assert.Throws<ObjectDisposedException>(()=>l.HeaderValue()));Assert.All(protector.Plain,b=>Assert.All(b,x=>Assert.Equal(0,x)));
        }
        finally{gate.Disable();await flow.ClearAsync();await app.StopAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
