using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if(args.SequenceEqual(new[]{"--diagnose"})){Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(NativeOcr.Diagnose(),ImageJson.Options));return 0;}
        if(!args.SequenceEqual(new[]{"--private-stdio"})){Console.Error.WriteLine("ImageWorker: private inherited stdio or --diagnose only.");return 2;}
        await using var input=Console.OpenStandardInput();await using var output=Console.OpenStandardOutput();
        try{await ImageWorkerSession.RunAsync(input,output,NativeOcr.Recognize,type=>Console.Error.WriteLine("ImageWorker OCR: "+type));return 0;}
        catch(Exception error){Console.Error.WriteLine("ImageWorker: "+error.GetType().Name);return 3;}
    }
}
