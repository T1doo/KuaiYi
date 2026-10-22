using KuaiYi.Core.Installation;
namespace KuaiYi.Launcher;
internal static class Program
{
    // Retain exact original owners through OS exit on an unconfirmed result; never adopt or kill its UI child.
    private static InstallationStableLauncher? runtime;
    private static InstallationBootstrapEntry? bootstrap;
    public static int Main(string[] args)
    {
        if(args.Length!=0||!OperatingSystem.IsWindows())return 2;
        string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KuaiYi");List<Exception> errors=[];int result=3;
        try
        {
            bootstrap=InstallationBootstrapEntry.Open(AppContext.BaseDirectory,data);
            runtime=new(bootstrap.Root,data){Bootstrap=bootstrap.Binding};result=runtime.RunAsync().GetAwaiter().GetResult();
        }
        catch(Exception error){errors.Add(error);}
        if(runtime==null||runtime.CleanupConfirmed)try{bootstrap?.Dispose();}catch(Exception error){errors.Add(error);}
        foreach(var error in errors)Console.Error.WriteLine("Stable launch refused: "+error.GetType().Name);
        return errors.Count==0?result:3;
    }
}
