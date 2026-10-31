using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Controls;

public static class ItemArt
{
    static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.Ordinal);
    static BitmapSource? atlas;
    static Dictionary<string, Int32Rect>? crops;
    public static ImageSource Get(ItemViewModel item) => Get(Category(item));
    public static ImageSource Get(string key)
    {
        key=key switch{"purpleArmor"=>"mageArmor","steelArmor"=>"plateArmor","demonMask"=>"mask","shield"=>"kiteShield","bookBlue"=>"blueBook","bookGold"=>"brownBook",_=>key};
        if (Cache.TryGetValue(key, out var image)) return image;
        if(key=="unknown")
        {
            // An unrecognised base gets an explicit neutral marker, never a
            // fabricated charm based only on its inventory dimensions.
            var drawing=new DrawingGroup();
            using(var context=drawing.Open())
            {
                context.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,120,160));
                var line=new Pen(new SolidColorBrush(Color.FromRgb(128,123,103)),1.5);
                context.DrawGeometry(new SolidColorBrush(Color.FromRgb(26,31,30)),line,Geometry.Parse("M60,27 L98,51 98,107 60,132 22,107 22,51 Z"));
                context.DrawGeometry(null,line,Geometry.Parse("M49,60 C48,45 76,45 75,62 C75,72 60,72 60,85"));
                context.DrawEllipse(line.Brush,null,new Point(60,97),1.8,1.8);
            }
            drawing.Freeze();var marker=new DrawingImage(drawing);marker.Freeze();Cache[key]=marker;return marker;
        }
        LoadAtlas();
        if (atlas is not null && crops?.TryGetValue(key, out var rectangle) == true)
        {
            var crop = new CroppedBitmap(atlas, rectangle); crop.Freeze(); Cache[key] = crop; return crop;
        }
        var illustration = new ItemIllustration { Kind = Fallback(key), Width = 120, Height = 160 };
        illustration.Measure(new Size(120,160)); illustration.Arrange(new Rect(0,0,120,160)); illustration.UpdateLayout();
        var bitmap = new RenderTargetBitmap(120,160,96,96,PixelFormats.Pbgra32); bitmap.Render(illustration); bitmap.Freeze(); Cache[key]=bitmap; return bitmap;
    }
    static void LoadAtlas()
    {
        if (crops is not null) return;
        crops = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var manifest = Application.GetResourceStream(new Uri("Assets/Art/items-atlas.json",UriKind.Relative));
            if (manifest is null) return;
            using var document = JsonDocument.Parse(manifest.Stream);
            string file = document.RootElement.GetProperty("atlas").GetString()!;
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.UriSource = new Uri("pack://application:,,,/Akara;component/Assets/Art/"+file); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze(); atlas=bitmap;
            foreach (var entry in document.RootElement.GetProperty("items").EnumerateObject())
            {
                var r=entry.Value;
                var rectangle=new Int32Rect(r.GetProperty("x").GetInt32(),r.GetProperty("y").GetInt32(),r.GetProperty("width").GetInt32(),r.GetProperty("height").GetInt32());
                if(rectangle.Width>0&&rectangle.Height>0&&rectangle.X>=0&&rectangle.Y>=0&&rectangle.X+rectangle.Width<=bitmap.PixelWidth&&rectangle.Y+rectangle.Height<=bitmap.PixelHeight)crops[entry.Name]=rectangle;
            }
        }
        catch (Exception exception) when(exception is IOException or JsonException or ArgumentException or InvalidOperationException) { atlas=null; }
    }
    public static string Category(ItemViewModel item)
    {
        // Kind contains the occupied slot for equipped items. Read the native
        // base type instead, so moving the same piece never changes its art.
        return Category(item.Code,item.Data.Text("itemType"),item.Name,
            item.Data.Array("equipSlots").Select(value=>value?.ToString()??""));
    }
    internal static string Category(string code,string nativeType,string name,IEnumerable<string> equipSlots)
    {
        code=code.ToLowerInvariant();nativeType=nativeType.ToLowerInvariant();name=name.ToLowerInvariant();
        var slots=equipSlots.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(code is "tbk")return "bookBlue";if(code is "ibk")return "bookGold";if(code is "box" or "bag")return "cube";
        if(code is "cm3"||nativeType=="lcha")return "grandCharm";
        if(code is "cm1" or "cm2"||nativeType is "char" or "scha" or "mcha")return "smallCharm";
        if(code is "rvl" or "rvs"||nativeType is "hpot" or "mpot" or "rpot" or "apot" or "wpot" or "vpot")return "potion";
        if(code=="rin"||nativeType=="ring")return "ring";if(code=="amu"||nativeType=="amul")return "amulet";
        if(code.StartsWith("ob",StringComparison.Ordinal)||nativeType is "orb" or "staf" or "wand" or "scep")return "orbStaff";
        if(nativeType is "jave" or "ajav" or "spea" or "aspe")return "javelin";
        if(nativeType=="pole")return "scythe";
        if(nativeType is "bow" or "abow" or "xbow")return "bow";
        if(nativeType is "axe" or "taxe")return "axe";
        if(nativeType is "swor" or "knif" or "tkni" or "h2h" or "h2h2" or "club" or "mace" or "hamm" or "blun" or "weap")return "sword";
        if(slots.Contains("weapon"))
        {
            if(name.Contains("oculus",StringComparison.Ordinal)||name.Contains("fathom",StringComparison.Ordinal)||name.Contains("eschuta",StringComparison.Ordinal))return "orbStaff";
            if(name.Contains("titan",StringComparison.Ordinal)||name.Contains("thunderstroke",StringComparison.Ordinal))return "javelin";
            if(name.Contains("reaper",StringComparison.Ordinal))return "scythe";
            return "sword";
        }
        if(nativeType is "helm" or "circ" or "phlm" or "pelt"||slots.Contains("helm"))return name.Contains("andariel",StringComparison.Ordinal)?"demonMask":name.Contains("harlequin",StringComparison.Ordinal)?"hood":"helm";
        if(nativeType is "tors" or "armo"||slots.Contains("armor"))return name.Contains("tal rasha",StringComparison.Ordinal)?"purpleArmor":"steelArmor";
        if(nativeType is "shie" or "ashd" or "head"||slots.Contains("shield"))return name.Contains("stormshield",StringComparison.Ordinal)?"roundShield":"shield";
        if(nativeType=="glov"||slots.Contains("gloves"))return "gloves";
        if(nativeType=="boot"||slots.Contains("boots"))return "boots";
        if(nativeType=="belt"||slots.Contains("belt"))return "belt";
        if(slots.Contains("ring1")||slots.Contains("ring2"))return "ring";
        if(slots.Contains("amulet"))return "amulet";
        // Runes share the small token illustration in the current atlas, but a
        // base such as Ring Mail (rng) must never be mistaken for a rune.
        if(nativeType=="rune"||code.Length==3&&code[0]=='r'&&char.IsAsciiDigit(code[1])&&char.IsAsciiDigit(code[2]))return "smallCharm";
        return "unknown";
    }
    static string Fallback(string key)=>key switch{"orbStaff"=>"orb","hood" or "demonMask"=>"helm","purpleArmor" or "steelArmor"=>"armor","roundShield"=>"shield","bookBlue" or "bookGold"=>"tome","grandCharm" or "smallCharm"=>"charm","scythe" or "javelin" or "axe" or "bow"=>"weapon",_=>key};
}
