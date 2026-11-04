using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using KuaiYi.App;
using KuaiYi.Core.Settings;

namespace KuaiYi.GrowthHarness;

internal static class AppearanceGeometryProbe
{
    internal static int Run(string output)
    {
        var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        int count=0;
        foreach(var style in Enum.GetValues<OverlayOutline>())
        foreach(double width in new[]{48d,100,320,420,600})
        foreach(double height in new[]{48d,120,360,640})
        foreach(double scale in new[]{1d,1.25,1.5,2})
        foreach(bool right in new[]{false,true})
        foreach(bool bottom in new[]{false,true})
        {
            var anchor=new Rect(right?width-48:0,bottom?height-48:0,48,48);
            var geometry=OverlayOutlineGeometry.Create(width,height,18,anchor,style,1);
            if(!geometry.FillContains(new Point(anchor.X+24,anchor.Y+24)))throw new InvalidOperationException("Anchor center missing.");
            if(width>=100&&height>=120&&!geometry.FillContains(new RectangleGeometry(new Rect(20,20,width-40,height-40))))throw new InvalidOperationException("Reading safe area clipped.");
            foreach(var polygon in OverlayOutlineGeometry.Pixels(geometry,scale))
                foreach(var p in polygon)
                    if(p.X<0||p.Y<0||p.X>Math.Ceiling(width*scale)||p.Y>Math.Ceiling(height*scale))throw new InvalidOperationException("Outline outside window.");
            count++;
        }
        var faces=DesktopTypography.Modern.FamilyMaps.Select(map=>new Typeface(new FontFamily(map.Target.Split(',')[0]),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal)).ToArray();
        var fonts=new List<string>();
        for(int i=0;i<faces.Length;i++)
        {
            if(!faces[i].TryGetGlyphTypeface(out var glyph)||!glyph.CharacterToGlyphMap.ContainsKey(i==0?'A':'译'))throw new InvalidOperationException("Reading font unavailable.");
            fonts.Add(glyph.FontUri.ToString());
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{status="Pass",geometryCases=count,fontUris=fonts,scope="WPF geometry and installed reading font resolution; not desktop interaction acceptance"},new JsonSerializerOptions{WriteIndented=true}));
        return 0;
    }
}
