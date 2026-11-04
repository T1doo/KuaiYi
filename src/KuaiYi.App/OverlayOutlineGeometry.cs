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
        double inset=Math.Min(14*progress,Math.Min(width,height)/8);
        Geometry shape;
        if(style==OverlayOutline.Cloud)
        {
            // Visible cloud shoulders; the central reading rectangle remains intact.
            shape=new RectangleGeometry(new Rect(inset,inset,width-2*inset,height-2*inset),radius,radius);
            double span=Math.Max(1,(width-2*radius)/3);
            for(int i=0;i<3;i++)
            {
                double x=radius+span*(i+.5),rx=span*.53;
                shape=Geometry.Combine(shape,new EllipseGeometry(new Point(x,inset*2),rx,inset*2),GeometryCombineMode.Union,null);
                shape=Geometry.Combine(shape,new EllipseGeometry(new Point(x,height-inset*2),rx,inset*2),GeometryCombineMode.Union,null);
            }
            if(height>96)
                for(int i=0;i<3;i++)
                {
                    double y=radius+(height-2*radius)*(i+.5)/3,ry=(height-2*radius)*.19;
                    shape=Geometry.Combine(shape,new EllipseGeometry(new Point(inset*2,y),inset*2,ry),GeometryCombineMode.Union,null);
                    shape=Geometry.Combine(shape,new EllipseGeometry(new Point(width-inset*2,y),inset*2,ry),GeometryCombineMode.Union,null);
                }
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
