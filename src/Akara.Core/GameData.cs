using System.Security.Cryptography;
using System.Collections.Concurrent;
using D2SSharp.Data;
using D2SSharp.Model;
using D2SSharp.Enums;

namespace ForjaHoradrica;

public sealed class GameData
{
    private readonly ConcurrentDictionary<string, List<Dictionary<string, string>>> tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<uint, IExternalData> externalData = new();
    private readonly Dictionary<string, string> fileHashes = new(StringComparer.OrdinalIgnoreCase);
    public string Id { get; }
    public string Name { get; }
    public string ExcelPath { get; }
    public string StringsPath { get; }
    public string Kind => "mod";
    public Dictionary<string, Dictionary<string, string>> Bases { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, Dictionary<string, string>> StatRows { get; }
    public GameData(string path, string? stringsPath = null, string? name = null)
    {
        ExcelPath = Path.GetFullPath(path);
        if (!Directory.Exists(ExcelPath)) throw new ApiProblem("profile_missing", "No existe la carpeta de tablas seleccionada.");
        Name = name ?? (ExcelPath.Contains("Reimagined", StringComparison.OrdinalIgnoreCase) ? "D2R Reimagined" : new DirectoryInfo(ExcelPath).Name);
        StringsPath = stringsPath is null ? Path.GetFullPath(Path.Combine(ExcelPath, "..", "..", "local", "lng", "strings")) : Path.GetFullPath(stringsPath);
        foreach (var file in new[] { "charstats.txt", "skills.txt", "itemstatcost.txt", "experience.txt", "inventory.txt", "properties.txt", "itemtypes.txt", "uniqueitems.txt", "setitems.txt", "runes.txt", "weapons.txt", "armor.txt", "misc.txt", "magicprefix.txt", "magicsuffix.txt", "automagic.txt" }) Read(file);
        foreach (var file in new[] { "weapons.txt", "armor.txt", "misc.txt" })
            foreach (var row in Read(file)) if (V(row, "code").Length > 0) Bases.TryAdd(V(row, "code"), row);
        StatRows = Read("itemstatcost.txt").Where(r => V(r, "Stat").Length > 0 && int.TryParse(V(r, "*ID"), out _)).ToDictionary(r => I(r, "*ID"));
        Id = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ExcelPath.ToLowerInvariant())))[..16].ToLowerInvariant();
        foreach (var file in Directory.EnumerateFiles(ExcelPath, "*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) fileHashes[Path.GetFileName(file)] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        if (File.Exists(Path.Combine(StringsPath, "item-runes.json"))) fileHashes[Path.GetRelativePath(ExcelPath, Path.Combine(StringsPath, "item-runes.json"))] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(StringsPath, "item-runes.json"))));
    }
    public void EnsureUnchanged()
    {
        foreach (var kv in fileHashes)
            if (!File.Exists(Path.Combine(ExcelPath, kv.Key)) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(ExcelPath, kv.Key)))) != kv.Value)
                throw new ApiProblem("profile_changed", "Las tablas del juego han cambiado. Vuelve a abrir el perfil antes de editar o guardar.", 409);
    }
    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", fileHashes.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key + ":" + kv.Value)))));
    public IExternalData External(uint version) => externalData.GetOrAdd(version, v => new TxtFileExternalData(ExcelPath, v));
    public object[] Classes() => Read("charstats.txt").Where(r => Enum.TryParse<CharacterClass>(V(r, "class"), true, out _)).Select(r => (object)new { id = (int)Enum.Parse<CharacterClass>(V(r, "class"), true), name = V(r, "class"), maxLevel = I(Read("experience.txt").First(x => V(x, "Level") == "MaxLvl"), V(r, "class"), 99), baseAttributes = new { strength = I(r, "str"), dexterity = I(r, "dex"), vitality = I(r, "vit"), energy = I(r, "int") } }).ToArray();
    public object Info() => new { Id, Name, Kind, ExcelPath, StringsPath, classes = Classes() };
    public List<Dictionary<string, string>> Read(string file)
    {
        if (tables.TryGetValue(file, out var value)) return value;
        var path = Path.Combine(ExcelPath, file);
        if (!File.Exists(path)) throw new ApiProblem("table_missing", "Falta una tabla del perfil: " + file);
        if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new ApiProblem("table_too_large", "La tabla es demasiado grande: " + file);
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0) throw new ApiProblem("table_invalid", "La tabla está vacía: " + file);
        var keys = lines[0].TrimStart('\uFEFF').Split('\t');
        value = [];
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.Split('\t'); var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < keys.Length; i++) if (!string.IsNullOrWhiteSpace(keys[i])) row[keys[i]] = i < cells.Length ? cells[i] : "";
            value.Add(row);
        }
        tables[file] = value;
        return value;
    }
    public Dictionary<string, string> Character(D2Save save) => Read("charstats.txt").SingleOrDefault(r => V(r, "class").Equals(save.Character.Class.ToString(), StringComparison.OrdinalIgnoreCase)) ?? throw new ApiProblem("class_unsupported", "Las tablas no contienen esta clase.");
    public static string V(Dictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value : "";
    public static int I(Dictionary<string, string> row, string key, int fallback = 0) => int.TryParse(V(row, key), out var value) ? value : fallback;
    public (int Width, int Height) Size(Item item) => Bases.TryGetValue(item.ItemCodeString, out var row) ? (I(row, "invwidth", 1), I(row, "invheight", 1)) : throw new ApiProblem("base_missing", "La base del objeto no existe en este perfil: " + item.ItemCodeString);
    public (int Width, int Height) Grid(D2Save save, string page)
    {
        string key = page == "stash" ? "Big Bank Page 1" : page == "cube" ? "Transmogrify Box Page 1" : save.Character.Class.ToString();
        var row = Read("inventory.txt").FirstOrDefault(r => V(r, "class") == key) ?? throw new ApiProblem("grid_missing", "Falta la cuadrícula nativa: " + key);
        return (I(row, "gridX"), I(row, "gridY"));
    }
}
