using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using KuaiYi.App;
using KuaiYi.Core;

namespace KuaiYi.GrowthHarness;

// Native WPF regression for implicit scrollbar styles leaking into embedded code.
// Public generated text only; no input, clipboard, source capture or provider.
internal static class ReaderScrollProbe
{
    internal static int Run(string output)
    {
        if(File.Exists(output))throw new IOException("Existing evidence must not be overwritten.");
        var app=new Application();
        var reader=new RichTextBox{Width=388,Height=300,IsReadOnly=true};
        DesktopScrollChrome.Install(reader);
        var view=new MarkdownFlowView(reader);
        var markdown=new RestrictedMarkdown();Guid version=Guid.NewGuid();
        markdown.Update(version,"```text\n"+new string('x',400)+"\n```",true);
        view.Apply(markdown.Blocks,15,new(388,300),version);
        var window=new Window{Title="快译 · 原生滚动条回归（公开文字）",Content=reader,Width=440,Height=360,ShowInTaskbar=false,ShowActivated=false};
        string? error=null;object[] observed=[];bool closed=false;
        window.Closed+=(_,_)=>closed=true;
        window.Loaded+=async(_,_)=>
        {
            try
            {
                await window.Dispatcher.InvokeAsync(()=>window.UpdateLayout(),DispatcherPriority.ApplicationIdle);
                var bars=Descendants(window).OfType<ScrollBar>().Where(bar=>bar.IsVisible).ToArray();
                foreach(var bar in bars)bar.ApplyTemplate();
                observed=bars.Select(bar=>(object)new{Orientation=bar.Orientation.ToString(),TrackOrientation=(bar.Template.FindName("PART_Track",bar) as Track)?.Orientation.ToString(),bar.ActualWidth,bar.ActualHeight,bar.IsEnabled}).ToArray();
                var horizontal=bars.Where(bar=>bar.Orientation==Orientation.Horizontal&&bar.IsEnabled).ToArray();
                if(horizontal.Length==0)throw new InvalidOperationException("Long native code must expose a working horizontal scrollbar.");
                if(horizontal.Any(bar=>(bar.Template.FindName("PART_Track",bar) as Track)?.Orientation!=Orientation.Horizontal||bar.ActualWidth<=bar.ActualHeight))
                    throw new InvalidOperationException("Reading chrome changed an embedded horizontal scrollbar into a vertical track.");
            }
            catch(Exception failure){error=failure.ToString();}
            finally{view.Clear();window.Close();}
        };
        app.Run(window);
        var result=new{Kind="ActualWpfReaderScrollRegression",Status=error==null&&closed?"Pass":"Fail",Bars=observed,Error=error,ClosedObserved=closed,ModelCalls=0};
        using(var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None))JsonSerializer.Serialize(file,result,new JsonSerializerOptions{WriteIndented=true});
        return error==null&&closed?0:3;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
}
