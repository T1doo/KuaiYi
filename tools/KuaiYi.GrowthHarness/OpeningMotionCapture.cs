using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Reflection;
using KuaiYi.App;
using KuaiYi.Core;

namespace KuaiYi.GrowthHarness;

// Opt-in public-fixture WPF pixel samples. This is not a DWM screen recording or
// a performance pass. Frames are never interpolated or synthesized from geometry.
internal sealed class OpeningMotionCapture
{
    private readonly string directory;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly List<(RenderTargetBitmap Bitmap,double Ms,PixelRect Card,PixelRect Anchor,double Corner,double Opacity)> frames=[];
    private double last=-1000;private long pixels;private bool capturing,done;
    private RichTextBox? body;
    private List<Button>? tools;
    internal OpeningMotionCapture(string prefix)
    {
        directory=prefix+".motion";
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Existing motion evidence must not be overwritten.");
    }
    internal void Observe(ShellWindow window,bool validNativeFrame,bool hasText)
    {
        if(done||capturing||!validNativeFrame||window.PresentationFrameApplying||window.PresentationGeometry is not {} geometry)return;
        double now=clock.Elapsed.TotalMilliseconds;
        body??=(RichTextBox)typeof(ShellWindow).GetField("body",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        tools??=(List<Button>)typeof(ShellWindow).GetField("tools",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        bool controlsSettled=!tools.Single(tool=>Equals(tool.Tag,"停止")).IsVisible;
        bool terminal=hasText&&controlsSettled&&!window.PresentationGate.HasRunningGeneration&&!window.PresentationTextRevealPending&&body.IsVisible&&body.Opacity>=.999&&geometry.Transition==OverlayTransition.Expanded&&!geometry.NeedsFrame;
        double interval=geometry.Transition==OverlayTransition.Opening?33:120;
        if(!terminal&&now-last<interval)return;
        var card=geometry.Current;long area=(long)card.Width*card.Height;
        if(frames.Count>=64||area<=0||area>1_000_000||pixels+area>16_000_000)return;
        capturing=true;
        try
        {
            var bitmap=new RenderTargetBitmap(card.Width,card.Height,96*geometry.Monitor.Dpi.Factor,96*geometry.Monitor.Dpi.Factor,PixelFormats.Pbgra32);
            bitmap.Render((Visual)window.Content);bitmap.Freeze();
            frames.Add((bitmap,now,card,geometry.Anchor,geometry.CornerProgress,geometry.ContentOpacity));
            pixels+=area;last=now;done=terminal;
        }
        finally{capturing=false;}
    }
    internal void Save()
    {
        Directory.CreateDirectory(directory);var records=new List<object>();
        for(int i=0;i<frames.Count;i++)
        {
            var frame=frames[i];string name=$"frame-{i:000}.png";var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(frame.Bitmap));
            using(var file=new FileStream(Path.Combine(directory,name),FileMode.CreateNew,FileAccess.Write,FileShare.None))encoder.Save(file);
            records.Add(new{File=name,Milliseconds=frame.Ms,Card=frame.Card,Anchor=frame.Anchor,CornerProgress=frame.Corner,ContentOpacity=frame.Opacity});
        }
        using(var file=new FileStream(Path.Combine(directory,"frames.json"),FileMode.CreateNew,FileAccess.Write,FileShare.None))
            JsonSerializer.Serialize(file,new{Kind="ActualWpfContentFrameSamples_NotDwmVideo",TerminalCaptured=done,PixelBudget=16_000_000,Pixels=pixels,Frames=records},new JsonSerializerOptions{WriteIndented=true});
        frames.Clear();
    }
}
