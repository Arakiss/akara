using System.Windows;
using System.Windows.Media;

namespace Akara.Desktop.Controls;

internal static class MaterialBrushes
{
    public static DrawingBrush Grain { get; }=CreateGrain();
    static DrawingBrush CreateGrain()
    {
        var drawing=new DrawingGroup();
        using(var context=drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,40,40));
            var random=new Random(37);
            for(int n=0;n<110;n++)
            {
                double x=random.NextDouble()*40,y=random.NextDouble()*40;
                var color=Color.FromArgb((byte)random.Next(3,8),190,179,150);
                context.DrawLine(new Pen(new SolidColorBrush(color),.45),new Point(x,y),new Point(Math.Min(40,x+random.NextDouble()*2.7+.3),y));
            }
        }
        drawing.Freeze();
        var brush=new DrawingBrush(drawing){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,40,40),Stretch=Stretch.None};brush.Freeze();return brush;
    }
}
