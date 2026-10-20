using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    // Called only after the original receiver/lease/current-ticket UI guard, shared with managed regressions.
    private void ApplyDropFeedback(string phase,uint effect,OleTextFormat format)
    {
        if(phase=="Hover"){dropHint.Text=format==OleTextFormat.ImageFile?"松开\n准备图片":"松开\n翻译";SetDropHover(true);}
        else if(phase is "Leave" or "DropBegin" or "DropEnd")SetDropHover(false);
        if(phase=="QueryEnd"&&effect==0)button.ToolTip="此投放未接收；仅支持可复制文字或明确开启后的单个本地图片文件，多文件、位图、虚拟文件和链接不支持。";
        else if(phase=="DropEnd"&&effect==0&&format==OleTextFormat.ImageFile)button.ToolTip="未接收图片投放；仅支持单个本地 PNG/JPEG/WebP/BMP/GIF 文件，多文件、虚拟文件、链接及不支持格式已拒绝，旧来源保留。";
    }
}
