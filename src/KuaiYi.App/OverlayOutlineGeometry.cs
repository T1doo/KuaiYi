using System.Windows;
using System.Windows.Media;
using KuaiYi.Core.Settings;
using KuaiYi.Windows;

namespace KuaiYi.App;

internal static class OverlayOutlineGeometry
{
    internal static Geometry Create(double width,double height,double radius,Rect anchor,OverlayOutline style,double progress)
    {
        var bounds=new Rect(0,0,width,height);
        if(style==OverlayOutline.Glass||progress<=0)return new RectangleGeometry(bounds,radius,radius);
        Geometry shape;
        if(style==OverlayOutline.Cloud)
        {
            // Fixed C1-continuous cubic topology: unequal shoulders, quiet sides,
            // one broad lower arc. No height-dependent addition of lobes.
            double r=Math.Min(radius+8*progress,Math.Min(width,height)*.24);
            double d=progress,tx=Math.Min(width*.10,44),ty=Math.Min(height*.12,30);
            (Point P,Vector T)[] nodes=
            [
                (new(r,6*d),new(r*.55,0)),
                (new(width*.35,2*d),new(tx,0)),
                (new(width*.62,7*d),new(tx,0)),
                (new(width-r,3*d),new(r*.55,0)),
                (new(width-3*d,r),new(0,r*.55)),
                (new(width-7*d,height*.52),new(0,ty)),
                (new(width-4*d,height-r),new(0,r*.55)),
                (new(width-r,height-4*d),new(-r*.55,0)),
                (new(width*.45,height-7*d),new(-tx,0)),
                (new(r,height-4*d),new(-r*.55,0)),
                (new(3*d,height-r),new(0,-r*.55)),
                (new(7*d,height*.45),new(0,-ty)),
                (new(3*d,r),new(0,-r*.55))
            ];
            var cloud=new StreamGeometry();
            using(var context=cloud.Open())
            {
                context.BeginFigure(nodes[0].P,true,true);
                for(int i=0;i<nodes.Length;i++)
                {
                    var first=nodes[i];var next=nodes[(i+1)%nodes.Length];
                    context.BezierTo(first.P+first.T,next.P-next.T,next.P,true,true);
                }
            }
            shape=cloud;
        }
        else
        {
            double r=style==OverlayOutline.Bubble?radius+10*progress:radius;
            double a=Math.Min(r,Math.Min(width,height)/2),b=style==OverlayOutline.Bubble?a:Math.Min(r+12*progress,Math.Min(width,height)/2);
            var path=new StreamGeometry();
            using(var c=path.Open())
            {
                c.BeginFigure(new Point(a,0),true,true);c.LineTo(new Point(width-b,0),true,false);
                c.ArcTo(new Point(width,b),new Size(b,b),0,false,SweepDirection.Clockwise,true,false);
                c.LineTo(new Point(width,height-a),true,false);c.ArcTo(new Point(width-a,height),new Size(a,a),0,false,SweepDirection.Clockwise,true,false);
                c.LineTo(new Point(b,height),true,false);c.ArcTo(new Point(0,height-b),new Size(b,b),0,false,SweepDirection.Clockwise,true,false);
                c.LineTo(new Point(0,a),true,false);c.ArcTo(new Point(a,0),new Size(a,a),0,false,SweepDirection.Clockwise,true,false);
            }
            shape=path;
        }
        shape=Geometry.Combine(shape,new EllipseGeometry(anchor),GeometryCombineMode.Union,null);
        shape=Geometry.Combine(shape,new RectangleGeometry(bounds),GeometryCombineMode.Intersect,null);
        shape.Freeze();return shape;
    }

    internal static NativeDesktop.Point[][] Pixels(Geometry geometry,double scale)
    {
        var flattened=geometry.GetFlattenedPathGeometry(.25/scale,ToleranceType.Absolute);
        return flattened.Figures.Select(figure=>
        {
            var points=new List<Point>{figure.StartPoint};
            foreach(var segment in figure.Segments)
                if(segment is PolyLineSegment poly)points.AddRange(poly.Points);
                else if(segment is LineSegment line)points.Add(line.Point);
            return points.Select(p=>new NativeDesktop.Point{X=(int)Math.Round(p.X*scale),Y=(int)Math.Round(p.Y*scale)}).ToArray();
        }).Where(points=>points.Length>=3).ToArray();
    }
}
