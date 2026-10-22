using KuaiYi.Contracts;
using KuaiYi.NativeHost;

// Synthetic child only. Uses the real entry and stdio session; no OS peer impersonation or model sends.
internal static class BrowserHostLifetimeFixture
{
    internal static async Task<int> Run(string selected)
    {
        Console.WriteLine("STARTED");await Console.Out.FlushAsync();
        if(await Console.In.ReadLineAsync()!="ENTER")return 2;
        BrowserHostLifetime lifetime;
        try{lifetime=BrowserHostLifetime.Enter(selected);}
        catch(Exception error){Console.WriteLine("REFUSED|"+error.GetType().Name);return 3;}
        Console.WriteLine("ENTERED");await Console.Out.FlushAsync();
        while(await Console.In.ReadLineAsync() is{} command)
        {
            if(command=="EXIT")return 0;
            if(command=="CRASH")return 23;
            try
            {
                if(command=="CHECK"){lifetime.RequireCurrent();Console.WriteLine("CURRENT");}
                else if(command=="SESSION")
                {
                    // Real BrowserSession EOF completes, while the owning OS process deliberately remains live.
                    using var input=new MemoryStream();using var output=new MemoryStream();
                    int result=await new BrowserSession(new NoDesktop()).RunAsync(input,output);
                    Console.WriteLine("SESSION_RETURNED|"+result);
                }
                else if(command is "OFFER" or "SUBMIT")
                {
                    await using var desktop=new InstalledDesktopChannel(new("",Path.Combine(selected,"KuaiYi.App.exe"),"","PUBLIC_NEVER_CONNECT",new Dictionary<string,string>()),lifetime.RequireCurrent);
                    if(command=="OFFER")await desktop.OfferAsync(Guid.NewGuid(),CancellationToken.None);
                    else await desktop.SubmitAsync(null!,null!,default,CancellationToken.None);
                    throw new InvalidDataException("Unexpected offer succeeded.");
                }
                else throw new InvalidDataException("Unknown fixture command.");
            }
            catch(Exception error){Console.WriteLine("REJECTED|"+error.GetType().Name);}
            await Console.Out.FlushAsync();
        }
        return 0;
    }
    private sealed class NoDesktop:IDesktopImageChannel
    {
        public ValueTask<BrowserGrant> OfferAsync(Guid id,CancellationToken cancellation)=>throw new InvalidDataException("EOF cannot offer.");
        public ValueTask SubmitAsync(BrowserGrant grant,ReceivedImage image,KuaiYi.Imaging.ImageSize size,CancellationToken cancellation)=>throw new InvalidDataException("EOF cannot submit.");
        public ValueTask CancelAsync(BrowserGrant grant,CancellationToken cancellation)=>throw new InvalidDataException("EOF cannot cancel.");
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
