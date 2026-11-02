using System.Security.Cryptography;
using System.Text.Json;
using D2SSharp.Enums;
using D2SSharp.IO;
using D2SSharp.Model;
using D2RBuilder;
using static ForjaHoradrica.GameData;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private static string QualityName(ItemQuality quality) => (int)quality == 8 ? "crafted" : quality.ToString().ToLowerInvariant();
    private sealed class ItemIdentities
    {
        private readonly Dictionary<Item, string> ids = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, Item> items = new(StringComparer.Ordinal);
        private int next;
        public ItemIdentities(D2Save save, string[]? characterIds = null, string[]? mercIds = null) { for (int i = 0; i < save.Items.Count; i++) Add(save.Items[i], characterIds is not null && i < characterIds.Length ? characterIds[i] : "i" + i); if (save.MercItems is not null) for (int i = 0; i < save.MercItems.Items.Count; i++) Add(save.MercItems.Items[i], mercIds is not null && i < mercIds.Length ? mercIds[i] : "m" + i); }
        public string Id(Item item) { if (!ids.TryGetValue(item, out var id)) { do { id = "n" + next++; } while (items.ContainsKey(id)); Add(item, id); } return id; }
        public void Add(Item item, string? id = null) { if (id is null) { Id(item); return; } if (id.Length > 100 || id.Length == 0 || items.ContainsKey(id)) throw new ApiProblem("item_id_duplicate", L.T("El identificador del nuevo objeto está vacío o repetido: ") + id); ids[item] = id; items[id] = item; }
        public Item Resolve(string id) => items.TryGetValue(id, out var item) ? item : throw new ApiProblem("item_missing", L.T("El objeto no existe en este borrador: ") + id);
        public void Remove(Item item) { if (ids.Remove(item, out var id)) items.Remove(id); }
        public ItemIdentities Rebind(D2Save before, D2Save after)
        {
            var result = new ItemIdentities(after); result.ids.Clear(); result.items.Clear();
            for (int i = 0; i < before.Items.Count; i++) result.Add(after.Items[i], Id(before.Items[i]));
            if (before.MercItems is not null && after.MercItems is not null) for (int i = 0; i < before.MercItems.Items.Count; i++) result.Add(after.MercItems.Items[i], Id(before.MercItems.Items[i]));
            return result;
        }
    }
    private static int ActiveSet(D2Save save) => (save.Character.WeaponSwitch & 1) == 0 ? 1 : 2;
    private static int? ItemWeaponSet(Item item, D2Save save, string owner) => owner == "mercenary" ? null : (int)item.Position.BodyLocation switch { 4 or 5 => ActiveSet(save), 11 or 12 => 3 - ActiveSet(save), _ => null };
    private static BodyLocation DestinationBody(ItemDestination destination, D2Save save)
    {
        var body = ParseSlot(destination.Slot ?? throw new ApiProblem("slot_required", L.T("Selecciona una ranura de equipo.")));
        if (destination.Owner == "character" && (int)body is 4 or 5 or 11 or 12)
        {
            int set = destination.WeaponSet ?? ((int)body >= 11 ? 3 - ActiveSet(save) : ActiveSet(save));
            if (set is not (1 or 2)) throw new ApiProblem("weapon_set", L.T("El conjunto de armas debe ser I o II."));
            bool shield = (int)body is 5 or 12; return (BodyLocation)(set == ActiveSet(save) ? shield ? 5 : 4 : shield ? 12 : 11);
        }
        return body;
    }
    private static bool IsType(GameData p, string actual, string expected, HashSet<string>? seen = null)
    {
        if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) return true;
        seen ??= new(StringComparer.OrdinalIgnoreCase); if (!seen.Add(actual)) return false;
        var row = p.Read("itemtypes.txt").FirstOrDefault(r => V(r, "Code").Equals(actual, StringComparison.OrdinalIgnoreCase));
        return row is not null && new[] { V(row, "Equiv1"), V(row, "Equiv2") }.Any(t => t.Length > 0 && IsType(p, t, expected, seen));
    }
    private static bool TwoHanded(Item item, GameData p, bool canOneHand = false) => p.Bases.TryGetValue(item.ItemCodeString, out var row) && V(row, "2handed") == "1" && !(canOneHand && V(row, "1or2handed") == "1");
    private static bool AllowedTwoHandOffhand(Item main, Item offhand, GameData p, D2Save save, string owner)
    {
        if (owner != "character") return false;
        string mainType = V(p.Bases[main.ItemCodeString], "type"), offType = V(p.Bases[offhand.ItemCodeString], "type");
        if (IsType(p, mainType, "bow") && IsType(p, offType, "bowq") || IsType(p, mainType, "xbow") && IsType(p, offType, "xboq")) return true;
        string restriction = V(p.Character(save), "TwoHandedOffHandRestrictItemType");
        return restriction.Length > 0 && IsType(p, offType, restriction);
    }
    private static void ValidateClass(Item item, GameData p, D2Save save, string owner)
    {
        if (owner != "character") return;
        string classCode = p.Read("skills.txt").Where(r => I(r, "*Id", -1) == save.Skills.GetSkillId(0)).Select(r => V(r, "charclass")).FirstOrDefault() ?? "";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? Restriction(string type)
        {
            if (!seen.Add(type)) return null; var row = p.Read("itemtypes.txt").FirstOrDefault(r => V(r, "Code") == type); if (row is null) return null;
            if (V(row, "Class").Length > 0) return V(row, "Class");
            return new[] { V(row, "Equiv1"), V(row, "Equiv2") }.Where(t => t.Length > 0).Select(Restriction).FirstOrDefault(c => !string.IsNullOrEmpty(c));
        }
        string? required = Restriction(V(p.Bases[item.ItemCodeString], "type"));
        if (!string.IsNullOrWhiteSpace(required) && !required.Equals(classCode, StringComparison.OrdinalIgnoreCase)) throw new ApiProblem("item_class", L.T("El objeto está restringido a otra clase de personaje."));
    }
    private static string[] EquipSlots(Item item, GameData p, string owner, D2Save save)
    {
        string type = V(p.Bases[item.ItemCodeString], "type"); bool Match(string t) => IsType(p, type, t);
        if (Match("helm")) return ["helm"]; if (Match("tors")) return ["armor"]; if (Match("amul")) return ["amulet"]; if (Match("ring")) return ["ring1", "ring2"];
        if (Match("boot")) return ["boots"]; if (Match("glov")) return ["gloves"]; if (Match("belt")) return ["belt"]; if (Match("shld") || Match("bowq") || Match("xboq")) return ["shield"];
        if (Match("weap")) return owner == "character" && (save.Character.Class.ToString() == "Barbarian" || save.Character.Class.ToString() == "Assassin" && Match("h2h")) ? ["weapon", "shield"] : ["weapon"];
        return [];
    }
    private static void SwitchWeaponSet(D2Save save, int? requested, List<ChangeEntry> diff)
    {
        if (!requested.HasValue || requested == ActiveSet(save)) return;
        if (requested is not (1 or 2)) throw new ApiProblem("weapon_set", L.T("El conjunto de armas debe ser I o II."));
        int before = ActiveSet(save);
        foreach (var item in save.Items.Where(i => i.Position.Mode == ItemMode.Equipped)) item.Position.BodyLocation = (BodyLocation)((int)item.Position.BodyLocation switch { 4 => 11, 5 => 12, 11 => 4, 12 => 5, var x => x });
        (save.Character.LeftSkill, save.Character.SwitchLeftSkill) = (save.Character.SwitchLeftSkill, save.Character.LeftSkill);
        (save.Character.RightSkill, save.Character.SwitchRightSkill) = (save.Character.SwitchRightSkill, save.Character.RightSkill);
        save.Character.WeaponSwitch = (save.Character.WeaponSwitch & ~1u) | (uint)(requested - 1);
        diff.Add(new("equipment", L.T("Conjunto de armas activo"), before, requested));
    }
    private static byte[] SerializeItem(Item item, GameData profile, uint version)
    {
        var buffer = new byte[1024 * 1024]; var writer = new BitWriter(buffer); item.Write(ref writer, profile.External(version), version); return buffer[..writer.BytesWritten];
    }
    private static Item DeserializeItem(byte[] bytes, GameData profile, uint version)
    {
        if (bytes.Length is < 4 or > 1024 * 1024) throw new ApiProblem("blueprint_size", L.T("El objeto supera el tamaño admitido."));
        var reader = new BitReader(bytes); var item = Item.Read(ref reader, profile.External(version), version);
        if (!bytes.AsSpan().SequenceEqual(SerializeItem(item, profile, version))) throw new ApiProblem("blueprint_roundtrip", L.T("El objeto no conserva su codificación nativa."));
        ValidateStatUniqueness(item); return item;
    }
    private static Item CopyItem(Item item, GameData p, uint version)
    {
        var copy = DeserializeItem(SerializeItem(item, p, version), p, version);
        void Reseed(Item child) { uint original=child.ItemSeed,value;do{value=BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));}while(value==0||value==original);child.ItemSeed=value;foreach (var socket in child.Sockets) if (socket is not null) Reseed(socket); }
        Reseed(copy); return copy;
    }
    private static Item MakeGear(GearChange gear, GameData p, uint version)
    {
        var factory = new ItemFactory(p.ExcelPath) { AllowNonSpawnableUniques = true, StringsDirectory = p.StringsPath, ItemFormatVersion = version >= 105 ? (ushort)101 : (ushort)Math.Min(version, 101u) };
        Item item = gear.Type.ToLowerInvariant() switch { "unique" => factory.CreateUnique(gear.Name, gear.Level), "set" => factory.CreateSet(gear.Name, gear.Level), "runeword" => factory.CreateRuneword(gear.Name, gear.BaseCode ?? throw new ApiProblem("base_required", L.T("Selecciona una base.")), gear.Level), "magic" => factory.CreateMagic(gear.BaseCode ?? throw new ApiProblem("base_required", L.T("Selecciona una base.")), gear.Prefix ?? "", gear.Suffix, gear.Level), "base" => factory.CreateNormal(gear.BaseCode ?? gear.Name, gear.Level), _ => throw new ApiProblem("item_type", L.T("Tipo de objeto no admitido.")) };
        foreach (var rune in gear.Sockets) factory.AddRuneSocket(item, rune); if (gear.Ethereal) factory.MakeEthereal(item); return item;
    }
    private static void DetachItem(D2Save save, Item item) { save.Items.Remove(item); save.MercItems?.Items.Remove(item); }
    private static void PlaceItem(D2Save save, Item item, ItemDestination d, GameData p)
    {
        if (d.Owner is not ("character" or "mercenary")) throw new ApiProblem("item_owner", L.T("El destino debe ser el personaje o el mercenario."));
        if (d.Owner == "mercenary" && (!save.Character.MercData.HasMerc || d.Page != "equipped")) throw new ApiProblem("mercenary_destination", L.T("Contrata un mercenario y selecciona una ranura de su equipo."));
        DetachItem(save, item);
        if (d.Page == "equipped")
        {
            var body = DestinationBody(d, save); string slot = Slot(new Item { Position = new() { BodyLocation = body } });
            ValidateClass(item, p, save, d.Owner);
            if (!EquipSlots(item, p, d.Owner, save).Contains(slot)) throw new ApiProblem("item_slot", L.T("El objeto no corresponde a esa ranura de equipo."));
            bool extendedMerc = p.StatRows.Values.Any(r => V(r, "Stat") == "charm_weight");
            if (d.Owner == "mercenary" && !extendedMerc && body is not (BodyLocation.Head or BodyLocation.Torso or BodyLocation.RightArm or BodyLocation.LeftArm)) throw new ApiProblem("mercenary_slot", L.T("Este perfil no admite esa ranura de mercenario."));
            var collection = d.Owner == "character" ? save.Items : (save.MercItems ??= new()).Items;
            foreach (var old in collection.Where(i => i.Position.Mode == ItemMode.Equipped && i.Position.BodyLocation == body).ToList()) { DetachItem(save, old); Store(save, old, StorePage.Stash, p); save.Items.Add(old); }
            bool mainHand = (int)body is 4 or 11;
            BodyLocation opposite = (BodyLocation)((int)body switch { 4 => 5, 5 => 4, 11 => 12, 12 => 11, _ => 0 });
            if (opposite != BodyLocation.None)
            {
                bool newTwoHand = mainHand && TwoHanded(item, p, d.Owner == "character" && save.Character.Class.ToString() == "Barbarian");
                foreach (var old in collection.Where(i => i.Position.Mode == ItemMode.Equipped && i.Position.BodyLocation == opposite).ToList())
                    if (newTwoHand && !AllowedTwoHandOffhand(item, old, p, save, d.Owner) || !mainHand && TwoHanded(old, p, d.Owner == "character" && save.Character.Class.ToString() == "Barbarian") && !AllowedTwoHandOffhand(old, item, p, save, d.Owner)) { DetachItem(save, old); Store(save, old, StorePage.Stash, p); save.Items.Add(old); }
            }
            item.Position = new() { Mode = ItemMode.Equipped, StorePage = StorePage.Null, BodyLocation = body, InvX = (byte)body }; collection.Add(item); return;
        }
        if (d.Page == "belt")
        {
            string type = V(p.Bases[item.ItemCodeString], "type"); if (!IsType(p, type, "potn")) throw new ApiProblem("belt_item", L.T("El cinturón sólo admite pociones."));
            int x = d.X ?? Enumerable.Range(0, BeltSlots(save, p)).FirstOrDefault(n => !save.Items.Any(i => i.Position.Mode == ItemMode.InBelt && i.Position.InvX == n), -1);
            if (x < 0 || x >= BeltSlots(save, p) || d.Y.GetValueOrDefault() != 0 || save.Items.Any(i => i.Position.Mode == ItemMode.InBelt && i.Position.InvX == x)) throw new ApiProblem("belt_position", L.T("La posición del cinturón está ocupada o fuera de sus límites."));
            item.Position = new() { Mode = ItemMode.InBelt, StorePage = StorePage.Null, InvX = (byte)x }; save.Items.Add(item); return;
        }
        StorePage page = d.Page switch { "inventory" => StorePage.Inventory, "stash" => StorePage.Stash, "cube" => StorePage.Cube, _ => throw new ApiProblem("item_page", L.T("Contenedor de destino desconocido.")) };
        if (d.X.HasValue != d.Y.HasValue) throw new ApiProblem("item_coordinates", L.T("Indica ambas coordenadas o deja que AKARA busque espacio."));
        if (d.X.HasValue) { if (d.X < 0 || d.X > 255 || d.Y < 0 || d.Y > 255) throw new ApiProblem("item_coordinates", L.T("Las coordenadas están fuera de rango.")); item.Position = new() { Mode = ItemMode.Stored, StorePage = page, InvX = (byte)d.X.Value, InvY = (byte)d.Y!.Value }; }
        else Store(save, item, page, p); save.Items.Add(item); ValidatePlacement(save, p);
    }
    private static int BeltSlots(D2Save save, GameData p)
    {
        int[] slots = [12, 8, 4, 16, 8, 12, 16]; var belt = save.Items.FirstOrDefault(i => i.Position.Mode == ItemMode.Equipped && i.Position.BodyLocation == BodyLocation.Belt);
        int index = belt is null ? 2 : I(p.Bases[belt.ItemCodeString], "belt", 2); return index >= 0 && index < slots.Length ? slots[index] : throw new ApiProblem("belt_layout", L.T("Distribución de cinturón no admitida."));
    }
    private static void ApplyItemOperations(D2Save save, GameData p, PreviewRequest request, ItemIdentities identities, List<ChangeEntry> diff)
    {
        if ((request.ItemOperations?.Count ?? 0) > 1000) throw new ApiProblem("operations_limit", L.T("Demasiadas operaciones en el borrador."));
        foreach (var op in request.ItemOperations ?? [])
        {
            if (op is null) throw new ApiProblem("operation_invalid", L.T("La operación de objeto está vacía."));
            Item item;
            if (op.Operation == "create")
            {
                if ((op.Gear is null ? 0 : 1) + (op.Design is null ? 0 : 1) + (op.Blueprint is null ? 0 : 1) != 1) throw new ApiProblem("item_create", L.T("La creación necesita exactamente gear, design o blueprint."));
                item = op.Gear is not null ? MakeGear(op.Gear, p, save.Version) : op.Design is not null ? DesignItem(op.Design, p, save.Version) : ImportBlueprintItem(op.Blueprint!, p, save.Version);
                identities.Add(item, op.ClientId); PlaceItem(save, item, op.Destination ?? new(), p); diff.Add(new("items", L.T("Crear objeto"), null, ItemName(item, p))); continue;
            }
            item = identities.Resolve(op.ItemId);
            switch (op.Operation)
            {
                case "edit":
                    if (op.Design is null) throw new ApiProblem("item_edit", L.T("La edición necesita un diseño completo del objeto."));
                    bool merc = save.MercItems?.Items.Contains(item) == true;
                    var existingDestination = new ItemDestination { Owner = merc ? "mercenary" : "character", Page = item.Position.Mode == ItemMode.Equipped ? "equipped" : item.Position.Mode == ItemMode.InBelt ? "belt" : item.Position.StorePage.ToString().ToLowerInvariant(), Slot = Slot(item), WeaponSet = ItemWeaponSet(item, save, merc ? "mercenary" : "character"), X = item.Position.InvX, Y = item.Position.InvY };
                    var replacement = DesignItem(op.Design, p, save.Version); replacement.ItemSeed = item.ItemSeed; string originalName = ItemName(item,p);
                    DetachItem(save,item); identities.Remove(item); identities.Add(replacement,op.ItemId); PlaceItem(save,replacement,op.Destination ?? existingDestination,p); diff.Add(new("items",L.T("Editar objeto"),originalName,ItemName(replacement,p))); break;
                case "move": case "equip": PlaceItem(save, item, op.Destination ?? throw new ApiProblem("destination_required", L.T("Selecciona un destino.")), p); diff.Add(new("items", L.T("Mover objeto"), ItemName(item, p), op.Destination)); break;
                case "copy": var copy = CopyItem(item, p, save.Version); identities.Add(copy, op.ClientId); PlaceItem(save, copy, op.Destination ?? new(), p); diff.Add(new("items", L.T("Copiar objeto"), ItemName(item, p), ItemName(copy, p))); break;
                case "delete": DetachItem(save, item); identities.Remove(item); diff.Add(new("items", L.T("Eliminar objeto"), ItemName(item, p), null)); break;
                default: throw new ApiProblem("item_operation", L.T("Operación de objeto desconocida: ") + op.Operation);
            }
        }
    }
}
