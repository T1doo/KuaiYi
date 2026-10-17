using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
namespace KuaiYi.Provider.Tests;
/// <summary>Test-only actual Kestrel socket; acks follow real response flushes. No mocked handler or model.</summary>
public sealed class PhaseHttpFixture:IAsyncLifetime
{
    public sealed class Script
    {
        public sealed record Response(int StatusCode,string? RetryAfter=null);
        public Func<int,Response>? ResponseForRequest;
        internal Channel<(byte[] Bytes,bool End,TaskCompletionSource Ack)> Chunks=Channel.CreateUnbounded<(byte[],bool,TaskCompletionSource)>(new(){SingleReader=true});
        public TaskCompletionSource Received=new(TaskCreationOptions.RunContinuationsAsynchronously),AllowHeaders=new(TaskCreationOptions.RunContinuationsAsynchronously),HeadersSent=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Mime="text/event-stream",Body="";public int Requests;public int StatusCode=200;
        public ConcurrentQueue<string> Bodies {get;}=new();
        public async Task Write(string value,bool end=false)=>await Write(Encoding.UTF8.GetBytes(value),end);
        public async Task Write(byte[] value,bool end=false){var ack=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);await Chunks.Writer.WriteAsync((value,end,ack));await ack.Task.WaitAsync(TimeSpan.FromSeconds(6));}
    }
    private WebApplication app=null!;private readonly ConcurrentDictionary<string,Script> scripts=[];public Uri BaseUri{get;private set;}=null!;
    public Script Add(string run,string mime="text/event-stream",bool headers=true){var script=new Script{Mime=mime};if(headers)script.AllowHeaders.TrySetResult();if(!scripts.TryAdd(run,script))throw new InvalidOperationException("Duplicate public script.");return script;}
    public async Task InitializeAsync()
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[]});builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(k=>{k.Listen(IPAddress.Loopback,0);k.Limits.MaxRequestBodySize=1024*1024;});app=builder.Build();
        app.MapPost("/{run}/{**endpoint}",async(HttpContext context,string run)=>
        {
            if(!scripts.TryGetValue(run,out var script)){context.Response.StatusCode=404;return;}
            try
            {
                using var reader=new StreamReader(context.Request.Body,new UTF8Encoding(false,true));script.Body=await reader.ReadToEndAsync(context.RequestAborted);script.Bodies.Enqueue(script.Body);int request=Interlocked.Increment(ref script.Requests);script.Received.TrySetResult();await script.AllowHeaders.Task.WaitAsync(context.RequestAborted);var response=script.ResponseForRequest?.Invoke(request)??new(script.StatusCode);context.Response.StatusCode=response.StatusCode;context.Response.ContentType=script.Mime;if(response.RetryAfter is {} retryAfter)context.Response.Headers["Retry-After"]=retryAfter;await context.Response.StartAsync(context.RequestAborted);await context.Response.Body.FlushAsync(context.RequestAborted);script.HeadersSent.TrySetResult();if(response.StatusCode!=200)return;
                await foreach(var chunk in script.Chunks.Reader.ReadAllAsync(context.RequestAborted))
                {await context.Response.Body.WriteAsync(chunk.Bytes,context.RequestAborted);await context.Response.Body.FlushAsync(context.RequestAborted);chunk.Ack.TrySetResult();if(chunk.End)break;}
            }
            catch(OperationCanceledException)when(context.RequestAborted.IsCancellationRequested){ /* Real client deliberately cancelled this test-only socket. */ }
        });await app.StartAsync();BaseUri=new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
    }
    public async Task DisposeAsync(){foreach(var script in scripts.Values){script.AllowHeaders.TrySetResult();script.Chunks.Writer.TryComplete();}await app.StopAsync();await app.DisposeAsync();}
}
