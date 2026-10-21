using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.GrowthHarness;

// Test orchestration only. Product assemblies are the unmodified default build; no network/profile/activation fixture.
internal static class WarmWindowsProbe
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static readonly List<object> errors=[],cases=[];
    private static string output="",phase="Guard",data="";
    private static byte[] marker=[];
    private static object? pipe;
    private static Task? driver;
    private static bool shellClosed,originalCloseJoined,resultClaimed;
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private)!.GetValue(value)!;
    private static object? Invoke(object value,string name,params object?[] args)
    {
        try{return value.GetType().GetMethod(name,Private)!.Invoke(value,args);}
        catch(TargetInvocationException error)when(error.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
    }
    private static void Need(bool value,string code){if(!value){var error=new InvalidOperationException(code);error.Data["ProbeCode"]=code;throw error;}}
    private static object Error(Exception error)=>new{Type=error.GetType().Name,error.HResult,Code=error.Data["ProbeCode"],Win32=error is Win32Exception native?(int?)native.NativeErrorCode:null,Stack=error.StackTrace,Inner=error is AggregateException many?many.InnerExceptions.Select(Error).ToArray():error.InnerException is {} inner?[Error(inner)]:Array.Empty<object>()};
    private static void Phase(string value){phase=value;File.AppendAllText(output+".phases.log",value+Environment.NewLine);}
    private static void Save(string state)
    {
        try{File.WriteAllText(output,JsonSerializer.Serialize(new{Kind="FixedDefaultProgramShellWindowsComponents",Status=state,Phase=phase,Errors=errors,Cases=cases,Pipe=pipe,ShellClosed=shellClosed,OriginalCloseJoined=originalCloseJoined,
        KnownFolderExclusiveClaim=marker.Length>0,FixtureDataRetained=true,ProductHost="GrowthHarness_same_process_default_Program_Main",Pump="ExplicitShutdown_only_after_original_close_join",SdkInstalledByHarness=false,
        Ocr="NotRun",ModelCalls=0,SecurityPolicyChanges=0,IndependentAppExe="NotRun",PositiveWarm="Blocked",Explorer="NotRun",DesktopQualityAndPerformance="NotRun"},new JsonSerializerOptions{WriteIndented=true}));}
        catch(Exception error)
        {
            errors.Add(Error(error));
            Console.Error.WriteLine("KUAIYI_COMPONENT_EVIDENCE_FAILURE "+JsonSerializer.Serialize(new{Status="Fail",Phase=phase,Errors=errors}));
        }
    }
    internal static int Run(string result,string fixtureDirectory)
    {
        if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")!="T1doo/KuaiYi"||Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT")!="1")return 2;
        output=Path.GetFullPath(result);fixtureDirectory=Path.GetFullPath(fixtureDirectory);
        try
        {
            CheckPath(Path.GetDirectoryName(output)!);Need(Directory.Exists(Path.GetDirectoryName(output))&&!File.Exists(output),"FreshResultRequired");
            using(var resultOwner=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read))resultOwner.Flush(true);resultClaimed=true;
            using(var phaseOwner=new FileStream(output+".phases.log",FileMode.CreateNew,FileAccess.Write,FileShare.Read))phaseOwner.Flush(true);
            Need(typeof(ShellWindow).Assembly.GetType("KuaiYi.App.IsolatedSessionLayout")==null,"DefaultAppAssemblyRequired");
            Need(AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar).All(p=>p!="versions"),"UninstalledDevelopmentLayoutRequired");
            string known=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);Need(Path.IsPathFullyQualified(known),"RealKnownFolderRequired");CheckPath(known);
            data=Path.Combine(known,"KuaiYi");CheckPath(data);
            if(!CreateDirectory(data,0))throw new Win32Exception(Marshal.GetLastWin32Error()); // Existing, including empty, is a hard stop.
            marker=RandomNumberGenerator.GetBytes(32);using(var owned=new FileStream(Path.Combine(data,"ci-owner.bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read)){owned.Write(marker);owned.Flush(true);}
            Phase("ExclusiveRealKnownFolderClaimed");pipe=WarmWindowsPipeProbe.Run().GetAwaiter().GetResult();Phase("RealPipeComponentsPassed");
            _=Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>driver=Drive(fixtureDirectory));
            Phase("InvokeFixedDefaultProgram");int code=KuaiYi.App.Program.Main([]);Phase("DefaultProgramReturned");
            Need(driver is {IsCompleted:true},"OriginalDriverNotCompleted");driver!.GetAwaiter().GetResult();
            Need(code==0&&shellClosed&&originalCloseJoined,"OriginalShellExitNotConfirmed");
            CheckPath(data);Need(File.ReadAllBytes(Path.Combine(data,"ci-owner.bin")).AsSpan().SequenceEqual(marker),"OriginalProfileOwnerChanged");
            Save(errors.Count==0?"Pass_ComponentsOnly":"Fail");return errors.Count==0?0:3;
        }
        catch(Exception error){errors.Add(Error(error));if(resultClaimed)Save("Fail");return 3;}
    }
    private static async Task Drive(string fixtures)
    {
        var app=Application.Current;var window=app.MainWindow as ShellWindow;
        try
        {
            Need(window!=null,"DefaultShellMissing");
            // Keep the test pump alive after the original window closes so its ORIGINAL close task can finish.
            app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            window!.Closed+=(_,_)=>shellClosed=true;
            await window.PresentationStartup.WaitAsync(TimeSpan.FromSeconds(15));
            Need(Field<bool>(window,"startupHealthy")&&window.IsLoaded&&window.IsVisible&&new WindowInteropHelper(window).Handle!=0,"DefaultShellStartupUnhealthy");
            Need(window.PresentationGate.Snapshot.Activation==Activation.Off&&!window.PresentationGate.HasRunningGeneration,"DefaultOffRequired");
            Phase("DefaultShellHealthyOff");
            string tiny=Path.Combine(Path.GetDirectoryName(output)!,"synthetic-two-frame.gif");using(var fixture=new FileStream(tiny,FileMode.CreateNew,FileAccess.Write,FileShare.Read))fixture.Write(TinyGif());
            await ImageCase(window,Path.Combine(fixtures,"kuaiyi-test.png"),"png",null);
            await ImageCase(window,tiny,"two-frame-offset-transparent-gif",null);
            await ImageCase(window,Path.Combine(fixtures,"gif-codewidth.gif"),"dictionary-interlaced-gif",File.ReadAllBytes(Path.Combine(fixtures,"gif-codewidth.indices")));
        }
        catch(Exception error){errors.Add(Error(error));}
        finally
        {
            if(window!=null)
            {
                try{Phase("OriginalFinalContentClear");Task<bool> clear=(Task<bool>)Invoke(window,"ClearCurrentContent")!;Need(await clear.WaitAsync(TimeSpan.FromSeconds(15)),"FinalClearFailed");}
                catch(Exception error){errors.Add(Error(error));}
                try
                {
                    Phase("OriginalShellCloseRequested");window.Close();
                    var requests=Field<ShellCloseRequests?>(window,"closeRequests");Need(requests?.Current!=null,"OriginalCloseTaskMissing");
                    await requests!.Current!.WaitAsync(TimeSpan.FromSeconds(15));
                    Need(shellClosed,"OriginalWindowNotClosed");originalCloseJoined=true;
                    Need(Field<Task>(window,"startup").IsCompletedSuccessfully&&Field<Task>(window,"positionSave").IsCompletedSuccessfully,"StartupOrSaveUnsettled");
                    Phase("OriginalCloseAndStartupJoined");Save(errors.Count==0?"ReadyToReturn":"Fail");app.Shutdown(0);
                }
                catch(Exception error){errors.Add(Error(error));Save("Fail_CleanupUnconfirmed");} // Keep original window/pump/owner; outer bounded job reports failure.
            }
            else{Save("Fail_NoShell");app.Shutdown(3);}
        }
    }
    private static async Task ImageCase(ShellWindow window,string path,string name,byte[]? indices)
    {
        Phase("Load_"+name);CheckPath(path);byte[] encoded=File.ReadAllBytes(path);string hash=Convert.ToHexStringLower(SHA256.HashData(encoded));
        FileImageOperation? operation=null;ImageSettlementSnapshot? firstVisible=null;int rendered=0;
        var visible=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ImageWorkerClient Worker()=>Field<ImageWorkerClient>(operation??throw new InvalidOperationException("OriginalOperationMissing"),"worker");
        EventHandler observe=(_,_)=>
        {
            try
            {
                if(operation==null||Field<object?>(window,"retainedImage")==null)return;
                var markdown=Field<MarkdownFlowView>(window,"bodyMarkdown");var region=Field<MarkdownImageSourceRegion?>(markdown,"imageSourceRegion");
                if(region is not {PreviewReady:true}||!region.Image.IsVisible||region.Image.ActualWidth<=0||region.Image.ActualHeight<=0)return;
                firstVisible??=Worker().SettlementSnapshot;
                if(++rendered>=2)visible.TrySetResult();
            }
            catch(Exception error){visible.TrySetException(error);}
        };
        CompositionTarget.Rendering+=observe;
        try
        {
            Func<IFileImageOperation> factory=()=>operation=(FileImageOperation)typeof(ShellWindow).GetMethod("CreateFileImageOperation",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
            Task original=(Task)Invoke(window,"StartOpenImage",path,factory)!;await original.WaitAsync(TimeSpan.FromSeconds(20));
            await visible.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var worker=Worker();var snapshot=worker.SettlementSnapshot;
            Need(firstVisible is {ExitObserved:true,OperationActive:false,PendingOriginalIo:0,PendingCleanup:0,FaultedCleanup:0}&&firstVisible.TotalIo>0,"WorkerNotSettledAtFirstVisibleRender");
            Need(snapshot is {ExitObserved:true,ExitState:HostExitState.ExitConfirmed,PendingOriginalIo:0,PendingCleanup:0,FaultedCleanup:0}&&Field<bool>(operation!,"finalized"),"OriginalWorkerFinalizeUnconfirmed");
            Need(Field<IImagePreparationOperation?>(window,"fileImageOwner")==null&&window.PresentationGate.Snapshot.Activation==Activation.Off&&!window.PresentationGate.HasRunningGeneration,"OwnerOrOffGateMismatch");
            object selected=Field<object>(window,"retainedImage");var image=(OwnedFileImage)selected.GetType().GetProperty("Image")!.GetValue(selected)!;
            var version=(Guid)selected.GetType().GetProperty("VersionId")!.GetValue(selected)!;
            Need(image.Kind==ImageSourceKind.LocalFile&&!image.IsDisposed&&image.Metadata.SourceByteLength==encoded.Length&&image.Metadata.SourceSha256==hash&&!image.Metadata.Transform.HasDesktopAnchor,"ActualSafeFileSourceMismatch");
            var markdown=Field<MarkdownFlowView>(window,"bodyMarkdown");var region=Field<MarkdownImageSourceRegion>(markdown,"imageSourceRegion");
            Need(markdown.IsImagePreviewReady(version,image)&&region.View.IsVisible,"ActualWpfPreviewNotReady");
            var bitmap=(WriteableBitmap)region.Image.Source;byte[] pixels=Field<byte[]>(image,"pixels");var png=Field<ScreenshotPng>(image,"png");
            Need(MemoryMarshal.TryGetArray(png.Bytes,out ArraySegment<byte> pngStorage)&&pngStorage.Array!=null,"OriginalPngStorageMissing");
            byte[] displayed=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(displayed,bitmap.PixelWidth*4,0);
            bool gif=image.Metadata.Format==EncodedRasterFormat.Gif;
            Need(!gif||image.Notice=="使用首帧"&&((string)region.View.Header).Contains("使用首帧",StringComparison.Ordinal)&&VisibleNotice(region.View),"ActualVisibleFirstFrameLabelMissing");
            if(name=="two-frame-offset-transparent-gif")
            {
                Need(image.Metadata.GifInfo==new GifFirstFrameInfo(2,1,1,2,1)&&bitmap.PixelWidth==4&&bitmap.PixelHeight==3,"LogicalCanvasMetadataMismatch");
                byte[] expected=Enumerable.Repeat((byte)255,48).ToArray();expected[6*4+1]=expected[6*4+2]=0;
                Need(displayed.AsSpan().SequenceEqual(expected),"FirstFrameOffsetTransparencyOrLaterFrameMismatch");
            }
            if(indices!=null)
            {
                Need(bitmap.PixelWidth==256&&bitmap.PixelHeight==128&&indices.Length==32768,"DictionaryFixtureDimensions");
                for(int i=0;i<indices.Length;i++){byte n=indices[i];Need(displayed[i*4]==n*37%256&&displayed[i*4+1]==255-n&&displayed[i*4+2]==n&&displayed[i*4+3]==255,"IndependentPaletteOracleMismatch");}
            }
            Task<bool> clear=(Task<bool>)Invoke(window,"ClearCurrentContent")!;Need(await clear.WaitAsync(TimeSpan.FromSeconds(15)),"OriginalClearRejected");
            bitmap.CopyPixels(displayed,bitmap.PixelWidth*4,0);
            Need(image.IsDisposed&&pixels.All(b=>b==0)&&pngStorage.Array!.AsSpan(pngStorage.Offset,pngStorage.Count).IndexOfAnyExcept((byte)0)<0&&displayed.All(b=>b==0),"OriginalManagedOrWpfBytesNotZero");
            Need(region.Image.Source==null&&Field<object?>(window,"retainedImage")==null&&Field<IImagePreparationOperation?>(window,"fileImageOwner")==null,"OriginalSourceOwnerNotCleared");
            Need(window.PresentationGate.Snapshot.Activation==Activation.Off&&!window.PresentationGate.HasRunningGeneration,"ClearEnabledOrSent");
            cases.Add(new{Name=name,OriginalFileTask=original.Status.ToString(),OriginalSourceVersion=version,SourceHash=hash,Width=image.Width,Height=image.Height,
                Gif=image.Metadata.GifInfo,VisibleFirstFrameNotice=gif,ActualVisibleFrames=rendered,WorkerAtFirstVisibleRender=firstVisible,WorkerAfterOriginalTask=snapshot,
                OriginalWorkerFinalized=true,OriginalManagedPixelsZero=true,OriginalPngZero=true,DisplayedWriteableBitmapZero=true,OriginalClearJoined=clear.IsCompletedSuccessfully,Activation="Off"});
            Phase("Cleared_"+name);
        }
        finally{CompositionTarget.Rendering-=observe;}
    }
    private static bool VisibleNotice(DependencyObject node)
    {
        if(node is FrameworkElement {IsVisible:true,ActualHeight:>0}&&((node is TextBlock text&&text.Text.Contains("使用首帧",StringComparison.Ordinal))||(node is AccessText access&&access.Text.Contains("使用首帧",StringComparison.Ordinal))))return true;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(VisibleNotice(VisualTreeHelper.GetChild(node,i)))return true;return false;
    }
    private static byte[] TinyGif()
    {
        byte[] Frame(int left,int top,int width,int height,byte[] values,bool transparent)
        {
            var codes=new List<byte>();int bits=0,value=0;
            void Code(int n){value|=n<<bits;bits+=3;while(bits>=8){codes.Add((byte)value);value>>=8;bits-=8;}}
            foreach(byte n in values){Code(4);Code(n);}Code(5);if(bits>0)codes.Add((byte)value);
            return [..(transparent?new byte[]{0x21,0xf9,4,1,0,0,0,0}:[]),0x2c,(byte)left,0,(byte)top,0,(byte)width,0,(byte)height,0,0,2,(byte)codes.Count,..codes,0];
        }
        return [.."GIF89a"u8.ToArray(),4,0,3,0,0x81,3,0,255,0,0,0,255,0,0,0,255,255,255,255,
            ..Frame(1,1,2,1,[0,2],true),..Frame(0,0,4,3,Enumerable.Repeat((byte)1,12).ToArray(),false),0x3b];
    }
    private static void CheckPath(string path)
    {
        Need(Path.IsPathFullyQualified(path)&&!path.StartsWith("\\\\",StringComparison.Ordinal),"LocalAbsolutePathRequired");
        for(string? current=Path.GetFullPath(path);current!=null;current=Path.GetDirectoryName(current))
            try{Need((File.GetAttributes(current)&FileAttributes.ReparsePoint)==0,"ReparsePointRefused");}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
    }
    [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CreateDirectory(string path,nint security);
}
