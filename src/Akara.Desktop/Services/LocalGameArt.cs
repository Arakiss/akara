using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Services;

/// <summary>Optional, read-only game art import. Game files and saves are never written.
/// Format references and native library provenance: docs/LOCAL-GAME-ART.md.</summary>
public static class LocalGameArt
{
    const string RendererVersion = "renderer-v1";
    public sealed record ImportResult(int ItemsLoaded, int SkillsLoaded, IReadOnlyList<string> Missing, string CacheDirectory);
    static readonly ConcurrentDictionary<string, ImageSource> Images = new(StringComparer.Ordinal);
    static readonly ConcurrentDictionary<string, ImageSource> Skills = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, ImageSource> UiArt = new(StringComparer.Ordinal);
    static readonly Dictionary<string, string> UiPaths = new(StringComparer.Ordinal)
    {
        ["DiabloDivider"] = "data/hd/global/ui/pausemenu/divider.sprite",
        ["DiabloPortraitFrame"] = "data/hd/global/ui/panel/portraitframe.sprite"
    };
    static readonly SemaphoreSlim Gate = new(1, 1);
    static string activeSource = "";
    static FontFamily? displayFont;
    const string DisplayFontPath = "data/hd/ui/fonts/exocetblizzardot-medium.otf";
    public static FontFamily? TryGetDisplayFont() => displayFont;
    public static ImageSource? TryGetUiArt(string key) => UiArt.GetValueOrDefault(key);
    static readonly Dictionary<string, string> ClassFiles = new(StringComparer.OrdinalIgnoreCase)
    { ["Amazon"]="amazon/amskillicon", ["Sorceress"]="sorceress/soskillicon", ["Necromancer"]="necromancer/neskillicon", ["Paladin"]="paladin/paskillicon", ["Barbarian"]="barbarian/baskillicon", ["Druid"]="druid/drskillicon", ["Assassin"]="assassin/asskillicon", ["Warlock"]="warlock/waskillicon" };

