namespace KuaiYi.Core.Settings;
public static class OverlayPositionMapper
{
    public static OverlayPosition Capture(PixelRect anchor,MonitorGeometry monitor)
    {
        PixelRect work=monitor.WorkArea;int margin=monitor.Dpi.CeilingPixels(12);
        double spanX=Math.Max(0,work.Width-2*margin-anchor.Width),spanY=Math.Max(0,work.Height-2*margin-anchor.Height);
        return new(){MonitorId=monitor.Id,X=spanX==0?0:Math.Clamp((anchor.Left-work.Left-margin)/spanX,0,1),Y=spanY==0?0:Math.Clamp((anchor.Top-work.Top-margin)/spanY,0,1)};
    }
    public static PixelRect Restore(OverlayPosition? saved,IReadOnlyList<MonitorGeometry> monitors,string primaryId)
    {
        if(monitors.Count==0)throw new ArgumentException("No available monitors.");
        var monitor=monitors.FirstOrDefault(m=>m.Id==saved?.MonitorId)??monitors.FirstOrDefault(m=>m.Id==primaryId)??monitors[0];
        var work=monitor.WorkArea;int margin=monitor.Dpi.CeilingPixels(12),size=monitor.Dpi.CeilingPixels(48);
        if(work.Width<size+2*margin||work.Height<size+2*margin)throw new ArgumentException("No feasible button work area.");
        double x=saved?.X??1,y=saved?.Y??0.45,dx=saved?.OffsetXDip??-8,dy=saved?.OffsetYDip??0;
        if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(dx)||!double.IsFinite(dy)||x is <0 or >1||y is <0 or >1)throw new SettingsValidationException("lastPosition");
        double left=work.Left+margin+x*(work.Width-2*margin-size)+dx*monitor.Dpi.Factor;
        double top=work.Top+margin+y*(work.Height-2*margin-size)+dy*monitor.Dpi.Factor;
        int px=(int)Math.Round(Math.Clamp(left,work.Left+margin,work.Right-margin-size)),py=(int)Math.Round(Math.Clamp(top,work.Top+margin,work.Bottom-margin-size));
        return new(px,py,px+size,py+size);
    }
}
