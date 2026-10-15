// Data-only Linux ports for the linked production view/bridge code. No WPF layout, inheritance,
// selection, routed input or dispatcher behavior is simulated or claimed as tested here.
namespace System.Windows
{
    internal readonly record struct Thickness(double Left,double Top,double Right,double Bottom)
    {public Thickness(double all):this(all,all,all,all){}}
    internal static class FontWeights {public const string Normal="Normal",Bold="Bold",SemiBold="SemiBold";}
    internal static class FontStyles {public const string Normal="Normal",Italic="Italic";}
    internal enum TextMarkerStyle { Decimal, Disc }
    internal enum TextAlignment { Left, Center, Right }
    internal enum TextWrapping { NoWrap, Wrap }
    internal readonly record struct Size(double Width,double Height);
    internal static class SystemParameters {public const double VerticalScrollBarWidth=17;}
    internal class UIElement {public bool ClipToBounds {get;set;}public string? AutomationName {get;set;}}
    internal class FrameworkElement:UIElement
    {
        public double Width {get;set;}=double.NaN;
        public double Height {get;set;}=double.NaN;
        public double MaxWidth {get;set;}=double.PositiveInfinity;
        public double MaxHeight {get;set;}=double.PositiveInfinity;
    }
    internal sealed class TouchEventArgs:EventArgs;
}
namespace System.Windows.Automation
{internal static class AutomationProperties {public static void SetName(UIElement element,string value)=>element.AutomationName=value;}}
namespace System.Windows.Media
{
    internal sealed record FontFamily(string Source);
    internal sealed record Brush(string Name);
    internal static class Brushes {public static readonly Brush Transparent=new("Transparent"),Black=new("Black"),White=new("White");}
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
    internal sealed class BlockUIContainer(UIElement child):Block {public UIElement Child {get;}=child;}
    internal sealed class Paragraph:Block
    {public System.Collections.Generic.List<Inline> Inlines {get;}=[];public TextAlignment TextAlignment {get;set;}}
    internal sealed class Table:Block
    {
        public double CellSpacing {get;set;}
        public System.Collections.Generic.List<TableColumn> Columns {get;}=[];
        public System.Collections.Generic.List<TableRowGroup> RowGroups {get;}=[];
    }
    internal sealed class TableColumn;
    internal sealed class TableRowGroup {public System.Collections.Generic.List<TableRow> Rows {get;}=[];}
    internal sealed class TableRow {public System.Collections.Generic.List<TableCell> Cells {get;}=[];}
    internal sealed class TableCell(Paragraph paragraph)
    {public Paragraph Paragraph {get;}=paragraph;public Thickness Padding {get;set;}}
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
        public double ColumnGap {get;set;}
        public double PageWidth {get;set;}
        public double ColumnWidth {get;set;}
    }
}
namespace System.Windows.Controls
{
    internal enum ScrollBarVisibility { Disabled, Auto, Hidden, Visible }
    internal sealed class RichTextBox:FrameworkElement
    {
        public Documents.FlowDocument Document {get;set;}=new();
        public double FontSize {get;set;}
        public Media.Brush Foreground {get;set;}=Media.Brushes.Black;
        public bool IsDocumentEnabled {get;set;}
        public bool IsReadOnly {get;set;}
        public bool IsUndoEnabled {get;set;}
        public ScrollBarVisibility HorizontalScrollBarVisibility {get;set;}
        public ScrollBarVisibility VerticalScrollBarVisibility {get;set;}
        public Size LastMeasure {get;private set;}
        public Size DesiredSize {get;set;}
        public void Measure(Size size)=>LastMeasure=size;
        public IDisposable DeclareChangeBlock()=>new ChangeBlock();
        private sealed class ChangeBlock:IDisposable {public void Dispose(){}}
    }
}