    public static ImageSource? TryGetItem(ItemViewModel item) => Images.GetValueOrDefault(Key(item.Data));
    public static ImageSource? TryGetSkill(string iconClass, int iconCell) => Skills.GetValueOrDefault(iconClass + ":" + iconCell);
    public static Task<ImportResult> WarmupAsync(string gameRoot, string excelDirectory, IEnumerable<JsonObject> items, string? dataRoot = null, CancellationToken cancellationToken = default)
    {
        var nodes = items.Select(n => (JsonObject)n.DeepClone()).ToArray();
        return Task.Run(async () =>
        {
            await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return Import(gameRoot, excelDirectory, nodes, dataRoot, cancellationToken); }
            finally { Gate.Release(); }
        }, cancellationToken);
    }

    static ImportResult Import(string gameRoot, string excel, JsonObject[] items, string? dataRoot, CancellationToken ct)
    {
        var missing = new List<string>();
        if (!Directory.Exists(gameRoot) || !Directory.Exists(excel)) { displayFont = null; return new(0, 0, ["No hay una instalación y perfil disponibles."], ""); }
        string modData = Directory.GetParent(Directory.GetParent(Path.GetFullPath(excel))!.FullName)!.FullName;
        string[] dependencies = [Path.Combine(gameRoot, ".build.info"), Path.Combine(modData, "global/ui/layouts/globaldatahd.json"), ..new[]{"items","uniques","sets"}.Select(n=>Path.Combine(modData,"hd/items/"+n+".json")), ..new[]{"uniqueitems","setitems","magicprefix","magicsuffix","automagic","itemtypes"}.Select(n=>Path.Combine(excel,n+".txt"))];
        string stamp = RendererVersion + "|" + gameRoot + "|" + excel + "|" + string.Join("|", dependencies.Select(LastWrite));
        string source = Hash(Encoding.UTF8.GetBytes(stamp))[..24];
        string cache = Path.GetFullPath(Path.Combine(dataRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AKARA"), "game-art", source));
        if (activeSource != cache) { Images.Clear(); Skills.Clear(); UiArt.Clear(); displayFont = null; activeSource = cache; }
        if (Inside(cache, gameRoot)) throw new IOException("La caché de imágenes debe estar fuera del juego.");
        Directory.CreateDirectory(cache);
        if (Flatten(items).All(n => Images.ContainsKey(Key(n))) && Skills.Count > 0 && displayFont is not null && UiArt.Count == UiPaths.Count) return new(Images.Count, Skills.Count, missing, cache);
        using var reader = new GameReader(gameRoot, modData);
        ImportDisplayFont(reader, cache);
        ImportUiArt(reader, cache);
        var bases = ReadMap(reader.TryRead("data/hd/items/items.json"), "asset");
        var uniques = ReadMap(reader.TryRead("data/hd/items/uniques.json"), "normal");
        var sets = ReadMap(reader.TryRead("data/hd/items/sets.json"), "normal");
        var tables = new[] { "weapons.txt", "armor.txt", "misc.txt" }.SelectMany(f => Tsv(Path.Combine(excel, f))).ToDictionary(r => r.GetValueOrDefault("code", ""), r => r, StringComparer.OrdinalIgnoreCase);
        var manifest = new JsonObject { ["schema"] = 1, ["renderer"] = RendererVersion, ["source"] = "local-game-install", ["sourceKey"] = source, ["items"] = new JsonArray() };
        string manifestPath=Path.Combine(cache,"manifest.json");
        if(File.Exists(manifestPath))try
        {
            using var stream=new FileStream(manifestPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var text=new StreamReader(stream);
            if(JsonNode.Parse(text.ReadToEnd()) is JsonObject previous&&previous["items"] is JsonArray existing)manifest["items"]=existing.DeepClone();
        }
        catch(Exception e)when(e is IOException or JsonException){/* An optional cache is rebuilt from installed files. */}
        foreach (var node in Flatten(items))
        {
            ct.ThrowIfCancellationRequested();
            string key = Key(node); if (Images.ContainsKey(key)) continue;
            try
            {
                string code = node.Text("code"), name = node.Text("name"), quality = node.Text("quality"), normalized = NormalizeName(name);
                string asset = quality == "unique" ? uniques.GetValueOrDefault(normalized, "") : quality == "set" ? sets.GetValueOrDefault(normalized, "") : "";
                if (asset.Length == 0) asset = bases.GetValueOrDefault(code, "");
                // Random ring/amulet/charm pictures must resolve their actual native invfile,
                // rather than pretending that one generic base picture is exact.
                var art = node.Obj("artwork"); string inventory = art.Text("inventoryFile"), original = art.Text("baseInventoryFile");
                if (quality is not ("unique" or "set") && inventory.Length > 0 && original.Length > 0 && inventory != original)
                {
                    string alternative = bases.GetValueOrDefault(inventory, "");
                    if (art.NullableInt("variableGfxId") is { } variant && code is "rin" or "amu" or "cm1" or "cm2" or "cm3")
                    {
                        string variantAsset = asset + (variant + 1);
                        if (FindSprite(reader, variantAsset, node.Text("itemType")).Length > 0) alternative = variantAsset;
                    }
                    if (alternative.Length == 0)
                    {
                        var matching = tables.FirstOrDefault(t => t.Value.GetValueOrDefault("invfile") == inventory);
                        if (matching.Key is not null) alternative = bases.GetValueOrDefault(matching.Key, "");
                    }
                    if (alternative.Length > 0) asset = alternative;
                    else throw new InvalidDataException("Variante de inventario nativa no resuelta: " + inventory);
                }
                if (asset.Length == 0) throw new InvalidDataException("Sin correspondencia HD nativa.");
                string path = FindSprite(reader, asset, node.Text("itemType"));
                if (path.Length == 0) throw new FileNotFoundException("No se encuentra el sprite nativo: " + asset);
                byte[] raw = reader.Read(path); var frame = DecodeSprite(raw, 0);
                // D2R's item JSON chooses the authentic image. A color transform is applied
                // only from installed engine presets, never from an invented color filter.
                string color = art.Text("hdColor", art.Text("color"));
                if (color.Length > 0) ApplyInstalledColor(reader, path, frame.Pixels, color, art.Int("transform"));
                var bitmap = Bitmap(frame.Width, frame.Height, frame.Pixels);
                string png = Path.Combine(cache, key + ".png"); Save(bitmap, png);
                Images[key] = bitmap;
                var entries=(JsonArray)manifest["items"]!;foreach(var old in entries.OfType<JsonObject>().Where(n=>n.Text("key")==key).ToArray())entries.Remove(old);
                entries.Add(new JsonObject { ["key"] = key, ["name"] = name, ["path"] = path, ["sourceHash"] = Hash(raw), ["color"] = color, ["png"] = Path.GetFileName(png) });
            }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or ArgumentException or OverflowException) { missing.Add(node.Text("name") + ": " + e.Message); }
        }
        foreach (var (cls, relative) in ClassFiles)
        {
            ct.ThrowIfCancellationRequested();
            if (Skills.ContainsKey(cls + ":0")) continue;
            string path = "data/hd/global/ui/spells/" + relative + ".sprite";
            byte[]? bytes = reader.TryRead(path);
            if (bytes is null)
            {
                string prefix = cls switch { "Amazon"=>"aam", "Sorceress"=>"sso", "Necromancer"=>"ne", "Paladin"=>"pa", "Barbarian"=>"ba", "Druid"=>"dr", "Assassin"=>"aas", _=>"wa" };
                path = "data/hd/global/ui/spells/" + prefix + "skillicon.sprite"; bytes = reader.TryRead(path);
            }
            if (bytes is null) { missing.Add("Iconos de " + cls + " no disponibles."); continue; }
            try
            {
                int count = checked((int)U32(bytes, 20));
                for (int i = 0; i < Math.Min(count, 256); i++)
                {
                    var f = DecodeSprite(bytes, i); var image = Bitmap(f.Width, f.Height, f.Pixels); Skills[cls + ":" + i] = image;
                    Save(image, Path.Combine(cache, "skill-" + cls.ToLowerInvariant() + "-" + i + ".png"));
                }
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or OverflowException) { missing.Add(cls + ": " + e.Message); }
        }
        manifest["missing"] = JsonSerializer.SerializeToNode(missing);
        WriteManifestAtomic(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return new(Images.Count, Skills.Count, missing, cache);
    }

    static void ImportDisplayFont(GameReader reader, string cache)
    {
        if (displayFont is not null) return;
        try
        {
            byte[]? bytes = reader.TryRead(DisplayFontPath);
            if (bytes is null) return;
            string hash = Hash(bytes), directory = Path.Combine(cache, "fonts", hash);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "exocetblizzardot-medium.otf");
            if (!File.Exists(path))
            {
                string temporary = path + "." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(temporary, bytes);
                    try { File.Move(temporary, path, false); }
                    catch (IOException) when (File.Exists(path)) { /* Another process published the same content-addressed font. */ }
                }
                finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
            }
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(path)), SHA256.HashData(bytes))) return;
            // Use the preferred WPF family name from the installed font itself. No font is
            // installed into Windows, embedded in the executable or copied into the repository.
            var typeface = new GlyphTypeface(new Uri(path, UriKind.Absolute));
            string family = typeface.FamilyNames.TryGetValue(System.Globalization.CultureInfo.GetCultureInfo("en-US"), out var english)
                ? english : typeface.FamilyNames.Values.First();
            displayFont = new FontFamily(new Uri(directory + Path.DirectorySeparatorChar, UriKind.Absolute), "./#" + family);
            WriteManifestAtomic(Path.Combine(directory, "provenance.json"), new JsonObject
            {
                ["source"] = "local-game-install", ["path"] = DisplayFontPath,
                ["sourceHash"] = hash, ["bytes"] = bytes.Length, ["family"] = family,
                ["distribution"] = "local-cache-only"
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        { displayFont = null; /* Typography is optional; the native theme keeps its fallback. */ }
    }

    static void ImportUiArt(GameReader reader, string cache)
    {
        var provenance = new JsonArray();
        foreach (var (key, path) in UiPaths)
        {
            try
            {
                byte[]? bytes = reader.TryRead(path); if (bytes is null) continue;
                var frame = DecodeSprite(bytes, 0); var image = Bitmap(frame.Width, frame.Height, frame.Pixels);
                Save(image, Path.Combine(cache, key + ".png")); UiArt[key] = image;
                provenance.Add(new JsonObject { ["key"] = key, ["path"] = path, ["sourceHash"] = Hash(bytes), ["width"] = frame.Width, ["height"] = frame.Height });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or OverflowException)
            { /* Optional decoration falls back to the native theme. */ }
        }
        try { WriteManifestAtomic(Path.Combine(cache, "ui-provenance.json"), new JsonObject { ["source"] = "local-game-install", ["images"] = provenance }.ToJsonString(new JsonSerializerOptions { WriteIndented = true })); }
        catch (IOException) { /* A manifest must not prevent the original decoded image from being displayed. */ }
    }

    static IEnumerable<JsonObject> Flatten(IEnumerable<JsonObject> items)
    { foreach (var n in items) { yield return n; foreach (var child in Flatten(n.Array("sockets").OfType<JsonObject>())) yield return child; } }
    static string Key(JsonObject n) => Hash(Encoding.UTF8.GetBytes(n.Text("code") + "|" + n.Text("quality") + "|" + n.Text("name") + "|" + n.Obj("artwork").ToJsonString()))[..32];
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static long LastWrite(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
    static bool Inside(string path, string directory) => path.StartsWith(Path.GetFullPath(directory).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    static string NormalizeName(string name) => Regex.Replace(name.Replace("'", "").ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
    static Dictionary<string,string> ReadMap(byte[]? bytes, string field)
    {
        var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); if (bytes is null) return map;
        using var doc = JsonDocument.Parse(bytes); var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array) return map;
        foreach (var row in root.EnumerateArray()) foreach (var entry in row.EnumerateObject())
            if (entry.Value.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String) map[entry.Name] = value.GetString()!;
        return map;
    }
    static IEnumerable<Dictionary<string,string>> Tsv(string path)
    {
        if (!File.Exists(path)) yield break; using var reader = new StreamReader(path); string[] fields = (reader.ReadLine() ?? "").Split('\t');
        while (reader.ReadLine() is { } line) { string[] values = line.Split('\t'); var row = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); for (int i=0;i<Math.Min(fields.Length, values.Length);i++) row[fields[i]]=values[i]; if(row.GetValueOrDefault("code", "").Length>0) yield return row; }
    }
    static string FindSprite(GameReader reader, string asset, string type)
    {
        if (!Regex.IsMatch(asset, "^[a-zA-Z0-9_/-]+$")) return "";
        string name=asset.Split('/')[^1];
        foreach(string group in new[]{"weapon","armor","misc",""}) foreach(string sub in new[]{asset,name})
        {
            string path="data/hd/global/ui/items/"+(group.Length>0?group+"/":"")+sub+".sprite";
            if(reader.Exists(path)) return path;
        }
        string doubled="data/hd/global/ui/items/misc/"+name[0]+name+".sprite"; return reader.Exists(doubled)?doubled:"";
    }

    public sealed record SpriteFrame(int Width, int Height, byte[] Pixels);
    public static SpriteFrame DecodeSprite(byte[] bytes, int frame)
    {
        if(bytes.Length<40 || Encoding.ASCII.GetString(bytes,0,4) is not ("SpA1" or "SPa1")) throw new InvalidDataException("Cabecera sprite inválida.");
        int version=BitConverter.ToUInt16(bytes,4), fw=BitConverter.ToUInt16(bytes,6), w=checked((int)U32(bytes,8)), h=checked((int)U32(bytes,12)), count=checked((int)U32(bytes,20));
        if(w<=0||h<=0||w>16384||h>8192||count<1||count>256||frame<0||frame>=count) throw new InvalidDataException("Dimensiones sprite inválidas.");
        int stride=w/count; if(fw<=0||fw>stride) fw=stride;
        if(version!=31) throw new InvalidDataException("Formato sprite no compatible: "+version);
        if(40L+(long)w*h*4>bytes.LongLength) throw new InvalidDataException("Sprite truncado.");
        var rgba=new byte[checked(fw*h*4)]; for(int y=0;y<h;y++) Buffer.BlockCopy(bytes,checked(40+(y*w+frame*stride)*4),rgba,y*fw*4,fw*4);
        return new(fw,h,rgba);
    }
    static uint U32(byte[] bytes,int offset) => BitConverter.ToUInt32(bytes,offset);
    static BitmapSource Bitmap(int w,int h,byte[] rgba)
    { var bgra=(byte[])rgba.Clone();for(int i=0;i<bgra.Length;i+=4)(bgra[i],bgra[i+2])=(bgra[i+2],bgra[i]);var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,bgra,w*4);bitmap.Freeze();return bitmap; }
    static void Save(BitmapSource bitmap,string path)
    {
        if(File.Exists(path))return;
        string temporary=path+"."+Environment.ProcessId+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){encoder.Save(stream);stream.Flush(true);}
            try{File.Move(temporary,path,false);}
            catch(IOException)when(File.Exists(path)){/* Another process published its complete PNG first. The decoded bitmap is valid. */}
        }
        finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){/* Best-effort cleanup of an optional private cache. */}}
    }
    static void WriteManifestAtomic(string path,string json)
    {
        string temporary=path+"."+Environment.ProcessId+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false))){writer.Write(json);writer.Flush();stream.Flush(true);}
            File.Move(temporary,path,true);
        }
        finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){/* Best-effort cleanup of an optional private cache. */}}
    }
    static void ApplyInstalledColor(GameReader reader,string sprite,byte[] pixels,string color,int transform)
    {
        byte[] raw=reader.Read("data/global/ui/layouts/globaldatahd.json");
        using var doc=JsonDocument.Parse(raw,new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});
        var fields=doc.RootElement.GetProperty("children").EnumerateArray().First(n=>n.TryGetProperty("type",out var t)&&t.GetString()=="SpriteColoringHelper").GetProperty("fields");
        JsonElement preset=fields.GetProperty("colorTransform").EnumerateArray().FirstOrDefault(n=>n.GetProperty("comment").GetString()?.EndsWith(" "+color,StringComparison.Ordinal)==true);
        if(preset.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Preset de color no disponible: "+color);
        string path=Regex.Replace(sprite,@"(?:\.lowend)?\.sprite$","");
        if(fields.TryGetProperty("colorTransformOverride",out var transformOverrides))foreach(var row in transformOverrides.EnumerateArray())if(row[0].GetString()==path&&row[1].GetString()==color)preset=row[2];
        JsonElement range=default;
        var ranges=fields.GetProperty("colorRange");if(transform>=0&&transform<ranges.GetArrayLength())range=ranges[transform];
        if(fields.TryGetProperty("colorRangeOverride",out var rangeOverrides))foreach(var row in rangeOverrides.EnumerateArray())if(row[0].GetString()==path)range=row[1];
        if(range.ValueKind==JsonValueKind.String&&range.GetString()=="$EmptyColorRange")return;
        if(range.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Máscara de color nativa no disponible: "+transform);
        double[] hsv=preset.GetProperty("hsvTransform").EnumerateArray().Select(n=>n.GetDouble()).ToArray(), tint=preset.GetProperty("tintTransform").EnumerateArray().Select(n=>n.GetDouble()).ToArray();
        double[] hr=Pair("hue"),sr=Pair("saturation"),vr=Pair("value");
        double[] Pair(string key)=>range.GetProperty(key).EnumerateArray().Select(n=>n.GetDouble()).ToArray();
        for(int i=0;i<pixels.Length;i+=4)
        {
            if(pixels[i+3]==0)continue;
            double r=pixels[i]/255d,g=pixels[i+1]/255d,b=pixels[i+2]/255d,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),delta=max-min;
            double h=delta==0?0:max==r?((g-b)/delta%6)/6:max==g?((b-r)/delta+2)/6:((r-g)/delta+4)/6;if(h<0)h+=1;
            double s=max==0?0:delta/max,v=max;
            // Verified against the installed ps_ui_p.bin advanced-color DXBC:
            // every HSV distance wraps, S/V use gains, and tint multiplies linear RGB.
            static double Distance(double a,double center){double d=Math.Abs(a-center);return Math.Min(d,1-d);}
            if(Distance(h,hr[0])>=hr[1]||Distance(s,sr[0])>=sr[1]||Distance(v,vr[0])>=vr[1])continue;
            h=(h+hsv[0])%1;if(h<0)h+=1;s*=1+hsv[1];v*=1+hsv[2];
            double c=v*s,x=c*(1-Math.Abs(h*6%2-1)),m=v-c;
            (r,g,b)=(h*6) switch {<1=>(c,x,0d),<2=>(x,c,0d),<3=>(0d,c,x),<4=>(0d,x,c),<5=>(x,0d,c),_=>(c,0d,x)};
            static double Linear(double c)=>c<0.04045?c/12.92:Math.Pow(Math.Abs((c+0.055)/1.055),2.4);
            static double Srgb(double c)=>c<0.0031308?c*12.92:1.055*Math.Pow(Math.Abs(c),1/2.4)-0.055;
            r=Srgb(Linear(r+m)*(1-tint[3]+Linear(tint[0])*tint[3]));g=Srgb(Linear(g+m)*(1-tint[3]+Linear(tint[1])*tint[3]));b=Srgb(Linear(b+m)*(1-tint[3]+Linear(tint[2])*tint[3]));
            pixels[i]=(byte)Math.Clamp(Math.Round(r*255),0,255);pixels[i+1]=(byte)Math.Clamp(Math.Round(g*255),0,255);pixels[i+2]=(byte)Math.Clamp(Math.Round(b*255),0,255);
        }
    }

    sealed class GameReader : IDisposable
    {
        readonly string modData; IntPtr storage;
        public GameReader(string gameRoot,string modData)
        { this.modData=modData; try { CascOpenStorage(gameRoot,0,out storage); } catch(DllNotFoundException) { } }
        string Loose(string path)
        { string relative=path.StartsWith("data/",StringComparison.OrdinalIgnoreCase)?path[5..]:path;string full=Path.GetFullPath(Path.Combine(modData,relative.Replace('/',Path.DirectorySeparatorChar)));return Inside(full,modData)?full:""; }
        public bool Exists(string path)
        { if(File.Exists(Loose(path)))return true;if(storage==IntPtr.Zero)return false;if(!CascOpenFile(storage,"data:"+path.Replace('/','\\'),0,0,out var file))return false;CascCloseFile(file);return true; }
        public byte[]? TryRead(string path) { try{return Read(path);}catch(IOException){return null;} }
        public byte[] Read(string path)
        {
            string loose=Loose(path);if(File.Exists(loose)){if(new FileInfo(loose).Length>64*1024*1024)throw new InvalidDataException("Asset demasiado grande.");return File.ReadAllBytes(loose);}
            if(storage==IntPtr.Zero||!CascOpenFile(storage,"data:"+path.Replace('/','\\'),0,0,out var file))throw new FileNotFoundException(path);
            try{if(!CascGetFileSize64(file,out ulong size)||size>64*1024*1024)throw new InvalidDataException("Tamaño CASC inválido.");var bytes=new byte[(int)size];if(!CascReadFile(file,bytes,(uint)size,out uint count)||count!=(uint)size)throw new IOException("Lectura CASC incompleta.");return bytes;}finally{CascCloseFile(file);}
        }
        public void Dispose(){if(storage!=IntPtr.Zero){CascCloseStorage(storage);storage=IntPtr.Zero;}}
    }
    [DllImport("CascLib",CharSet=CharSet.Ansi,SetLastError=true)] [return:MarshalAs(UnmanagedType.I1)] static extern bool CascOpenStorage(string path,uint locale,out IntPtr storage);
    [DllImport("CascLib",CharSet=CharSet.Ansi,SetLastError=true)] [return:MarshalAs(UnmanagedType.I1)] static extern bool CascOpenFile(IntPtr storage,string path,uint locale,uint flags,out IntPtr file);
    [DllImport("CascLib",SetLastError=true)] [return:MarshalAs(UnmanagedType.I1)] static extern bool CascGetFileSize64(IntPtr file,out ulong size);
    [DllImport("CascLib",SetLastError=true)] [return:MarshalAs(UnmanagedType.I1)] static extern bool CascReadFile(IntPtr file,byte[] bytes,uint size,out uint count);
    [DllImport("CascLib")] [return:MarshalAs(UnmanagedType.I1)] static extern bool CascCloseFile(IntPtr file);
    [DllImport("CascLib")] [return:MarshalAs(UnmanagedType.I1)] static extern bool CascCloseStorage(IntPtr storage);
}
