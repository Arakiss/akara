using System.Windows;
using System.Windows.Media;

namespace Akara.Desktop.Controls;

public static class MaterialBrushes
{
    public static DrawingBrush Grain { get; }=CreateGrain();
    static DrawingBrush CreateGrain()
    {
        var drawing=new DrawingGroup();
        using(var context=drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,128,128));
            var random=new Random(37);
            for(int n=0;n<900;n++)
            {
                double x=random.NextDouble()*128,y=random.NextDouble()*128;
                var color=Color.FromArgb((byte)random.Next(2,6),190,179,150);
                context.DrawLine(new Pen(new SolidColorBrush(color),.4),new Point(x,y),new Point(Math.Min(128,x+random.NextDouble()*1.2+.3),y));
            }
        }
        drawing.Freeze();
        var brush=new DrawingBrush(drawing){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,128,128),Stretch=Stretch.None};brush.Freeze();return brush;
    }
}
