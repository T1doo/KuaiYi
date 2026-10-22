using KuaiYi.Core.Installation;
namespace KuaiYi.Launcher;
internal static class Program
{
    // Keep this exact owner and its original tasks alive through this bootstrap's OS exit on any unconfirmed result.
    private static InstallationStableLauncher? runtime;
    public static int Main(string[] args)
    {
        if(args.Length!=0||!OperatingSystem.IsWindows())return 2;
        string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KuaiYi");
        try{return (runtime=new(AppContext.BaseDirectory,data)).RunAsync().GetAwaiter().GetResult();}
        catch(Exception error){Console.Error.WriteLine("Stable launch refused: "+error.GetType().Name);return 3;}
    }
}
