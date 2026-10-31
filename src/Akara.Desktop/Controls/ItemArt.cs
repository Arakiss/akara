using System.IO;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Controls;

public static class ItemArt
{
    static readonly ImageSource Neutral = CreateNeutral();
    static readonly ConditionalWeakTable<BitmapSource, ImageSource> Presentation = new();
    public static ImageSource Get(ItemViewModel item) => Akara.Desktop.Services.LocalGameArt.TryGetItem(item) ?? Neutral;
    // The paper doll and inspector fit the original opaque silhouette, while the
    // inventory retains the native frame. No pixel, tint or item data is changed.
    public static ImageSource GetPresentation(ItemViewModel item)
    {
        var original = Get(item);
        if (original is not BitmapSource bitmap || bitmap.Format.BitsPerPixel != 32) return original;
        if (Presentation.TryGetValue(bitmap, out var cached)) return cached;
        int stride = checked(bitmap.PixelWidth * 4);
        var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        int left = bitmap.PixelWidth, top = bitmap.PixelHeight, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.PixelHeight; y++)
            for (int x = 0; x < bitmap.PixelWidth; x++)
                if (pixels[y * stride + x * 4 + 3] > 0)
                { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        if (right < left || bottom < top) return original;
        left = Math.Max(0, left - 1); top = Math.Max(0, top - 1);
        right = Math.Min(bitmap.PixelWidth - 1, right + 1); bottom = Math.Min(bitmap.PixelHeight - 1, bottom + 1);
        var crop = new CroppedBitmap(bitmap, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
        crop.Freeze(); Presentation.Add(bitmap, crop); return crop;
    }
    public static ImageSource Get(string key) => Neutral;
    static ImageSource CreateNeutral()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0,0,64,64));
            var line = new Pen(new SolidColorBrush(Color.FromRgb(109,116,121)),1);
            context.DrawRectangle(null,line,new Rect(13,9,38,46));
            context.DrawLine(line,new Point(22,25),new Point(42,25));
            context.DrawLine(line,new Point(22,33),new Point(42,33));
        }
        drawing.Freeze();var image = new DrawingImage(drawing);image.Freeze();return image;
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
