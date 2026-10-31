using D2SSharp.Enums;
using D2SSharp.Model;
using static ForjaHoradrica.GameData;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    // Presentation metadata only. Never changes the native item or its encoding.
    // The desktop uses these table values to resolve locally installed game art.
    private static object Artwork(Item item, GameData p)
    {
        if (!p.Bases.TryGetValue(item.ItemCodeString, out var basis))
            return new { inventoryFile = "", color = "", variableGfxId = item.VariableGfxId };

        string inventoryFile = V(basis, "invfile"), color = "", hdColor = "";
        int index = Convert.ToInt32(item.QualityData?.FileIndex ?? 0);
        Dictionary<string, string>? special = item.Quality switch
        {
            ItemQuality.Unique => p.Read("uniqueitems.txt").FirstOrDefault(r => I(r, "*ID", -1) == index),
            ItemQuality.Set => p.Read("setitems.txt").FirstOrDefault(r => I(r, "*ID", -1) == index),
            _ => null
        };
        if (special is not null)
        {
            string overrideFile = V(special, "invfile");
            if (overrideFile.Length == 0)
                overrideFile = V(basis, item.Quality == ItemQuality.Unique ? "uniqueinvfile" : "setinvfile");
            if (overrideFile.Length > 0) inventoryFile = overrideFile;
            color = V(special, "invtransform");
            hdColor = V(special, "chrtransform");
        }
        else if (item.Quality is ItemQuality.Magic or ItemQuality.Rare)
        {
            // Native precedence: the first coloured suffix, then prefix.
            // See ITEMS_GetColor in D2MOO; colour codes come from this profile.
            IEnumerable<int> suffixes = item.QualityData is MagicQualityData magicSuffix ? [magicSuffix.SuffixId]
                : item.QualityData is RareCraftQualityData rareSuffix ? rareSuffix.Suffixes.Select(n => (int)n) : [];
            IEnumerable<int> prefixes = item.QualityData is MagicQualityData magicPrefix ? [magicPrefix.PrefixId]
                : item.QualityData is RareCraftQualityData rarePrefix ? rarePrefix.Prefixes.Select(n => (int)n) : [];
            string AffixColor(string file, IEnumerable<int> ids)
            {
                var rows = p.Read(file);
                return ids.Where(n => n > 0 && n <= rows.Count).Select(n => V(rows[n - 1], "transformcolor")).FirstOrDefault(c => c.Length > 0) ?? "";
            }
            color = AffixColor("magicsuffix.txt", suffixes);
            if (color.Length == 0) color = AffixColor("magicprefix.txt", prefixes);
            hdColor = color;
        }
        var type = p.Read("itemtypes.txt").FirstOrDefault(r => V(r, "Code").Equals(V(basis, "type"), StringComparison.OrdinalIgnoreCase));
        string[] variants = type is null ? [] : Enumerable.Range(1, I(type, "VarInvGfx")).Select(n => V(type, "InvGfx" + n)).ToArray();
        if (item.VariableGfxId is { } variant && variant < variants.Length && variants[variant].Length > 0 && (special is null || V(special, "invfile").Length == 0))
            inventoryFile = variants[variant];
        return new { inventoryFile, baseInventoryFile = V(basis, "invfile"), color, hdColor, transform = I(basis, "InvTrans"), variableGfxId = item.VariableGfxId, variants };
    }
}
