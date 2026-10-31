using D2SSharp.Enums;
using D2SSharp.Model;
using static ForjaHoradrica.GameData;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private static object Requirements(Item item, GameData p)
    {
        var row = p.Bases[item.ItemCodeString]; int level = I(row, "levelreq"), strength = I(row, "reqstr"), dexterity = I(row, "reqdex");
        if (item.Quality is ItemQuality.Unique or ItemQuality.Set)
        {
            string file = item.Quality == ItemQuality.Unique ? "uniqueitems.txt" : "setitems.txt";
            var special = p.Read(file).FirstOrDefault(r => I(r, "*ID", -1) == item.QualityData?.FileIndex);
            if (special is not null) level = Math.Max(level, I(special, "lvl req"));
        }
        IEnumerable<int> prefixIds = item.QualityData is MagicQualityData magic ? [(int)magic.PrefixId] : item.QualityData is RareCraftQualityData rare ? rare.Prefixes.Select(n => (int)n) : [];
        IEnumerable<int> suffixIds = item.QualityData is MagicQualityData magic2 ? [(int)magic2.SuffixId] : item.QualityData is RareCraftQualityData rare2 ? rare2.Suffixes.Select(n => (int)n) : [];
        foreach (var pair in new[] { ("magicprefix.txt", prefixIds), ("magicsuffix.txt", suffixIds) }) foreach (int id in pair.Item2.Where(n => n > 0)) { var rows = p.Read(pair.Item1); if (id <= rows.Count) level = Math.Max(level, I(rows[id - 1], "levelreq")); }
        foreach (var child in item.Sockets) if (child is not null) level = Math.Max(level, RequiredLevel(child, p));
        int reqId = p.StatRows.FirstOrDefault(kv => V(kv.Value, "Stat") == "item_req_percent").Key;
        long modifier = item.Stats.Where(s => (int)s.Id == reqId).Sum(s => s.Value);
        if (item.Flags.HasFlag(ItemFlags.Ethereal)) { strength = Math.Max(0, strength - 10); dexterity = Math.Max(0, dexterity - 10); }
        strength = (int)Math.Max(0, strength + Math.Ceiling(strength * modifier / 100.0)); dexterity = (int)Math.Max(0, dexterity + Math.Ceiling(dexterity * modifier / 100.0));
        return new { level, strength, dexterity, estimated = true };
    }
    private static int RequiredLevel(Item item, GameData p)
    {
        int level = I(p.Bases[item.ItemCodeString], "levelreq");
        if (item.Quality is ItemQuality.Unique or ItemQuality.Set)
        {
            var row = p.Read(item.Quality == ItemQuality.Unique ? "uniqueitems.txt" : "setitems.txt").FirstOrDefault(r => I(r, "*ID", -1) == item.QualityData?.FileIndex);
            if (row is not null) level = Math.Max(level, I(row, "lvl req"));
        }
        return item.Sockets.Where(c => c is not null).Select(c => RequiredLevel(c!, p)).Append(level).Max();
    }
    private static void ValidateEquipment(D2Save save, GameData p)
    {
        foreach (var item in save.Items.Where(i => i.Position.Mode == ItemMode.Equipped))
        {
            ValidateClass(item, p, save, "character");
            if (!EquipSlots(item, p, "character", save).Contains(Slot(item))) throw new ApiProblem("equipment_slot", "Hay un objeto incompatible con su ranura de equipo.");
        }
        if (save.MercItems is not null)
        {
            if (!save.Character.MercData.HasMerc && save.MercItems.Items.Count > 0) throw new ApiProblem("mercenary_missing", "Hay equipo de mercenario sin un mercenario contratado.");
            if (save.MercItems.Items.GroupBy(i => i.Position.BodyLocation).Any(g => g.Count() > 1)) throw new ApiProblem("mercenary_collision", "Hay dos objetos en la misma ranura del mercenario.");
            foreach (var item in save.MercItems.Items)
                if (item.Position.Mode != ItemMode.Equipped || (int)item.Position.BodyLocation is < 1 or > 10 || !EquipSlots(item, p, "mercenary", save).Contains(Slot(item))) throw new ApiProblem("mercenary_slot", "El equipo del mercenario contiene una ubicación incompatible.");
        }
        foreach (string owner in new[] { "character", "mercenary" })
        {
            IEnumerable<Item> items = owner == "character" ? save.Items : save.MercItems?.Items ?? [];
            foreach (int main in owner == "character" ? new[] { 4, 11 } : [4])
            {
                var weapon = items.FirstOrDefault(i => i.Position.Mode == ItemMode.Equipped && (int)i.Position.BodyLocation == main);
                var offhand = items.FirstOrDefault(i => i.Position.Mode == ItemMode.Equipped && (int)i.Position.BodyLocation == main + 1);
                if (weapon is not null && offhand is not null && TwoHanded(weapon, p, owner == "character" && save.Character.Class.ToString() == "Barbarian") && !AllowedTwoHandOffhand(weapon, offhand, p, save, owner)) throw new ApiProblem("two_handed_conflict", "Un arma de dos manos ocupa una mano secundaria incompatible.");
            }
        }
        var belt = save.Items.Where(i => i.Position.Mode == ItemMode.InBelt).ToArray();
        if (belt.Any(i => i.Position.InvX >= BeltSlots(save, p)) || belt.GroupBy(i => i.Position.InvX).Any(g => g.Count() > 1)) throw new ApiProblem("belt_position", "El cinturón contiene posiciones duplicadas o fuera de sus límites.");
        int active = Array.FindIndex(save.Character.TownDifficulty, b => (b & 0x80) != 0);
        int acts = save.Character.Preview.GameVersion == GameVersion.Classic ? 4 : 5;
        int max = Math.Min(2, (int)(((uint)save.Character.Flags >> 8) & 31) / acts);
        if (active > max) throw new ApiProblem("difficulty_locked", "La dificultad de inicio todavía no está desbloqueada.");
    }
}
