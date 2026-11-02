using D2SSharp.Enums;
using D2SSharp.Model;
using static ForjaHoradrica.GameData;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private static object AdvancedCatalog(SaveSession session, string type, string? search, int offset, int limit)
    {
        var p = session.Profile; var save = Read(session.Original, p, session.Version);
        IEnumerable<Dictionary<string, string>> rows;
        string file = type switch { "stat" => "itemstatcost.txt", "prefix" => "magicprefix.txt", "suffix" => "magicsuffix.txt", "rareprefix" => "rareprefix.txt", "raresuffix" => "raresuffix.txt", "hireling" => "hireling.txt", "all-skills" => "skills.txt", _ => throw new ApiProblem("catalog_type", L.T("Tipo de catálogo desconocido.")) };
        var all = p.Read(file); rows = all;
        string Name(Dictionary<string, string> row) => type switch { "stat" => V(row, "Stat"), "hireling" => V(row, "Hireling") + " · " + string.Join(" / ", Enumerable.Range(1, 6).Select(n => V(row, "Skill" + n)).Where(n => n.Length > 0)), "all-skills" => V(row, "skill"), _ => V(row, "name") };
        if (type == "stat") rows = rows.Where(r => I(r, "Save Bits") > 0 && I(r, "*ID", -1) is >= 0 and < 511);
        else if (type is "prefix" or "suffix") rows = rows.Where(r => V(r, "spawnable") == "1");
        else if (type == "hireling")
        {
            int version = save.Character.Preview.GameVersion == GameVersion.Classic ? 0 : 100;
            rows = rows.Where(r => I(r, "Version") == version && V(r, "Hireling").Length > 0).GroupBy(r => I(r, "Id", -1)).Select(g => g.Where(r => I(r, "Level") <= save.Character.Level).OrderByDescending(r => I(r, "Level")).FirstOrDefault() ?? g.OrderBy(r => I(r, "Level")).First());
        }
        rows = rows.Where(r => Name(r).Length > 0);
        if (!string.IsNullOrWhiteSpace(search)) rows = rows.Where(r => Name(r).Contains(search, StringComparison.OrdinalIgnoreCase));
        var selected = rows.OrderBy(Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var entries = selected.Skip(offset).Take(limit).Select(r =>
        {
            int nativeId = type is "prefix" or "suffix" ? all.IndexOf(r) + 1 : type is "rareprefix" or "raresuffix" ? all.IndexOf(r) : I(r, type == "all-skills" ? "*Id" : type == "hireling" ? "Id" : "*ID");
            int bits = I(r, "Save Bits"), shift = I(r, "ValShift"), paramBits = I(r, "Save Param Bits"), add = I(r, "Save Add");
            double scale = V(r, "Stat").Contains("_perlevel", StringComparison.Ordinal) ? 8 : 1;
            return new { id = type + ":" + nativeId, nativeId, name = Name(r), label = Name(r).Replace("item_", "").Replace('_', ' '), type, supported = true, level = I(r, "level"), levelRequired = I(r, "levelreq"), saveBits = bits, paramBits, saveAdd = add, valShift = shift, minValue = bits > 0 ? -add / scale : (double?)null, maxValue = bits > 0 && bits <= 32 ? ((1L << bits) - 1 - add) / scale : (double?)null, allowedTypes = Enumerable.Range(1, 7).Select(n => V(r, "itype" + n)).Where(s => s.Length > 0), act = I(r, "Act"), version = I(r, "Version"), aura = type == "hireling" ? Enumerable.Range(1, 6).Select(n => V(r, "Skill" + n)).FirstOrDefault(n => n is "Might" or "Defiance" or "Blessed Aim" or "Prayer" or "Thorns" or "Holy Freeze") : null };
        });
        return new { entries, total = selected.Length };
    }
}
