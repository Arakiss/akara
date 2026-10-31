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
        string code=item.Code.ToLowerInvariant(), name=item.Name.ToLowerInvariant();
        if(code is "tbk")return "bookBlue";if(code is "ibk")return "bookGold";if(code is "box" or "bag")return "cube";
        if(code is "cm3")return "grandCharm";if(code is "cm1" or "cm2")return "smallCharm";if(code is "rvl"||item.Kind is "hpot" or "mpot" or "rpot")return "potion";
        if(code is "rin"||item.Slot is "ring1" or "ring2")return "ring";if(code is "amu"||item.Slot=="amulet")return "amulet";
        if(item.Slot=="helm")return name.Contains("andariel")?"demonMask":name.Contains("harlequin")?"hood":"helm";
        if(item.Slot=="armor")return name.Contains("tal rasha")?"purpleArmor":"steelArmor";
        if(item.Slot=="shield")return name.Contains("stormshield")?"roundShield":"shield";
        if(item.Slot=="gloves")return "gloves";if(item.Slot=="boots")return "boots";if(item.Slot=="belt")return "belt";
        if(item.Slot=="weapon"||item.Kind is "weap" or "staf" or "scep")
        {
            if(name.Contains("oculus")||name.Contains("fathom")||item.Kind=="orb")return "orbStaff";
            if(name.Contains("titan")||name.Contains("thunderstroke")||item.Kind.Contains("jave"))return "javelin";
            if(name.Contains("infinity")||name.Contains("reaper")||item.Kind is "pole" or "spea")return "scythe";
            if(item.Kind.Contains("bow"))return "bow";if(item.Kind.Contains("axe"))return "axe";
            return "sword";
        }
        if(code.StartsWith('r'))return "smallCharm";
        return item.Height>1?"grandCharm":"smallCharm";
    }
    static string Fallback(string key)=>key switch{"orbStaff"=>"orb","hood" or "demonMask"=>"helm","purpleArmor" or "steelArmor"=>"armor","roundShield"=>"shield","bookBlue" or "bookGold"=>"tome","grandCharm" or "smallCharm"=>"charm","scythe" or "javelin" or "axe" or "bow"=>"weapon",_=>key};
}
