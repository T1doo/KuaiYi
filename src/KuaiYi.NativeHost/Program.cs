using KuaiYi.Contracts;
using KuaiYi.NativeHost;
using var input=Console.OpenStandardInput();using var output=Console.OpenStandardOutput();
try
{
    var policy=InstalledHostPolicy.Load(args);
    return await new BrowserSession(new InstalledDesktopChannel(policy)).RunAsync(input,output);
}
catch(Exception error)
{
    var code=error is BrowserException b?b.Code:BrowserFailure.HostNotConfigured;
    await NativeMessagingCodec.WriteAsync(output,new{type="Error",code=code.ToString()});return 2;
}
