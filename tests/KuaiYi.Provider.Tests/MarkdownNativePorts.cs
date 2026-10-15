// Data-only Linux ports for the linked production view/bridge code. No WPF layout, inheritance,
// selection, routed input or dispatcher behavior is simulated or claimed as tested here.
namespace System.Windows
{
    internal readonly record struct Thickness(double Left,double Top,double Right,double Bottom)
    {public Thickness(double all):this(all,all,all,all){}}
    internal static class FontWeights {public const string Normal="Normal",Bold="Bold",SemiBold="SemiBold";}
    internal static class FontStyles {public const string Normal="Normal",Italic="Italic";}
    internal enum TextMarkerStyle { Decimal, Disc }
}
namespace System.Windows.Media
{
    internal sealed record FontFamily(string Source);
}
namespace System.Windows.Documents
{
    internal abstract class TextElement
    {
        public double FontSize {get;set;}
        public string? FontWeight {get;set;}
        public string? FontStyle {get;set;}
        public Media.FontFamily? FontFamily {get;set;}
    }
    internal abstract class Inline:TextElement;
    internal sealed class Run(string text):Inline {public string Text {get;}=text;}
    internal sealed class Span(Run run):Inline {public System.Collections.Generic.List<Inline> Inlines {get;}=[run];}
    internal abstract class Block:TextElement
    {
        public double LineHeight {get;set;}
        public Thickness Margin {get;set;}
    }
    internal sealed class Paragraph:Block {public System.Collections.Generic.List<Inline> Inlines {get;}=[];}
    internal sealed class ListItem(Paragraph paragraph) {public Paragraph Paragraph {get;}=paragraph;}
    internal sealed class List(ListItem item):Block
    {
        public ListItem Item {get;}=item;
        public TextMarkerStyle MarkerStyle {get;set;}
        public int StartIndex {get;set;}
        public Thickness Padding {get;set;}
    }
    internal sealed class FlowDocument
    {
        public System.Collections.Generic.List<Block> Blocks {get;}=[];
        public Thickness PagePadding {get;set;}
        public double FontSize {get;set;}
    }
}
namespace System.Windows.Controls
{
    internal sealed class RichTextBox
    {
        public Documents.FlowDocument Document {get;set;}=new();
        public double FontSize {get;set;}
        public IDisposable DeclareChangeBlock()=>new ChangeBlock();
        private sealed class ChangeBlock:IDisposable {public void Dispose(){}}
    }
}
