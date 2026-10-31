using System.Windows;
using System.Windows.Media;

namespace Akara.Desktop.Controls;

/// <summary>Original vector illustrations; no game artwork is redistributed.</summary>
public sealed class ItemIllustration : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(ItemIllustration), new FrameworkPropertyMetadata("charm", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(nameof(Code), typeof(string), typeof(ItemIllustration), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Code { get => (string)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    static readonly Brush Steel = new LinearGradientBrush(Color.FromRgb(211,216,209), Color.FromRgb(66,80,83), 35);
    static readonly Brush Gold = new LinearGradientBrush(Color.FromRgb(215,185,117), Color.FromRgb(95,66,36), 60);
    static readonly Brush Leather = new LinearGradientBrush(Color.FromRgb(96,75,55), Color.FromRgb(35,34,31), 20);
    static readonly Brush Gem = new RadialGradientBrush(Color.FromRgb(153,233,226), Color.FromRgb(20,85,89));
    static readonly Pen Edge = new(new SolidColorBrush(Color.FromRgb(26,30,30)), 1.2);
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        double side = Math.Min(ActualWidth, ActualHeight), scale = side / 100;
        d.PushTransform(new TranslateTransform((ActualWidth - side) / 2, (ActualHeight - side) / 2));
        d.PushTransform(new ScaleTransform(scale, scale));
        string kind = (Kind ?? "").ToLowerInvariant();
        string code = (Code ?? "").ToLowerInvariant();
        if (kind.Length == 0) kind = Infer(code);
        switch (kind)
        {
            case "weapon": case "sword":
                Shape(d, "M51,8 L60,12 55,59 64,65 58,73 51,69 48,87 39,84 44,66 37,61 42,53 47,55 Z", Steel);
                d.DrawLine(new Pen(Brushes.WhiteSmoke, 1.1), new(53,16), new(49,58));
                Shape(d,"M34,61 L63,69 65,75 32,67 Z",Gold); Shape(d,"M41,72 L49,74 46,88 37,86 Z",Leather); break;
            case "orb": case "staff":
                d.PushTransform(new RotateTransform(16,50,50));
                Shape(d,"M46,37 L53,37 51,91 43,91 Z",Leather);
                d.DrawEllipse(Gold, Edge, new(49,29), 18,18);
                d.DrawEllipse(Gem, new Pen(Brushes.PaleTurquoise,1), new(49,27), 12,12);
                d.DrawEllipse(new SolidColorBrush(Color.FromArgb(160,232,254,244)),null,new(45,23),3,4);
                Shape(d,"M33,39 L44,46 57,44 65,36 60,51 45,54 Z",Gold);
                d.Pop(); break;
            case "shield":
                Shape(d,"M20,17 L50,8 80,17 76,52 Q72,75 50,92 Q28,76 24,52 Z",Steel);
                Shape(d,"M28,23 L50,17 72,23 68,51 Q65,68 50,81 Q35,68 32,51 Z",Leather);
                Shape(d,"M47,26 L54,26 54,45 64,45 64,52 54,52 54,70 47,70 47,52 36,52 36,45 47,45 Z",Gold); break;
            case "helm":
                Shape(d,"M24,48 Q21,17 50,15 Q80,15 77,48 L70,75 58,85 55,57 45,57 42,85 30,75 Z",Steel);
                Shape(d,"M22,42 L43,45 43,51 24,49 Z",Brushes.Black);Shape(d,"M57,45 L78,42 76,49 57,51 Z",Brushes.Black);
                Shape(d,"M48,18 L53,18 56,58 50,69 45,58 Z",Gold); break;
            case "armor":
                Shape(d,"M23,20 L38,13 43,24 57,24 62,13 77,20 88,40 73,49 69,83 31,83 27,49 12,40 Z",Steel);
                Shape(d,"M35,30 L48,36 48,64 35,70 32,47 Z",Leather);Shape(d,"M65,30 L52,36 52,64 65,70 68,47 Z",Leather);
                Shape(d,"M32,72 L68,72 69,82 31,82 Z",Gold); d.DrawLine(new Pen(Brushes.SlateGray,1.3),new(50,27),new(50,70)); break;
            case "gloves":
                Shape(d,"M22,35 L26,16 32,15 33,35 36,11 43,12 43,36 48,14 54,17 52,42 61,28 67,33 59,56 58,82 28,82 25,61 15,44 Z",Steel);
                Shape(d,"M29,69 L59,69 58,83 28,83 Z",Leather); break;
            case "boots":
                Shape(d,"M23,12 L46,12 45,63 51,76 50,87 15,87 13,77 29,64 Z",Steel);
                Shape(d,"M57,18 L78,18 76,64 88,77 87,88 55,88 51,77 62,65 Z",Steel);
                Shape(d,"M21,18 L45,18 44,27 23,27 Z",Gold);Shape(d,"M57,23 L77,23 76,31 59,31 Z",Gold);break;
            case "belt":
                Shape(d,"M10,37 Q50,26 90,37 L88,61 Q50,72 12,61 Z",Leather);
                d.DrawRoundedRectangle(Gold,Edge,new Rect(39,32,24,34),3,3);d.DrawRectangle(Brushes.Black,null,new Rect(45,38,12,22));
                d.DrawLine(new Pen(Gold,3),new(46,49),new(65,49));break;
            case "ring": case "ring1": case "ring2":
                d.DrawEllipse(null,new Pen(Gold,10),new(49,58),23,26);Shape(d,"M31,30 L49,14 69,30 64,49 36,49 Z",Gold);Shape(d,"M38,30 L49,20 61,30 57,42 41,42 Z",Gem);break;
            case "amulet":
                var chain=new Pen(Gold,2.8);d.DrawGeometry(null,chain,Geometry.Parse("M22,13 Q7,38 34,57 M78,13 Q93,38 66,57"));
                Shape(d,"M50,35 L76,57 66,82 50,93 34,82 24,57 Z",Gold);Shape(d,"M50,43 L67,59 60,77 50,84 40,77 33,59 Z",Gem);break;
            case "tome":
                Shape(d,"M24,12 L75,19 75,89 23,80 Z",Leather);Shape(d,"M28,19 L67,24 67,78 28,72 Z",new SolidColorBrush(Color.FromRgb(39,64,76)));
                Shape(d,"M35,42 L48,33 61,43 48,58 Z",Gold);d.DrawLine(new Pen(Gold,2),new(48,34),new(48,67));break;
            case "cube":
                Shape(d,"M15,29 L50,12 85,29 85,72 50,90 15,72 Z",Gold);Shape(d,"M19,32 L49,46 49,84 19,69 Z",Leather);Shape(d,"M52,47 L81,32 81,69 52,83 Z",Leather);
                d.DrawLine(new Pen(Gold,2),new(33,40),new(33,74));d.DrawLine(new Pen(Gold,2),new(66,40),new(66,74));break;
            case "potion":
                Shape(d,"M42,15 L58,15 58,36 Q78,48 75,70 Q74,86 50,89 Q26,86 25,70 Q22,48 42,36 Z",Steel);
                Shape(d,"M29,59 Q50,65 71,59 L70,75 Q50,90 30,75 Z",new SolidColorBrush(Color.FromRgb(102,39,71))); d.DrawRectangle(Gold,Edge,new Rect(40,11,20,8));break;
            case "charm": default:
                Shape(d,"M35,9 L65,9 73,24 69,85 58,93 42,93 31,85 27,24 Z",new LinearGradientBrush(Color.FromRgb(84,114,129),Color.FromRgb(27,49,62),0));
                d.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(127,163,179)),1.3),Geometry.Parse("M38,24 L59,65 M62,25 L39,65 M35,44 L65,44 M43,74 L56,74"));
                d.DrawLine(new Pen(Gold,1),new(37,13),new(62,13));break;
        }
        d.Pop(); d.Pop();
    }
    static void Shape(DrawingContext d,string path,Brush fill) => d.DrawGeometry(fill,Edge,Geometry.Parse(path));
    static string Infer(string code) => code switch { "rin"=>"ring","amu"=>"amulet","box"=>"cube","tbk" or "ibk"=>"tome","rvl"=>"potion","cm1" or "cm2" or "cm3"=>"charm","oba"=>"orb",_=>"charm" };
}
