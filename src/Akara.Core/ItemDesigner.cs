using System.Text.Json;
using D2SSharp.Enums;
using D2SSharp.Model;
using D2RBuilder;
using static ForjaHoradrica.GameData;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private static Item DesignItem(ItemDesignRequest design, GameData p, uint version, int depth = 0)
    {
        if (depth > 5 || design is null || !p.Bases.TryGetValue(design.BaseCode, out var baseRow) || design.Level is < 1 or > 99) throw new ApiProblem("design_base", L.T("Selecciona una base nativa, un nivel válido y un diseño sin anidación excesiva."));
        var factory = new ItemFactory(p.ExcelPath) { AllowNonSpawnableUniques = true, StringsDirectory = p.StringsPath, ItemFormatVersion = version >= 105 ? (ushort)101 : (ushort)Math.Min(version, 101u) };
        Item item;
        if (design.RunewordName is not null) item = factory.CreateRuneword(design.RunewordName, design.BaseCode, design.Level);
        else if (design.Quality == "unique") item = factory.CreateUnique(design.UniqueName ?? throw new ApiProblem("design_unique", L.T("Selecciona un único del catálogo.")), design.Level);
        else if (design.Quality == "set") item = factory.CreateSet(design.SetName ?? throw new ApiProblem("design_set", L.T("Selecciona un objeto de conjunto.")), design.Level);
        else if (design.Quality == "magic" && (design.PrefixIds.Length > 0 || design.SuffixIds.Length > 0))
        {
            if (design.PrefixIds.Length > 1 || design.SuffixIds.Length > 1) throw new ApiProblem("design_affixes", L.T("Un objeto mágico admite un prefijo y un sufijo."));
            item = factory.CreateMagicFromIds(design.BaseCode, design.PrefixIds.FirstOrDefault(), design.SuffixIds.FirstOrDefault(), design.Level);
        }
        else if (design.Quality == "magic" && (!string.IsNullOrWhiteSpace(design.Prefix) || !string.IsNullOrWhiteSpace(design.Suffix))) item = factory.CreateMagic(design.BaseCode, design.Prefix ?? "", design.Suffix, design.Level);
        else
        {
            item = factory.CreateNormal(design.BaseCode, design.Level);
            switch (design.Quality)
            {
                case "normal": break;
                case "magic": item.Quality = ItemQuality.Magic; item.QualityData = new MagicQualityData(); break;
                case "rare": case "crafted":
                    if (design.RarePrefixId is < 0 or > 255 || design.RareSuffixId is < 0 or > 255 || design.PrefixIds.Length > 3 || design.SuffixIds.Length > 3) throw new ApiProblem("design_affixes", L.T("El objeto admite hasta tres prefijos y tres sufijos."));
                    int prefixCount = p.Read("magicprefix.txt").Count, suffixCount = p.Read("magicsuffix.txt").Count;
                    if (design.PrefixIds.Any(i => i < 0 || i > prefixCount || i > 2047) || design.SuffixIds.Any(i => i < 0 || i > suffixCount || i > 2047)) throw new ApiProblem("design_affixes", L.T("Un afijo no existe en las tablas seleccionadas."));
                    item.Quality = (ItemQuality)(design.Quality == "rare" ? 6 : 8);
                    item.QualityData = new RareCraftQualityData { RarePrefixId = (byte)design.RarePrefixId, RareSuffixId = (byte)design.RareSuffixId, Prefixes = design.PrefixIds.Concat([0, 0, 0]).Take(3).Select(i => (ushort)i).ToArray(), Suffixes = design.SuffixIds.Concat([0, 0, 0]).Take(3).Select(i => (ushort)i).ToArray() };
                    foreach (int id in design.PrefixIds.Where(i => i > 0)) factory.ApplyNativeAffix(item, id, true);
                    foreach (int id in design.SuffixIds.Where(i => i > 0)) factory.ApplyNativeAffix(item, id, false);
                    break;
                case "superior": item.Quality = ItemQuality.Superior; item.QualityData = new SuperiorQualityData(); break;
                case "inferior": item.Quality = ItemQuality.Inferior; item.QualityData = new InferiorQualityData(); break;
                default: throw new ApiProblem("design_quality", L.T("Calidad no admitida por el diseñador."));
            }
        }
        if (item.ItemCodeString != design.BaseCode) throw new ApiProblem("design_catalog_base", L.T("La base elegida no coincide con el único o conjunto del catálogo."));
        if (design.Stats is not null)
        {
            if (design.Stats.Count > 256 || design.Stats.Any(s => s is null) || design.Stats.GroupBy(s => (s.Id, s.Layer)).Any(g => g.Count() > 1)) throw new ApiProblem("design_stats", L.T("La lista de propiedades contiene duplicados o es demasiado grande."));
            item.Stats = design.Stats.Select(s => NativeStat(s, p)).OrderBy(s => (int)s.Id).ThenBy(s => s.Layer).ToList();
        }
        if (design.RunewordStats is not null)
        {
            if (!item.Flags.HasFlag(ItemFlags.Runeword) || design.RunewordStats.Count > 256) throw new ApiProblem("design_runeword_stats", L.T("Las propiedades de palabra rúnica necesitan una palabra rúnica nativa."));
            item.RunewordStats = design.RunewordStats.Select(s => NativeStat(s, p)).OrderBy(s => (int)s.Id).ThenBy(s => s.Layer).ToList();
        }
        if (design.SetBonusLists is not null || design.SetBonusMask.HasValue)
        {
            byte mask = design.SetBonusMask ?? item.SetItemMask ?? 0;
            if (item.Quality != ItemQuality.Set || mask > 31 || design.SetBonusLists is null || design.SetBonusLists.Count != System.Numerics.BitOperations.PopCount(mask) || design.SetBonusLists.Any(l => l is null || l.Count > 256)) throw new ApiProblem("design_set_stats", L.T("Las listas parciales deben coincidir con los bits de la máscara de conjunto."));
            item.SetItemMask = mask; item.SetBonusStats = design.SetBonusLists.Select(l => l.Select(s => NativeStat(s, p)).OrderBy(s => (int)s.Id).ThenBy(s => s.Layer).ToList()).ToList();
        }
        factory.ApplyBaseAutoAffix(item);
        if (design.Ethereal) factory.MakeEthereal(item);
        if (design.Defense.HasValue) { if (!item.Defense.HasValue || design.Defense < 0) throw new ApiProblem("design_defense", L.T("Esta base no admite esa defensa.")); item.Defense = design.Defense; }
        if (design.MaxDurability.HasValue) { if (design.MaxDurability < 0 || design.MaxDurability > 255 || !item.MaxDurability.HasValue) throw new ApiProblem("design_durability", L.T("Durabilidad máxima fuera de rango.")); item.MaxDurability = design.MaxDurability; }
        if (design.Durability.HasValue) { if (design.Durability < 0 || design.Durability > item.MaxDurability) throw new ApiProblem("design_durability", L.T("La durabilidad supera la capacidad del objeto.")); item.Durability = design.Durability; }
        if (design.Quantity.HasValue) { if (V(baseRow, "stackable") != "1" || design.Quantity < 1 || design.Quantity > I(baseRow, "maxstack", 511)) throw new ApiProblem("design_quantity", L.T("Cantidad fuera de la capacidad nativa del objeto.")); item.Quantity = design.Quantity; }
        if (design.SocketChildren is null) throw new ApiProblem("design_sockets", L.T("La lista de engarces no puede ser nula."));
        int socketCount = design.SocketCount ?? Math.Max(item.Sockets.Count, design.SocketChildren.Count);
        if (socketCount < 0 || socketCount > I(baseRow, "gemsockets") || socketCount > 15 || design.SocketChildren.Count > socketCount) throw new ApiProblem("design_sockets", L.T("Los engarces no caben en la base seleccionada."));
        if (design.SocketChildren.TakeWhile(c => c is not null).Count() != design.SocketChildren.Count(c => c is not null)) throw new ApiProblem("design_socket_gap", L.T("Los engarces ocupados deben empezar por el primero y continuar sin huecos."));
        if (item.Flags.HasFlag(ItemFlags.Runeword))
        {
            if (socketCount != item.Sockets.Count || design.SocketChildren.Count > 0 && (design.SocketChildren.Count != item.Sockets.Count || design.SocketChildren.Select((child,index) => child is not null && child.BaseCode == item.Sockets[index]?.ItemCodeString && child.Quality == "normal" && !child.Ethereal && (child.Stats?.Count ?? 0) == 0).Any(ok => !ok))) throw new ApiProblem("design_runeword_sockets", L.T("La palabra rúnica conserva sus runas nativas y su orden."));
        }
        else if (socketCount > 0)
        {
            item.Flags |= ItemFlags.Socketed; item.Sockets = Enumerable.Repeat<Item?>(null, socketCount).ToList();
            for (int i = 0; i < design.SocketChildren.Count; i++)
            {
                var childDesign = design.SocketChildren[i]; if (childDesign is null) continue;
                var child = DesignItem(childDesign, p, version, depth + 1); string childType = V(p.Bases[child.ItemCodeString], "type");
                if (!(IsType(p, childType, "rune") || IsType(p, childType, "gem") || IsType(p, childType, "jewl")) || child.Sockets.Count > 0) throw new ApiProblem("design_socket_child", L.T("Sólo se pueden engarzar runas, gemas o joyas válidas."));
                child.Position = new() { Mode = ItemMode.Socketed, StorePage = StorePage.Null, InvX = (byte)i }; item.Sockets[i] = child;
            }
        }
        else { item.Flags &= ~ItemFlags.Socketed; item.Sockets.Clear(); }
        if (item.MaxDurability == 0) item.Durability = null;
        if (design.Identified) item.Flags |= ItemFlags.Identified; else item.Flags &= ~ItemFlags.Identified;
        if (design.PersonalizedName is not null)
        {
            if (design.PersonalizedName.Length > 15 || design.PersonalizedName.Any(c => c < 32 || c > 126)) throw new ApiProblem("design_personalized_name", L.T("La firma admite hasta15 caracteres ASCII imprimibles."));
            item.PersonalizedName = design.PersonalizedName; if (design.PersonalizedName.Length > 0) item.Flags |= ItemFlags.Personalized; else item.Flags &= ~ItemFlags.Personalized;
        }
        ValidateStatUniqueness(item);
        // Validate the actual native item encoding, including paired stats and socket children.
        var decoded = DeserializeItem(SerializeItem(item, p, version), p, version);
        static void CompareStats(IEnumerable<Stat> expected, IEnumerable<Stat> actual)
        {
            if (expected.Any(s => !actual.Any(a => a.Id == s.Id && a.Layer == s.Layer && a.Value == s.Value)) || actual.Any(a => a.Value != 0 && a.Id != StatId.PoisonCount && !expected.Any(s => s.Id == a.Id && s.Layer == a.Layer && s.Value == a.Value))) throw new ApiProblem("design_stat_loss", L.T("La codificación cambió una propiedad solicitada del diseño."));
        }
        if (item.RunewordStats is not null) CompareStats(item.RunewordStats, decoded.RunewordStats ?? []);
        for (int i = 0; i < item.SetBonusStats.Count; i++) CompareStats(item.SetBonusStats[i], decoded.SetBonusStats[i]);
        foreach (var stat in item.Stats)
            if (!decoded.Stats.Any(s => s.Id == stat.Id && s.Layer == stat.Layer && s.Value == stat.Value)) throw new ApiProblem("design_stat_loss", L.T("La codificación cambió una propiedad solicitada del diseño."));
        if (decoded.Stats.Any(s => s.Value != 0 && s.Id != StatId.PoisonCount && !item.Stats.Any(t => t.Id == s.Id && t.Layer == s.Layer && t.Value == s.Value)) || item.Defense != decoded.Defense || item.Durability != decoded.Durability || item.MaxDurability != decoded.MaxDurability || item.Quantity != decoded.Quantity || item.Sockets.Count != decoded.Sockets.Count || item.PersonalizedName != decoded.PersonalizedName)
            throw new ApiProblem("design_field_loss", L.T("La codificación nativa cambió un campo solicitado del diseño."));
        if (design.Stats is not null && design.Stats.Any(s => s.Id is 17 or 18) && !(design.Stats.Any(s => s.Id == 17) && design.Stats.Any(s => s.Id == 18))) throw new ApiProblem("design_paired_stat", L.T("El daño mejorado necesita las dos propiedades emparejadas17 y18."));
        return decoded;
    }
    private static Stat NativeStat(ItemStatSpec spec, GameData p)
    {
        if (!p.StatRows.TryGetValue(spec.Id, out var row) || spec.Id < 0 || spec.Id >= 511 || spec.Id == (int)StatId.NumSockets) throw new ApiProblem("design_stat", L.T("La propiedad no se puede guardar como modificador de objeto."));
        int bits = I(row, "Save Bits"), shift = I(row, "ValShift"), paramBits = I(row, "Save Param Bits");
        if (bits is <= 0 or > 32 || paramBits > 31 || spec.Layer < 0 || spec.Layer >= (paramBits == 0 ? 1L : 1L << paramBits) || spec.Value.HasValue == spec.RawValue.HasValue) throw new ApiProblem("design_stat_parameter", L.T("Indica value o rawValue y un parámetro nativo válido."));
        long raw;
        if (spec.RawValue.HasValue) raw = spec.RawValue.Value;
        else
        {
            double value = spec.Value!.Value; if (V(row, "Stat").Contains("_perlevel", StringComparison.Ordinal)) value *= 8;
            if (!double.IsFinite(value) || value != Math.Truncate(value) || value > long.MaxValue || value < long.MinValue) throw new ApiProblem("design_stat_value", L.T("La propiedad no admite esa fracción o valor."));
            raw = checked((long)value << shift);
        }
        long encoded = (raw >> shift) + I(row, "Save Add");
        if (encoded < 0 || encoded > (1L << bits) - 1 || raw != ((raw >> shift) << shift)) throw new ApiProblem("design_stat_range", L.T("El valor no cabe en la codificación nativa de ") + V(row, "Stat") + ".");
        return new() { Id = (StatId)spec.Id, Layer = spec.Layer, Value = raw };
    }
    private static Item ImportBlueprintItem(ItemBlueprintDocument document, GameData p, uint version)
    {
        if (document.SchemaVersion != 1 || document.SaveVersion != version || document.ProfileFingerprint != p.Fingerprint() || document.Data is null || Hash(document.Data) != document.DataHash) throw new ApiProblem("blueprint_profile", L.T("El plano no corresponde a la versión y tablas seleccionadas, o fue modificado."));
        var item = DeserializeItem(document.Data, p, version);
        if (item.ItemCodeString != document.Code) throw new ApiProblem("blueprint_code", L.T("La base del plano no coincide con sus datos."));
        return CopyItem(item, p, version);
    }
    public ItemBlueprintDocument ExportBlueprint(string sessionId, string itemId, string? planId = null)
    {
        var s = Session(sessionId); lock (s.Sync)
        {
            var save = Read(planId is null ? s.Original : Plan(s, planId).Bytes, s.Profile, s.Version);
            var plan = planId is null ? null : Plan(s, planId);
            var ids = new ItemIdentities(save, plan?.CharacterItemIds, plan?.MercItemIds); var item = ids.Resolve(itemId); var data = SerializeItem(item, s.Profile, s.Version);
            return new() { SaveVersion = s.Version, ProfileFingerprint = s.Profile.Fingerprint(), Code = item.ItemCodeString, Name = ItemName(item, s.Profile), Quality = item.Quality.ToString().ToLowerInvariant(), Data = data, DataHash = Hash(data), Description = JsonSerializer.SerializeToElement(ItemDto(item, s.Profile, itemId), ContractJson) };
        }
    }
}
