using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using D2SSharp.Enums;
using D2SSharp.Model;
using D2RBuilder;
using static ForjaHoradrica.GameData;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private readonly ConcurrentDictionary<string, GameData> profiles = new();
    private readonly ConcurrentDictionary<string, SaveSession> sessions = new();
    private readonly string dataRoot;
    private readonly string saveRoot;
    private readonly Func<bool> gameState;
    public EditorService(string? storageRoot = null, Func<bool>? gameState = null, string? saveGamesRoot = null)
    {
        this.gameState = gameState ?? GameRunning;
        dataRoot = Path.GetFullPath(storageRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AKARA"));
        saveRoot = Path.GetFullPath(saveGamesRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games", "Diablo II Resurrected"));
        var native = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Diablo II Resurrected", "mods", "Reimagined", "Reimagined.mpq", "data", "global", "excel");
        if (Directory.Exists(native)) { try { var p = new GameData(native); profiles.TryAdd(p.Id, p); } catch (Exception) { /* A broken optional auto-detected profile must not prevent opening the application. */ } }
    }
    public static bool GameRunning() => Process.GetProcessesByName("D2R").Length > 0;
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public object Profiles() => new { profiles = profiles.Values.Select(p => p.Info()) };
    public object AddProfile(ProfileRequest request)
    {
        var p = new GameData(request.ExcelPath, request.StringsPath, request.Name); profiles[p.Id] = p; return p.Info();
    }
    public object Bootstrap()
    {
        var defaultProfile = profiles.Values.FirstOrDefault();
        var files = new List<object>();
        if (Directory.Exists(saveRoot))
        {
            var dirs = new List<string> { saveRoot };
            string mods = Path.Combine(saveRoot, "mods");
            if (Directory.Exists(mods)) dirs.AddRange(Directory.EnumerateDirectories(mods).Take(40));
            foreach (var dir in dirs)
                foreach (var path in Directory.EnumerateFiles(dir, "*.d2s").Take(100)) files.Add(new { path, name = Path.GetFileNameWithoutExtension(path), profileId = path.Contains("Reimagined", StringComparison.OrdinalIgnoreCase) ? defaultProfile?.Id : null });
        }
        return new { appName = "AKARA", version = typeof(EditorService).Assembly.GetName().Version?.ToString(3) ?? "0.2.0", gameRunning = gameState(), profiles = profiles.Values.Select(p => p.Info()), saves = files, saveDirectories = SaveDirectories(), presets = defaultProfile is null ? [] : PresetEntries(defaultProfile, false), defaultProfileId = defaultProfile?.Id };
    }
    private SaveSession Session(string id) => !string.IsNullOrWhiteSpace(id) && sessions.TryGetValue(id, out var s) ? s : throw new ApiProblem("session_missing", "La sesión ya no existe. Abre de nuevo la partida.", 404);
    private static byte[] ReadSave(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Path.GetExtension(path).Equals(".d2s", StringComparison.OrdinalIgnoreCase)) throw new ApiProblem("path_invalid", "Selecciona la ruta completa de un archivo.d2s.");
        var info = new FileInfo(Path.GetFullPath(path));
        if (!info.Exists) throw new ApiProblem("save_missing", "No existe la partida seleccionada.", 404);
        if (info.Length < 400 || info.Length > 16 * 1024 * 1024) throw new ApiProblem("save_size", "El tamaño de la partida no es válido.");
        return File.ReadAllBytes(info.FullName);
    }
    private static D2Save Read(byte[] bytes, GameData profile, uint version) => D2Save.Read(bytes, profile.External(version));
    public object Open(OpenRequest request)
    {
        var p = request.ProfileId is not null && profiles.TryGetValue(request.ProfileId, out var selected) ? selected : profiles.Values.FirstOrDefault() ?? throw new ApiProblem("profile_required", "Selecciona la carpeta de tablas del juego o del mod.");
        if (request.ProfileId is not null && !profiles.ContainsKey(request.ProfileId)) throw new ApiProblem("profile_missing", "El perfil seleccionado ya no está disponible. Elige otro perfil.");
        p.EnsureUnchanged();
        var bytes = ReadSave(request.Path);
        if (BitConverter.ToUInt32(bytes, 0) != D2Save.Magic) throw new ApiProblem("save_signature", "El archivo no es una partida de Diablo II.");
        uint version = BitConverter.ToUInt32(bytes, 4);
        if (version < 97 || version > 105) throw new ApiProblem("save_version", "Se admiten partidas D2R de versión97 a105.");
        if (!D2Save.VerifyChecksum(bytes)) throw new ApiProblem("save_checksum", "El checksum de la partida no es válido. Usa una copia de respaldo.");
        var save = Read(bytes, p, version);
        bool exact = bytes.AsSpan().SequenceEqual(save.ToBytes(p.External(version), version));
        var session = new SaveSession { Id = Guid.NewGuid().ToString("N"), SourcePath = Path.GetFullPath(request.Path), Original = bytes, Hash = Hash(bytes), Profile = p, Version = version, Roundtrip = exact };
        if (sessions.Count >= 100) throw new ApiProblem("session_limit", "Cierra la aplicación y vuelve a abrirla para liberar sesiones antiguas.");
        sessions[session.Id] = session;
        return Inspect(session, save);
    }
    public object InspectSession(string id) { var s = Session(id); lock (s.Sync) return Inspect(s, Read(s.Original, s.Profile, s.Version)); }
    private static string Slot(Item i) => i.Position.BodyLocation.ToString() switch { "RightArm" or "RightHand" => "weapon", "LeftArm" or "LeftHand" => "shield", "Head" => "helm", "Torso" => "armor", "Neck" => "amulet", "RightRing" => "ring1", "LeftRing" => "ring2", "Feet" => "boots", "Gloves" => "gloves", "Belt" => "belt", _ => "" };
    private static object[] SkillDtos(D2Save save, GameData p)
    {
        var rows = p.Read("skills.txt"); var desc = File.Exists(Path.Combine(p.ExcelPath, "skilldesc.txt")) ? p.Read("skilldesc.txt") : [];
        var result = new List<object>();
        for (int n = 0; n < save.Skills.SkillLevels.Length; n++)
        {
            int id = save.Skills.GetSkillId(n); var row = rows.FirstOrDefault(r => I(r, "*Id", -1) == id);
            if (row is null) continue;
            var d = desc.FirstOrDefault(r => V(r, "skilldesc") == V(row, "skilldesc"));
            int[] prerequisites = new[] { "reqskill1", "reqskill2", "reqskill3" }.Select(k => V(row, k)).Where(v => v.Length > 0).Select(name => rows.FirstOrDefault(r => V(r, "skill") == name)).Where(r => r is not null).Select(r => I(r!, "*Id")).ToArray();
            result.Add(new { id, name = V(row, "skill"), points = (int)save.Skills.SkillLevels[n], maxPoints = I(row, "maxlvl", 20), requiredLevel = I(row, "reqlevel"), prerequisites, tabId = d is null ? 0 : I(d, "skillpage"), row = d is null ? 0 : I(d, "skillrow"), column = d is null ? 0 : I(d, "skillcolumn") });
        }
        return result.ToArray();
    }
    private static string ItemName(Item item, GameData p)
    {
        int id = Convert.ToInt32(item.QualityData?.FileIndex ?? 0);
        if (item.Quality == ItemQuality.Unique) return p.Read("uniqueitems.txt").Where(r => I(r, "*ID", -1) == id).Select(r => V(r, "index")).FirstOrDefault() ?? item.ItemCodeString;
        if (item.Quality == ItemQuality.Set) return p.Read("setitems.txt").Where(r => I(r, "*ID", -1) == id).Select(r => V(r, "index")).FirstOrDefault() ?? item.ItemCodeString;
        string baseName = p.Bases.TryGetValue(item.ItemCodeString, out var row) ? V(row, "name") : item.ItemCodeString;
        if (item.Flags.HasFlag(ItemFlags.Runeword) && item.RunewordId.HasValue)
        {
            string file = Path.Combine(p.StringsPath, "item-runes.json");
            if (File.Exists(file))
            {
                using var strings = JsonDocument.Parse(File.ReadAllText(file));
                var match = strings.RootElement.EnumerateArray().FirstOrDefault(e => e.TryGetProperty("id", out var v) && v.GetInt32() == item.RunewordId.Value);
                if (match.ValueKind == JsonValueKind.Object)
                {
                    string? key = match.GetProperty("Key").GetString(); var rune = p.Read("runes.txt").FirstOrDefault(r => V(r, "Name") == key);
                    if (rune is not null) return V(rune, "*Rune Name") + " · " + baseName;
                }
            }
        }
        if (item.QualityData is MagicQualityData magic)
        {
            var prefix = p.Read("magicprefix.txt"); var suffix = p.Read("magicsuffix.txt");
            if (magic.PrefixId > 0 && magic.PrefixId <= prefix.Count) baseName = V(prefix[magic.PrefixId - 1], "name") + " " + baseName;
            if (magic.SuffixId > 0 && magic.SuffixId <= suffix.Count) baseName += " " + V(suffix[magic.SuffixId - 1], "name");
        }
        return baseName;
    }
    private static object[] StatDtos(IEnumerable<Stat> stats, GameData p) => stats.Select(s =>
    {
        var row = p.StatRows.GetValueOrDefault((int)s.Id); string name = row is null ? s.Id.ToString() : V(row, "Stat");
        var labels = new Dictionary<string, string> { ["strength"] = "Fuerza", ["energy"] = "Energía", ["dexterity"] = "Destreza", ["vitality"] = "Vitalidad", ["maxhp"] = "Vida", ["maxmana"] = "Maná", ["fireresist"] = "Resistencia al fuego", ["lightresist"] = "Resistencia a rayos", ["coldresist"] = "Resistencia al frío", ["poisonresist"] = "Resistencia al veneno", ["item_fastercastrate"] = "Velocidad de lanzamiento", ["item_fastergethitrate"] = "Recuperación de impacto", ["item_fastermovevelocity"] = "Velocidad de carrera", ["item_magicbonus"] = "Hallazgo mágico", ["item_goldbonus"] = "Oro encontrado", ["item_allskills"] = "Todas las habilidades", ["item_armor_percent"] = "Defensa mejorada", ["armorclass"] = "Defensa", ["item_hp_perlevel"] = "Vida por nivel", ["item_mana_perlevel"] = "Maná por nivel", ["item_maxmana_percent"] = "Maná máximo", ["charm_weight"] = "Peso del talismán", ["torch_weight"] = "Peso de antorcha", ["soulstone_weight"] = "Peso de Annihilus" };
        string label = labels.GetValueOrDefault(name, name.Replace("item_", "").Replace('_', ' '));
        double displayValue = s.Value / Math.Pow(2, row is null ? 0 : I(row, "ValShift"));
        if (name.Contains("_perlevel", StringComparison.Ordinal)) displayValue /= 8;
        if (name is "item_singleskill" or "item_nonclassskill") label = "Habilidad: " + p.Read("skills.txt").Where(x => I(x, "*Id", -1) == s.Layer).Select(x => V(x, "skill")).FirstOrDefault();
        if (name == "item_skillongethit") label = "Al recibir un golpe: " + p.Read("skills.txt").Where(x => I(x, "*Id", -1) == s.Layer >> 6).Select(x => V(x, "skill")).FirstOrDefault() + " (nivel" + (s.Layer & 63) + ")";
        bool hidden = name == "item_nonclassskill" && s.Layer == 449;
        return (object)new { id = (int)s.Id, name, label, layer = s.Layer, value = s.Value, displayValue, hidden, saveBits = row is null ? 0 : I(row, "Save Bits"), paramBits = row is null ? 0 : I(row, "Save Param Bits") };
    }).ToArray();
    private static object ItemDto(Item item, GameData p, string id, string owner = "character", D2Save? save = null)
    {
        var size = p.Size(item);
        string name = ItemName(item, p), baseName = p.Bases.TryGetValue(item.ItemCodeString, out var br) ? V(br, "name") : item.ItemCodeString;
        return new { id, owner, code = item.ItemCodeString, name, baseName, runewordName = item.Flags.HasFlag(ItemFlags.Runeword) ? name.Split(" · ")[0] : null, quality = QualityName(item.Quality), type = QualityName(item.Quality), level = item.ItemLevel, identified = item.Flags.HasFlag(ItemFlags.Identified), ethereal = item.Flags.HasFlag(ItemFlags.Ethereal), personalizedName = item.PersonalizedName, defense = item.Defense, prefixIds = item.QualityData is MagicQualityData mq ? new[]{(int)mq.PrefixId}.Where(n=>n>0) : item.QualityData is RareCraftQualityData rq ? rq.Prefixes.Select(n=>(int)n).Where(n=>n>0) : [], suffixIds = item.QualityData is MagicQualityData ms ? new[]{(int)ms.SuffixId}.Where(n=>n>0) : item.QualityData is RareCraftQualityData rs ? rs.Suffixes.Select(n=>(int)n).Where(n=>n>0) : [], rarePrefixId = item.QualityData is RareCraftQualityData rn ? rn.RarePrefixId : 0, rareSuffixId = item.QualityData is RareCraftQualityData rf ? rf.RareSuffixId : 0, itemType = GameData.V(p.Bases[item.ItemCodeString], "type"), requirements = Requirements(item,p), slot = Slot(item), weaponSet = save is null ? null : ItemWeaponSet(item, save, owner), twoHanded = TwoHanded(item, p, owner == "character" && save?.Character.Class.ToString() == "Barbarian"), equipSlots = save is null ? [] : EquipSlots(item, p, owner, save), page = item.Position.Mode == ItemMode.Equipped ? "equipped" : item.Position.Mode == ItemMode.InBelt ? "belt" : item.Position.Mode == ItemMode.Socketed ? "socket" : item.Position.StorePage.ToString().ToLowerInvariant(), x = item.Position.InvX, y = item.Position.InvY, width = size.Width, height = size.Height, quantity = item.Quantity, durability = item.Durability, maxDurability = item.MaxDurability, stats = StatDtos(item.Stats, p), setBonusMask = item.SetItemMask, setBonusLists = item.SetBonusStats.Select(s => StatDtos(s, p)), runewordId = item.RunewordId, runewordStats = item.RunewordStats is null ? [] : StatDtos(item.RunewordStats, p), sockets = item.Sockets.Select((s, n) => s is null ? null : ItemDto(s, p, id + "/s" + n, owner, save)) };
    }
    private static object Inspect(SaveSession s, D2Save save, ItemIdentities? identities = null)
    {
        long Stat(int id) => save.Stats.GetStat((StatId)id);
        var grid = s.Profile.Grid(save, "inventory"); var stash = s.Profile.Grid(save, "stash"); var cube = s.Profile.Grid(save,"cube");
        var chr = s.Profile.Character(save);
        return new
        {
            sessionId = s.Id, sourceHash = s.Hash, sourcePath = s.IsNew ? null : s.SourcePath, isNew = s.IsNew, activeWeaponSet = ActiveSet(save), profileId = s.Profile.Id, fileName = Path.GetFileName(s.SourcePath),
            character = new
            {
                name = s.Version >= 104 ? save.Character.Preview.Name : save.Character.Name,
                classId = (int)save.Character.Class, className = save.Character.Class.ToString(), level = (int)save.Character.Level,
                experience = Stat(13), gameVersion = (int)save.Character.Preview.GameVersion,
                status = new { local = !save.Character.Flags.HasFlag(CharacterFlags.RealmChar), hardcore = save.Character.Flags.HasFlag(CharacterFlags.Hardcore), dead = save.Character.Flags.HasFlag(CharacterFlags.Dead), ladder = save.Character.Flags.HasFlag(CharacterFlags.Ladder) },
                attributes = new { strength = Stat(0), dexterity = Stat(2), vitality = Stat(3), energy = Stat(1), gold = Stat(14), stashGold = Stat(15), unspentStats = Stat(4), unspentSkills = Stat(5) },
                vitals = new { life = Stat(6) / 256.0, maxLife = Stat(7) / 256.0, mana = Stat(8) / 256.0, maxMana = Stat(9) / 256.0 }
            },
            progression = new
            {
                statsPerLevel = I(chr, "StatPerLevel", 5), skillsPerLevel = I(chr, "SkillsPerLevel", 1),
                maxLevel = I(s.Profile.Read("experience.txt").First(x => V(x, "Level") == "MaxLvl"), save.Character.Class.ToString(), 99),
                experienceMinimum = ExperienceBounds(s.Profile,save.Character.Class.ToString(),save.Character.Level).Minimum, experienceMaximum = ExperienceBounds(s.Profile,save.Character.Class.ToString(),save.Character.Level).Maximum,
                baseAttributes = new { strength = I(chr, "str"), dexterity = I(chr, "dex"), vitality = I(chr, "vit"), energy = I(chr, "int") }
            },
            skills = SkillDtos(save, s.Profile), items = save.Items.Select((item, index) => ItemDto(item, s.Profile, identities?.Id(item) ?? "i" + index, "character", save)),
            inventoryGrid = new { width = grid.Width, height = grid.Height }, stashGrid = new { width = stash.Width, height = stash.Height },
            cubeGrid = new { width = cube.Width,height=cube.Height },beltGrid = new {width=4,height=BeltSlots(save,s.Profile)/4},
            questRewards = new { skillPoints = CountSkillRewards(save), statPoints = CountStatRewards(save), anyaScrollsConsumed = Difficulties(save).Count(q => (q.ActV.PrisonOfIce & QuestFlags.Custom3) != 0) },
            hotkeys = save.Character.SkillHotkeys.Select((key, index) => new { index, skillId = key.SkillId, side=key.Side.ToString().ToLowerInvariant() }), leftSkill = save.Character.LeftSkill.SkillId, rightSkill = save.Character.RightSkill.SkillId,
            mercenary = MercenaryDto(save, s.Profile, identities),
            gameRunning = GameRunning(), validation = new { canEdit = s.Roundtrip, checksum = true, roundtrip = s.Roundtrip },
            warnings = s.Roundtrip ? Array.Empty<string>() : new[] { "La partida se puede inspeccionar, pero estas tablas no permiten conservarla exactamente. Elige el perfil correcto antes de editar." }
        };
    }
    public object Catalog(string sessionId, string type, string? search, int offset, int limit)
    {
        var s = Session(sessionId); var p = s.Profile; offset = Math.Max(0, offset); limit = Math.Clamp(limit, 1, 250);
        if (type is "stat" or "prefix" or "suffix" or "rareprefix" or "raresuffix" or "hireling" or "all-skills") return AdvancedCatalog(s, type, search, offset, limit);
        if (type == "skills") { var all = SkillDtos(Read(s.Original, p, s.Version), p); return new { entries = all, total = all.Length }; }
        IEnumerable<object> entries;
        IEnumerable<Dictionary<string, string>> rows;
        string file = type switch { "unique" => "uniqueitems.txt", "set" => "setitems.txt", "runeword" => "runes.txt", "base" => "", "prefix" => "magicprefix.txt", "suffix" => "magicsuffix.txt", "stat" => "itemstatcost.txt", _ => throw new ApiProblem("catalog_type", "Tipo de catálogo desconocido.") };
        rows = type == "base" ? p.Bases.Values : p.Read(file);
        string Name(Dictionary<string, string> r) => type switch { "base" => V(r, "name"), "runeword" => V(r, "*Rune Name").Length > 0 ? V(r, "*Rune Name") : V(r, "Name"), "prefix" or "suffix" => V(r, "name"), "stat" => V(r, "Stat"), _ => V(r, "index") };
        rows = rows.Where(r => Name(r).Length > 0 && (type != "unique" || V(r, "disabled") != "1") && (type != "runeword" || V(r, "complete") == "1"));
        if (!string.IsNullOrWhiteSpace(search)) rows = rows.Where(r => Name(r).Contains(search, StringComparison.OrdinalIgnoreCase) || V(r, "code").Contains(search, StringComparison.OrdinalIgnoreCase));
        var allRows = rows.OrderBy(Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var catalogSave = type == "base" ? Read(s.Original,p,s.Version) : null;
        bool Supported(Dictionary<string, string> row)
        {
            var codes = row.Where(kv => (kv.Key.StartsWith("prop", StringComparison.OrdinalIgnoreCase) || kv.Key.StartsWith("aprop", StringComparison.OrdinalIgnoreCase) || kv.Key.StartsWith("T1Code", StringComparison.OrdinalIgnoreCase) || kv.Key.StartsWith("mod", StringComparison.OrdinalIgnoreCase) && kv.Key.EndsWith("code", StringComparison.OrdinalIgnoreCase)) && kv.Value.Length > 0).Select(kv => kv.Value);
            var props = p.Read("properties.txt");
            return codes.All(code => { var prop = props.FirstOrDefault(r => V(r, "code") == code); return prop is not null && Enumerable.Range(1, 7).Where(n => V(prop, "func" + n).Length > 0).All(n => ItemFactory.SupportsPropertyFunction(I(prop, "func" + n))); });
        }
        entries = allRows.Skip(offset).Take(limit).Select(r => (object)new { id = type + ":" + (V(r, "*ID").Length > 0 ? V(r, "*ID") : type == "runeword" ? V(r, "Name") : V(r, "code").Length > 0 ? V(r, "code") : Name(r)), name = Name(r), code = type == "set" ? V(r, "item") : V(r, "code"), type, itemType = V(r,"type"), maxSockets = I(r,"gemsockets"), stackable = V(r,"stackable")=="1", maxStack = I(r,"maxstack"), socketable = type=="base"&&(IsType(p,V(r,"type"),"rune")||IsType(p,V(r,"type"),"gem")||IsType(p,V(r,"type"),"jewl")), equipSlots = type=="base"?EquipSlots(new Item{ItemCodeString=V(r,"code")},p,"character",catalogSave!):Array.Empty<string>(), width=I(r,"invwidth",1),height=I(r,"invheight",1), levelRequired = I(r, "lvl req", I(r, "levelreq")), supported = Supported(r), allowedTypes = Enumerable.Range(1, 6).Select(i => V(r, "itype" + i)).Where(v => v.Length > 0), properties = r.Where(kv => kv.Key.StartsWith("prop", StringComparison.OrdinalIgnoreCase) && kv.Value.Length > 0).Select(kv => kv.Value) });
        return new { entries, total = allRows.Length };
    }
    public object Preview(PreviewRequest request)
    {
        request = JsonSerializer.Deserialize<PreviewRequest>(JsonSerializer.Serialize(request,ContractJson),ContractJson) ?? throw new ApiProblem("request_invalid","El borrador está vacío.");
        if (request.Gear?.Any(g => g is null || g.Name is null || g.Type is null || g.Sockets is null || g.Sockets.Any(s => s is null)) == true || request.Skills?.Any(s => s is null) == true || request.ItemStats?.Any(s => s is null) == true || request.MoveToStashItemIds?.Any(s => s is null) == true)
            throw new ApiProblem("request_invalid", "La petición contiene una operación vacía o un campo obligatorio sin valor.");
        var s = Session(request.SessionId);
        lock (s.Sync)
        {
            s.Plan = null;
            var errors = new List<string>(); var warnings = new List<string>(); var diff = new List<ChangeEntry>();
            if (!s.Roundtrip) errors.Add("El perfil elegido no conserva los bytes de la partida original.");
            try
            {
                if (errors.Count > 0) throw new ApiProblem("roundtrip_required", errors[0]);
                s.Profile.EnsureUnchanged();
                var save = Read(s.Original, s.Profile, s.Version);
                var identities = new ItemIdentities(save);
                if(request.Loadout is not null) request = ExpandLoadout(request,save,s.Profile);
                if (request.Preset is not null) request = ApplyPreset(request, save, s.Profile);
                ApplyWorld(save, request.World, diff);
                SwitchWeaponSet(save, request.ActiveWeaponSet, diff);
                ApplyCharacter(save, s.Profile, request, diff);
                ApplyStatus(save,request.Status,diff);
                ApplyMercenary(save, s.Profile, request.Mercenary, diff);
                ApplyItems(save, s.Profile, request, diff, identities);
                ApplyItemOperations(save, s.Profile, request, identities, diff);
                ValidatePlacement(save, s.Profile);
                ValidateEquipment(save, s.Profile);
                var bytes = save.ToBytes(s.Profile.External(s.Version), s.Version);
                if (!D2Save.VerifyChecksum(bytes)) throw new ApiProblem("candidate_checksum", "La copia generada no tiene un checksum válido.");
                var readback = Read(bytes, s.Profile, s.Version);
                if (!bytes.AsSpan().SequenceEqual(readback.ToBytes(s.Profile.External(s.Version), s.Version))) throw new ApiProblem("candidate_roundtrip", "Los cambios no conservan una codificación binaria estable.");
                if (save.Items.Count != readback.Items.Count) throw new ApiProblem("candidate_items", "Cambió el número de objetos al validar la copia.");
                if ((save.MercItems?.Items.Count ?? 0) != (readback.MercItems?.Items.Count ?? 0)) throw new ApiProblem("candidate_merc_items", "Cambió el equipo del mercenario al validar la copia.");
                foreach (var item in readback.Items.Concat(readback.MercItems?.Items ?? [])) ValidateStatUniqueness(item);
                var characterIds = save.Items.Select(identities.Id).ToArray(); var mercIds = save.MercItems?.Items.Select(identities.Id).ToArray();
                var plan = new EditPlan(Guid.NewGuid().ToString("N"), bytes, diff, characterIds, mercIds); s.Plan = plan;
                warnings.Add("Los recursos mostrados son valores base; el juego calcula los bonus del equipo al cargar.");
                if (GameRunning()) warnings.Add("Diablo II está abierto. Puedes exportar una copia; cierra el juego antes de reemplazar la partida.");
                return new { planId = plan.Id, canSave = true, validation = new { errors, warnings }, diff, summary = Inspect(s, readback, new(readback, characterIds, mercIds)) };
            }
            catch (Exception ex) when (ex is ApiProblem or ArgumentException or InvalidDataException or NotSupportedException or InvalidOperationException or OverflowException)
            {
                if (errors.Count == 0) errors.Add(ex.Message);
                return new { planId = (string?)null, canSave = false, validation = new { errors, warnings }, diff, summary = (object?)null };
            }
        }
    }
    private static QuestsDifficulty[] Difficulties(D2Save s) => [s.Quests.Normal, s.Quests.Nightmare, s.Quests.Hell];
    private static bool Reward(QuestFlags f) => (f & QuestFlags.RewardGranted) != 0;
    private static QuestFlags Completed(QuestFlags f) => (f | QuestFlags.RewardGranted | QuestFlags.QuestLog | QuestFlags.CompletedBefore) & ~QuestFlags.RewardPending;
    private static int CountSkillRewards(D2Save s) => Difficulties(s).Sum(q => (Reward(q.ActI.DenOfEvil) ? 1 : 0) + (Reward(q.ActII.RadamentsLair) ? 1 : 0) + (Reward(q.ActIV.TheFallenAngel) ? 2 : 0));
    private static int CountStatRewards(D2Save s) => Difficulties(s).Count(q => Reward(q.ActIII.LamEsensTome)) * 5;
    private static void ApplyCharacter(D2Save s, GameData p, PreviewRequest r, List<ChangeEntry> diff)
    {
        long Stat(int id) => s.Stats.GetStat((StatId)id);
        void Set(int id, long value) { if (!p.StatRows.TryGetValue(id, out var row)) throw new ApiProblem("stat_missing", "No existe la estadística solicitada."); int bits = I(row, "CSvBits"); if (bits <= 0 || bits > 32 || value < 0 || value > ((1L << bits) - 1)) throw new ApiProblem("stat_range", "La estadística excede el rango guardable: " + V(row, "Stat")); if (Stat(id) != value) s.Stats.SetStat((StatId)id, value); }
        var chr = p.Character(s); int oldLevel = s.Character.Level, level = r.Level ?? (r.Experience.HasValue ? LevelForExperience(p,s.Character.Class.ToString(),r.Experience.Value) : oldLevel);
        long oldVit = Stat(3), oldEnergy = Stat(1);
        var maxRow = p.Read("experience.txt").First(x => V(x, "Level") == "MaxLvl");
        if (level < 1 || level > I(maxRow, s.Character.Class.ToString(), 99)) throw new ApiProblem("level_range", "El nivel excede el límite de la clase en este perfil.");
        if(r.Experience.HasValue){var bounds=ExperienceBounds(p,s.Character.Class.ToString(),level);if(r.Experience<bounds.Minimum||r.Experience>bounds.Maximum)throw new ApiProblem("experience_level","La experiencia no corresponde al nivel seleccionado.");}
        if (r.QuestRewards?.Skills == true) { int before=CountSkillRewards(s);foreach (var q in Difficulties(s)) { q.ActI.DenOfEvil = Completed(q.ActI.DenOfEvil); q.ActII.RadamentsLair = Completed(q.ActII.RadamentsLair); q.ActIV.TheFallenAngel = Completed(q.ActIV.TheFallenAngel); } if(before!=12)diff.Add(new("quests", "Puntos de habilidad por misiones",before,12)); }
        if (r.QuestRewards?.Stats == true) { int before=CountStatRewards(s);foreach (var q in Difficulties(s)) q.ActIII.LamEsensTome = Completed(q.ActIII.LamEsensTome);if(before!=15)diff.Add(new("quests", "Puntos de atributos por misiones",before,15)); }
        if (r.QuestRewards?.Anya == true) { int before=Difficulties(s).Count(q=>(q.ActV.PrisonOfIce&QuestFlags.Custom3)!=0);foreach (var q in Difficulties(s)) q.ActV.PrisonOfIce = Completed(q.ActV.PrisonOfIce) | QuestFlags.Custom3 | QuestFlags.Custom4; if(before!=3)diff.Add(new("quests", "Pergaminos de Anya consumidos",before,3)); }
        if (r.Name is not null)
        {
            if (r.Name.Length is < 2 or > 15 || r.Name.Any(c => !(char.IsAsciiLetter(c) || c is '-' or '_'))) throw new ApiProblem("name_invalid", "El nombre admite de 2 a 15 letras ASCII, guion y guion bajo.");
            string before = s.Version >= 104 ? s.Character.Preview.Name : s.Character.Name;
            s.Character.Name = r.Name; s.Character.Preview.Name = r.Name;
            if (before != r.Name) diff.Add(new("character", "Nombre", before, r.Name));
        }
        if (level != oldLevel)
        {
            s.Character.Level = (byte)level; Set(12, level);
            var xp = p.Read("experience.txt").First(x => V(x, "Level") == (level - 1).ToString()); Set(13, long.Parse(V(xp, s.Character.Class.ToString())));
            if (level != oldLevel) diff.Add(new("character", "Nivel", oldLevel, level));
        }
        if(r.Experience.HasValue){long before=Stat(13);Set(13,r.Experience.Value);if(before!=r.Experience.Value)diff.Add(new("character","Experiencia",before,r.Experience.Value));}
        if (r.Attributes is not null)
        {
            var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["strength"] = 0, ["energy"] = 1, ["dexterity"] = 2, ["vitality"] = 3, ["gold"] = 14, ["stashGold"] = 15, ["goldbank"] = 15 };
            foreach (var kv in r.Attributes)
            {
                if (!ids.TryGetValue(kv.Key, out int id)) throw new ApiProblem("attribute_unknown", "Atributo desconocido: " + kv.Key);
                long before = Stat(id); Set(id, kv.Value); if (before != kv.Value) diff.Add(new("attributes", kv.Key, before, kv.Value));
            }
        }
        int[] statIds = [0, 1, 2, 3]; string[] baseKeys = ["str", "int", "dex", "vit"];
        for (int i = 0; i < 4; i++) if (Stat(statIds[i]) < I(chr, baseKeys[i])) throw new ApiProblem("attribute_base", "Un atributo no puede ser inferior al valor inicial de la clase.");
        long spent = statIds.Sum(Stat) - baseKeys.Sum(k => I(chr, k)), budget = (level - 1) * I(chr, "StatPerLevel", 5) + CountStatRewards(s);
        if (spent > budget) throw new ApiProblem("attribute_budget", $"Has asignado {spent} puntos de atributos; disponibles: {budget}."); Set(4, budget - spent);
        if (level != oldLevel || Stat(3) != oldVit || Stat(1) != oldEnergy)
        {
            long hp = Stat(7) + (level - oldLevel) * I(chr, "LifePerLevel") * 64L + (Stat(3) - oldVit) * I(chr, "LifePerVitality") * 64L;
            long mp = Stat(9) + (level - oldLevel) * I(chr, "ManaPerLevel") * 64L + (Stat(1) - oldEnergy) * I(chr, "ManaPerMagic") * 64L;
            long stamina = Stat(11) + (level - oldLevel) * I(chr, "StaminaPerLevel") * 64L + (Stat(3) - oldVit) * I(chr, "StaminaPerVitality") * 64L;
            Set(7, hp); Set(6, hp); Set(9, mp); Set(8, mp); Set(11, stamina); Set(10, stamina);
        }
        var skillRows = p.Read("skills.txt");
        if (r.Skills is not null)
        {
            if (r.Skills.Count > 256 || r.Skills.Select(v => v.Id).Distinct().Count() != r.Skills.Count) throw new ApiProblem("skills_invalid", "La lista de habilidades contiene duplicados o es demasiado grande.");
            var beforeSkills=s.Skills.SkillLevels.ToArray();Array.Clear(s.Skills.SkillLevels);
            foreach (var change in r.Skills)
            {
                var row = skillRows.FirstOrDefault(x => I(x, "*Id", -1) == change.Id); int idx = s.Skills.GetSkillIndex(change.Id);
                if (row is null || idx < 0 || idx >= s.Skills.SkillLevels.Length) throw new ApiProblem("skill_class", "La habilidad no pertenece a la clase seleccionada: " + change.Id);
                if (change.Points < 0 || change.Points > I(row, "maxlvl", 20) || (change.Points > 0 && (level < I(row, "reqlevel") || change.Points > level - I(row, "reqlevel") + 1))) throw new ApiProblem("skill_cap", "Puntos ilegales para " + V(row, "skill"));
                s.Skills[idx] = change.Points;
            }
            if(!beforeSkills.AsSpan().SequenceEqual(s.Skills.SkillLevels))diff.Add(new("skills", "Distribución de habilidades",beforeSkills.Select((points,index)=>new{Id=s.Skills.GetSkillId(index),Points=(int)points}).Where(x=>x.Points>0).ToArray(),r.Skills.Where(x => x.Points > 0).Select(x => new { x.Id, x.Points }).ToArray()));
        }
        foreach (var row in skillRows)
        {
            int idx = s.Skills.GetSkillIndex(I(row, "*Id", -1)); if (idx < 0 || idx >= s.Skills.SkillLevels.Length || s.Skills.SkillLevels[idx] == 0) continue;
            int hardPoints = s.Skills.SkillLevels[idx];
            if (hardPoints > I(row, "maxlvl", 20) || level < I(row, "reqlevel") || hardPoints > level - I(row, "reqlevel") + 1) throw new ApiProblem("skill_cap", "El nivel del personaje no permite los puntos actuales de " + V(row, "skill") + ".");
            foreach (var key in new[] { "reqskill1", "reqskill2", "reqskill3" })
            {
                var name = V(row, key); if (name.Length == 0) continue;
                var dependency = skillRows.FirstOrDefault(x => V(x, "skill") == name); int depIndex = dependency is null ? -1 : s.Skills.GetSkillIndex(I(dependency, "*Id", -1));
                if (depIndex < 0 || depIndex >= s.Skills.SkillLevels.Length || s.Skills.SkillLevels[depIndex] == 0) throw new ApiProblem("skill_prerequisite", V(row, "skill") + " necesita al menos 1 punto en " + name + ".");
            }
        }
        int skillSpent = s.Skills.SkillLevels.Sum(x => (int)x), skillBudget = (level - 1) * I(chr, "SkillsPerLevel", 1) + CountSkillRewards(s);
        if (skillSpent > skillBudget) throw new ApiProblem("skill_budget", $"Has asignado {skillSpent} puntos de habilidad; disponibles: {skillBudget}."); Set(5, skillBudget - skillSpent);
        if (r.UnlockHell)
        {
            uint acts = s.Character.Preview.GameVersion == GameVersion.Classic ? 4u : 5u;
            s.Character.Flags = (CharacterFlags)(((uint)s.Character.Flags & ~0x1F00u) | ((acts * 2) << 8)); s.Character.TownDifficulty = [0, 0, 0x80];
            foreach (var q in new[] { s.Quests.Normal, s.Quests.Nightmare }) { q.ActI.SistersToTheSlaughter = Completed(q.ActI.SistersToTheSlaughter); q.ActI.Completion |= QuestFlags.RewardGranted; q.ActII.TheSevenTombs = Completed(q.ActII.TheSevenTombs); q.ActII.Completion |= QuestFlags.RewardGranted; q.ActIII.TheGuardian = Completed(q.ActIII.TheGuardian); q.ActIII.Completion |= QuestFlags.RewardGranted; q.ActIV.TerrorsEnd = Completed(q.ActIV.TerrorsEnd); q.ActIV.Completion |= QuestFlags.RewardGranted; if (acts == 5) { q.ActV.EveOfDestruction = Completed(q.ActV.EveOfDestruction); q.ActV.Completion |= QuestFlags.RewardGranted; } }
            diff.Add(new("progression", "Dificultad", null, "Infierno desbloqueado; campamento del actoI"));
        }
        if (r.UnlockWaypoints) { foreach (var wp in new[] { s.Waypoints.Normal, s.Waypoints.Nightmare, s.Waypoints.Hell }) for (int i = 0; i < 39; i++) wp.SetWaypoint(i, true); diff.Add(new("progression", "Puntos de ruta", null, "Todos desbloqueados")); }
        if (r.FillTomes) { foreach (var item in s.Items.Where(i => i.ItemCodeString is "tbk" or "ibk")) item.Quantity = I(p.Bases[item.ItemCodeString], "maxstack", 20); diff.Add(new("items", "Tomos", null, "Completados hasta su capacidad nativa")); }
        bool ValidSkill(int id) => id is 32767 or 65535 || id >= 0 && id < 32767 && skillRows.Any(x => I(x, "*Id", -1) == id);
        if(r.HotkeySides is not null){r.Hotkeys??=new();foreach(int key in r.HotkeySides.Keys){if(key<0||key>=s.Character.SkillHotkeys.Length)throw new ApiProblem("hotkey_slot","El índice de atajo está fuera de rango.");r.Hotkeys.TryAdd(key,s.Character.SkillHotkeys[key].SkillId);}}
        var beforeBindings=s.Character.SkillHotkeys.Select(k=>(k.SkillId,k.Side,k.ItemIndex)).ToArray();var beforeLeft=s.Character.LeftSkill.SkillId;var beforeRight=s.Character.RightSkill.SkillId;var beforeSwitchLeft=s.Character.SwitchLeftSkill.SkillId;var beforeSwitchRight=s.Character.SwitchRightSkill.SkillId;
        foreach (var kv in r.Hotkeys ?? [])
        {
            if (kv.Key < 0 || kv.Key >= s.Character.SkillHotkeys.Length || !ValidSkill(kv.Value)) throw new ApiProblem("hotkey_invalid", "El acceso rápido no corresponde a una habilidad del perfil.");
            var old=s.Character.SkillHotkeys[kv.Key];Side side=old.SkillId==(kv.Value&32767)?old.Side:r.LeftSkill==kv.Value&&r.RightSkill!=kv.Value?Side.Left:Side.Right;
            if(r.HotkeySides?.TryGetValue(kv.Key,out var requestedSide)==true)side=requestedSide?.ToLowerInvariant() switch{"left"=>Side.Left,"right"=>Side.Right,_=>throw new ApiProblem("hotkey_side","El lado del atajo debe ser left o right.")};
            s.Character.SkillHotkeys[kv.Key] = new() { SkillId = (ushort)(kv.Value&32767), Side = side, ItemIndex = old.SkillId==(kv.Value&32767)?old.ItemIndex:ushort.MaxValue };
        }
        Skill Assignment(Skill old,int id,Side side)=>old.SkillId==(id&32767)?old:new(){SkillId=(ushort)(id&32767),Side=side,ItemIndex=ushort.MaxValue};
        if (r.LeftSkill.HasValue) { if (!ValidSkill(r.LeftSkill.Value)) throw new ApiProblem("hotkey_invalid", "La habilidad del botón izquierdo no existe."); s.Character.LeftSkill = Assignment(s.Character.LeftSkill,r.LeftSkill.Value,Side.Left); }
        if (r.RightSkill.HasValue) { if (!ValidSkill(r.RightSkill.Value)) throw new ApiProblem("hotkey_invalid", "La habilidad del botón derecho no existe."); s.Character.RightSkill = Assignment(s.Character.RightSkill,r.RightSkill.Value,Side.Right); }
        if (r.SwitchLeftSkill.HasValue) { if(!ValidSkill(r.SwitchLeftSkill.Value))throw new ApiProblem("hotkey_invalid","La habilidad izquierda del equipo alternativo no existe.");s.Character.SwitchLeftSkill=Assignment(s.Character.SwitchLeftSkill,r.SwitchLeftSkill.Value,Side.Left); }
        if (r.SwitchRightSkill.HasValue) { if(!ValidSkill(r.SwitchRightSkill.Value))throw new ApiProblem("hotkey_invalid","La habilidad derecha del equipo alternativo no existe.");s.Character.SwitchRightSkill=Assignment(s.Character.SwitchRightSkill,r.SwitchRightSkill.Value,Side.Right); }
        if(!beforeBindings.SequenceEqual(s.Character.SkillHotkeys.Select(k=>(k.SkillId,k.Side,k.ItemIndex)))||beforeLeft!=s.Character.LeftSkill.SkillId||beforeRight!=s.Character.RightSkill.SkillId||beforeSwitchLeft!=s.Character.SwitchLeftSkill.SkillId||beforeSwitchRight!=s.Character.SwitchRightSkill.SkillId)diff.Add(new("skills","Accesos rápidos",null,"Asignaciones actualizadas"));
    }
    private static PreviewRequest ApplyPreset(PreviewRequest request, D2Save save, GameData p)
    {
        if (request.Preset != "orbe-tal-oculus") return ApplyClassPreset(request, save, p);
        bool nativeOrb = p.Read("skills.txt").Any(r => V(r, "skill") == "Frozen Orb" && I(r, "maxlvl") == 25);
        bool nativeTal = p.Read("setitems.txt").Any(r => V(r, "index") == "Tal Rasha's Guardianship" && V(r, "aprop1a") == "cast2" && I(r, "amin1a") == 15);
        if (save.Character.Class.ToString() != "Sorceress" || !nativeOrb || !nativeTal) throw new ApiProblem("preset_class", "Esta plantilla corresponde a una Hechicera con las tablas nativas compatibles de D2R Reimagined.");
        using var stream = typeof(EditorService).Assembly.GetManifestResourceStream("Akara.Core.orb-preset.json") ?? typeof(EditorService).Assembly.GetManifestResourceNames().Where(n => n.EndsWith("orb-preset.json")).Select(n => typeof(EditorService).Assembly.GetManifestResourceStream(n)).First()!;
        using var json = JsonDocument.Parse(stream); var root = json.RootElement;
        request.Level ??= root.GetProperty("level").GetInt32();
        var attributes = root.GetProperty("stats").EnumerateObject().ToDictionary(x => x.Name == "goldbank" ? "stashGold" : x.Name, x => x.Value.GetInt64(), StringComparer.OrdinalIgnoreCase);
        if (request.Attributes is not null) foreach (var kv in request.Attributes) attributes[kv.Key] = kv.Value; request.Attributes = attributes;
        request.Skills ??= root.GetProperty("skills").EnumerateObject().Select(kv => new SkillChange(I(p.Read("skills.txt").First(r => V(r, "skill") == kv.Name), "*Id"), kv.Value.GetInt32())).ToList();
        request.Gear = root.GetProperty("gear").EnumerateArray().Select(e => new GearChange { Type = e.GetProperty("kind").GetString()!, Name = e.GetProperty("name").GetString()!, BaseCode = e.TryGetProperty("baseCode", out var bc) ? bc.GetString() : null, Slot = e.TryGetProperty("slot", out var slot) ? slot.GetString() : null, Page = e.TryGetProperty("page", out var page) ? page.GetString() : null, Prefix = e.TryGetProperty("prefix", out var pr) ? pr.GetString() : null, Suffix = e.TryGetProperty("suffix", out var su) ? su.GetString() : null, Sockets = e.TryGetProperty("sockets", out var so) ? so.EnumerateArray().Select(x => x.GetString()!).ToList() : [] }).Concat(request.Gear ?? []).ToList();
        request.MoveToStashItemIds = (request.MoveToStashItemIds ?? []).Concat(save.Items.Select((item, index) => new { item, index }).Where(x => x.item.Position.Mode == ItemMode.Stored && x.item.Position.StorePage == StorePage.Inventory && x.item.ItemCodeString is "cm1" or "cm2" or "cm3").Select(x => "i" + x.index)).Distinct().ToList();
        request.Hotkeys ??= root.GetProperty("hotkeys").EnumerateObject().ToDictionary(x => int.Parse(x.Name), x => x.Value.GetInt32()); request.LeftSkill ??= root.GetProperty("leftSkill").GetInt32(); request.RightSkill ??= root.GetProperty("rightSkill").GetInt32();
        request.QuestRewards = new() { Skills = true, Stats = true, Anya = true }; request.UnlockHell = true; request.UnlockWaypoints = true; request.FillTomes = true; request.FillBelt = true;
        request.World = GoldenBirdRewards(request.World);
        return request;
    }
    private static BodyLocation ParseSlot(string slot) => slot.ToLowerInvariant() switch { "weapon" or "rightarm" => BodyLocation.RightArm, "shield" or "leftarm" => BodyLocation.LeftArm, "righthand" => BodyLocation.RightHand, "lefthand" => BodyLocation.LeftHand, "helm" or "head" => BodyLocation.Head, "armor" or "torso" => BodyLocation.Torso, "amulet" or "neck" => BodyLocation.Neck, "ring1" or "rightring" => BodyLocation.RightRing, "ring2" or "leftring" => BodyLocation.LeftRing, "boots" or "feet" => BodyLocation.Feet, "gloves" => BodyLocation.Gloves, "belt" => BodyLocation.Belt, _ => throw new ApiProblem("slot_invalid", "Ubicación de equipo desconocida: " + slot) };
    private static void ApplyMercenary(D2Save save, GameData p, MercenaryChange? change, List<ChangeEntry> diff)
    {
        if (change is null) return;
        var merc = save.Character.MercData;
        if (!merc.HasMerc && !change.HirelingId.HasValue) throw new ApiProblem("mercenary_type", "Selecciona un tipo de mercenario antes de crearlo.");
        int id = change.HirelingId ?? merc.HirelingId, level = change.Level ?? save.Character.Level;
        if (id is < 0 or > 65535 || level < 1 || level > save.Character.Level) throw new ApiProblem("mercenary_level", "El mercenario debe tener un nivel entre 1 y el nivel del personaje.");
        int version = save.Character.Preview.GameVersion == GameVersion.Classic ? 0 : 100;
        var row = p.Read("hireling.txt").Where(x => I(x, "Id", -1) == id && I(x, "Version") == version && I(x, "Level") <= level).OrderByDescending(x => I(x, "Level")).FirstOrDefault();
        if (row is null) throw new ApiProblem("mercenary_type", "Ese tipo de mercenario no existe para el modo y nivel seleccionados.");
        int coefficient = I(row, "Exp/Lvl"); if (coefficient <= 0) throw new ApiProblem("mercenary_experience", "La tabla no contiene un crecimiento válido para el mercenario.");
        if (merc.HirelingId != id) merc.NameIndex = 0;
        merc.HirelingId = (ushort)id;
        if (merc.Seed == 0) { do { merc.Seed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)); } while (merc.Seed == 0); }
        merc.Experience = checked((uint)((long)coefficient * level * level * (level + 1)));
        if (change.Resurrect) merc.IsDead = false;
        save.MercItems ??= new MercItemsSection();
        diff.Add(new("mercenary", "Mercenario", null, V(row, "Hireling") + " · nivel " + level));
    }
    private static void Store(D2Save save, Item item, StorePage page, GameData p)
    {
        var grid = p.Grid(save, page == StorePage.Inventory ? "inventory" : page == StorePage.Cube ? "cube" : "stash"); var size = p.Size(item);
        for (int y = 0; y <= grid.Height - size.Height; y++) for (int x = 0; x <= grid.Width - size.Width; x++)
        {
            bool collision = save.Items.Where(i => !ReferenceEquals(i, item) && i.Position.Mode == ItemMode.Stored && i.Position.StorePage == page).Any(i => { var z = p.Size(i); return x < i.Position.InvX + z.Width && x + size.Width > i.Position.InvX && y < i.Position.InvY + z.Height && y + size.Height > i.Position.InvY; });
            if (collision) continue; item.Position = new() { Mode = ItemMode.Stored, StorePage = page, BodyLocation = BodyLocation.None, InvX = (byte)x, InvY = (byte)y }; return;
        }
        throw new ApiProblem("inventory_full", "No hay espacio para el objeto en " + (page == StorePage.Inventory ? "el inventario" : "el alijo") + ".");
    }
    private static Item ById(D2Save save, string id)
    {
        if (id.Length < 2 || id[0] != 'i' || !int.TryParse(id.AsSpan(1), out int index) || index < 0 || index >= save.Items.Count) throw new ApiProblem("item_missing", "El objeto ya no pertenece a esta sesión: " + id); return save.Items[index];
    }
    private static void ApplyItems(D2Save save, GameData p, PreviewRequest r, List<ChangeEntry> diff, ItemIdentities identities)
    {
        var factory = new ItemFactory(p.ExcelPath) { AllowNonSpawnableUniques = true, StringsDirectory = p.StringsPath, ItemFormatVersion = save.Version >= 105 ? (ushort)101 : (ushort)Math.Min(save.Version, 101u) };
        if ((r.Gear?.Count ?? 0) > 200 || (r.ItemStats?.Count ?? 0) > 500 || (r.MoveToStashItemIds?.Count ?? 0) > 200) throw new ApiProblem("operations_limit", "Demasiadas operaciones en una vista previa.");
        foreach (var id in r.MoveToStashItemIds ?? []) { var item = ById(save, id); Store(save, item, StorePage.Stash, p); diff.Add(new("items", "Mover al alijo", ItemName(item, p), "Alijo personal")); }
        foreach (var change in r.ItemStats ?? [])
        {
            var item = ById(save, change.ItemId);
            if (!p.StatRows.TryGetValue(change.StatId, out var row)) throw new ApiProblem("item_stat_unknown", "Stat de objeto desconocido.");
            int bits = I(row, "Save Bits"), paramBits = I(row, "Save Param Bits"), shift = I(row, "ValShift");
            if (!double.IsFinite(change.Value) || change.Value != Math.Truncate(change.Value) || bits <= 0 || bits > 32 || change.StatId >= 511 || change.Layer < 0 || change.Layer >= (paramBits == 0 ? 1L : 1L << paramBits) || change.Value + I(row, "Save Add") < 0 || change.Value + I(row, "Save Add") > ((1L << bits) - 1) || change.StatId == (int)StatId.NumSockets) throw new ApiProblem("item_stat_range", "El valor o parámetro no cabe en la codificación nativa del stat.");
            var stat = item.Stats.FirstOrDefault(x => (int)x.Id == change.StatId && x.Layer == change.Layer); long value = checked((long)change.Value << shift);
            diff.Add(new("items", ItemName(item, p) + ": " + V(row, "Stat"), stat?.Value, value));
            if (stat is null) item.Stats.Add(new() { Id = (StatId)change.StatId, Layer = change.Layer, Value = value }); else stat.Value = value;
        }
        foreach (var gear in r.Gear ?? [])
        {
            var item = MakeGear(gear, p, save.Version); identities.Add(item, gear.ClientId);
            var destination = gear.MercSlot is not null ? new ItemDestination { Owner = "mercenary", Page = "equipped", Slot = gear.MercSlot } : gear.Slot is not null ? new ItemDestination { Page = "equipped", Slot = gear.Slot, WeaponSet = gear.WeaponSet } : new ItemDestination { Page = gear.Page ?? "stash" };
            PlaceItem(save, item, destination, p);
            diff.Add(new(destination.Owner == "mercenary" ? "mercenary" : "items", "Añadir " + gear.Type, null, ItemName(item, p) + " · " + (destination.Slot ?? destination.Page)));
        }
        if (r.FillBelt)
        {
            foreach (var item in save.Items.Where(i => i.Position.Mode == ItemMode.InBelt)) item.ItemCodeString = "rvl";
            // Armor.belt indexes the native belt layout; it is not a row count.
            var belt = save.Items.FirstOrDefault(i => i.Position.Mode == ItemMode.Equipped && i.Position.BodyLocation == BodyLocation.Belt);
            int[] vanillaBeltSlots = [12, 8, 4, 16, 8, 12, 16];
            int beltLayout = belt is null ? 2 : I(p.Bases[belt.ItemCodeString], "belt", 2);
            int count = beltLayout >= 0 && beltLayout < vanillaBeltSlots.Length ? vanillaBeltSlots[beltLayout] : throw new ApiProblem("belt_layout", "El perfil usa una distribución de cinturón no admitida.");
            for (byte x = 0; x < count; x++) if (!save.Items.Any(i => i.Position.Mode == ItemMode.InBelt && i.Position.InvX == x)) { var potion = factory.CreateNormal("rvl", 1); potion.Position = new() { Mode = ItemMode.InBelt, InvX = x, StorePage = StorePage.Null }; save.Items.Add(potion); }
            diff.Add(new("items", "Cinturón", null, count + " rejuvenecedoras completas"));
        }
    }
    private static void ValidateSlot(Item item, BodyLocation slot, GameData p)
    {
        var baseRow = p.Bases[item.ItemCodeString]; string type = V(baseRow, "type"); var types = p.Read("itemtypes.txt");
        bool Is(string expected, string actual, HashSet<string> seen) { if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) return true; if (!seen.Add(actual)) return false; var row = types.FirstOrDefault(x => V(x, "Code") == actual); return row is not null && new[] { V(row, "Equiv1"), V(row, "Equiv2") }.Any(x => x.Length > 0 && Is(expected, x, seen)); }
        bool Match(string t) => Is(t, type, new(StringComparer.OrdinalIgnoreCase));
        bool ok = slot switch { BodyLocation.Head => Match("helm"), BodyLocation.Torso => Match("tors"), BodyLocation.Neck => Match("amul"), BodyLocation.RightRing or BodyLocation.LeftRing => Match("ring"), BodyLocation.Feet => Match("boot"), BodyLocation.Gloves => Match("glov"), BodyLocation.Belt => Match("belt"), BodyLocation.RightArm => Match("weap"), BodyLocation.LeftArm => Match("shld") || Match("weap"), _ => false };
        if (!ok) throw new ApiProblem("item_slot", "La base del objeto no corresponde a esa ranura de equipo.");
    }
    private static void ValidatePlacement(D2Save save, GameData p)
    {
        foreach (var page in new[] { StorePage.Inventory, StorePage.Stash, StorePage.Cube })
        {
            var grid = p.Grid(save, page == StorePage.Inventory ? "inventory" : page == StorePage.Cube ? "cube" : "stash"); var occupied = new bool[grid.Width, grid.Height];
            foreach (var item in save.Items.Where(i => i.Position.Mode == ItemMode.Stored && i.Position.StorePage == page))
            {
                var z = p.Size(item); int x = item.Position.InvX, y = item.Position.InvY;
                if (x + z.Width > grid.Width || y + z.Height > grid.Height) throw new ApiProblem("item_outside_grid", "Hay un objeto fuera de los límites del inventario.");
                for (int dx = x; dx < x + z.Width; dx++) for (int dy = y; dy < y + z.Height; dy++) { if (occupied[dx, dy]) throw new ApiProblem("item_collision", "Dos objetos ocupan la misma casilla."); occupied[dx, dy] = true; }
            }
        }
        if (save.Items.Where(i => i.Position.Mode == ItemMode.Equipped).GroupBy(i => i.Position.BodyLocation).Any(g => g.Count() > 1)) throw new ApiProblem("equipment_collision", "Hay dos objetos en la misma ranura de equipo.");
        foreach (string statName in new[] { "torch_weight", "soulstone_weight" })
        {
            var row = p.StatRows.FirstOrDefault(kv => V(kv.Value, "Stat") == statName);
            if (row.Value is null) continue;
            long total = save.Items.Where(i => i.Position.Mode == ItemMode.Stored && i.Position.StorePage == StorePage.Inventory).SelectMany(i => i.Stats).Where(x => (int)x.Id == row.Key).Sum(x => x.Value);
            if (total > 1) throw new ApiProblem("charm_duplicate", "El inventario no admite más de " + (statName == "torch_weight" ? "una Antorcha del Infierno" : "un Annihilus") + " activo. Mueve el anterior al alijo.");
        }
    }
    private static void ValidateStatUniqueness(Item item)
    {
        static void Check(IEnumerable<Stat> stats)
        {
            if (stats.GroupBy(s => ((int)s.Id, s.Layer)).Any(g => g.Count() > 1)) throw new ApiProblem("duplicate_item_stat", "La codificación del objeto duplica una propiedad. Los cambios no se pueden guardar.");
        }
        Check(item.Stats); foreach (var list in item.SetBonusStats) Check(list); if (item.RunewordStats is not null) Check(item.RunewordStats);
        foreach (var child in item.Sockets) if (child is not null) ValidateStatUniqueness(child);
    }
    private static EditPlan Plan(SaveSession s, string id) => s.Plan is { } plan && plan.Id == id ? plan : throw new ApiProblem("plan_missing", "Genera una vista previa válida antes de guardar.", 409);
    public object Export(ExportRequest request)
    {
        var s = Session(request.SessionId); lock (s.Sync)
        {
            var plan = Plan(s, request.PlanId);
            var candidate = Read(plan.Bytes, s.Profile, s.Version); string name = s.Version >= 104 ? candidate.Character.Preview.Name : candidate.Character.Name;
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ApiProblem("export_name", "El nombre del personaje no se puede usar como nombre de archivo.");
            var exportDir = Path.Combine(dataRoot, "exports", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
            string path = request.DestinationPath is null ? Path.Combine(exportDir, name + ".d2s") : Path.GetFullPath(request.DestinationPath);
            if (!Path.GetExtension(path).Equals(".d2s", StringComparison.OrdinalIgnoreCase)) throw new ApiProblem("export_extension", "La exportación debe ser un archivo.d2s.");
            if (path.Equals(s.SourcePath, StringComparison.OrdinalIgnoreCase)) throw new ApiProblem("export_source", "Exportar crea una copia. Para reemplazar el original, usa Guardar partida.");
            if (!Path.GetFileNameWithoutExtension(path).Equals(name, StringComparison.OrdinalIgnoreCase)) throw new ApiProblem("export_name", "El archivo exportado debe llamarse " + name + ".d2s para que el juego reconozca el personaje.");
            if (request.DestinationPath is null) Directory.CreateDirectory(exportDir);
            WriteNewAtomic(path, plan.Bytes);
            return new { outputPath = path, hash = Hash(plan.Bytes) };
        }
    }
    private string BackupRoot(SaveSession s) => Path.Combine(dataRoot, "backups", Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s.SourcePath.ToLowerInvariant())))[..20]);
    private List<BackupEntry> Backups(SaveSession s)
    {
        var dir = BackupRoot(s); if (!Directory.Exists(dir)) return [];
        var entries = new List<BackupEntry>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").Take(1000)) { try { var entry = JsonSerializer.Deserialize<BackupEntry>(File.ReadAllText(file)); if (entry is not null && entry.SourcePath.Equals(s.SourcePath, StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(Path.GetFullPath(entry.Path)) == dir) entries.Add(entry); } catch (Exception) { } }
        return entries.OrderByDescending(e => e.CreatedAt).ToList();
    }
    public object GetBackups(string id) => new { entries = Backups(Session(id)).Select(b => new { b.Id, b.Path, b.CreatedAt, b.Hash }) };
    public object Commit(CommitRequest request)
    {
        var s = Session(request.SessionId); lock (s.Sync)
        {
            if (!s.IsNew) return Replace(s, Plan(s, request.PlanId).Bytes, request.ExpectedHash);
            if (request.DestinationPath is null) throw new ApiProblem("destination_required", "Selecciona dónde guardar el personaje nuevo.");
            var result = JsonSerializer.SerializeToElement(Export(new(s.Id, request.PlanId, request.DestinationPath)), ContractJson);
            string path = result.GetProperty("outputPath").GetString()!;
            return new { backupPath = (string?)null, outputPath = path, hash = result.GetProperty("hash").GetString(), summary = Open(new(path, s.Profile.Id)) };
        }
    }
    public object Restore(RestoreRequest request)
    {
        var s = Session(request.SessionId); lock (s.Sync)
        {
            var backup = Backups(s).FirstOrDefault(b => b.Id == request.BackupId) ?? throw new ApiProblem("backup_missing", "El respaldo ya no existe.", 404);
            var bytes = ReadSave(backup.Path); if (Hash(bytes) != backup.Hash) throw new ApiProblem("backup_changed", "El respaldo fue modificado. Se ha cancelado la restauración.");
            return Replace(s, bytes, request.ExpectedHash);
        }
    }
    private object Replace(SaveSession s, byte[] bytes, string expectedHash)
    {
        if (gameState()) throw new ApiProblem("game_running", "Cierra Diablo II antes de reemplazar una partida. Puedes exportar una copia mientras juegas.", 409);
        s.Profile.EnsureUnchanged();
        var original = ReadSave(s.SourcePath); var hash = Hash(original);
        if (hash != expectedHash || hash != s.Hash) throw new ApiProblem("source_changed", "La partida ha cambiado desde que la abriste. Vuelve a cargarla antes de guardar.", 409);
        if (!D2Save.VerifyChecksum(bytes)) throw new ApiProblem("candidate_checksum", "La copia no supera la validación de checksum.");
        var candidate = Read(bytes, s.Profile, s.Version); string candidateName = s.Version >= 104 ? candidate.Character.Preview.Name : candidate.Character.Name;
        if (!Path.GetFileNameWithoutExtension(s.SourcePath).Equals(candidateName, StringComparison.OrdinalIgnoreCase)) throw new ApiProblem("rename_export_required", "Para cambiar el nombre del personaje, exporta una copia con su nuevo nombre. El guardado directo conserva el archivo original.");
        var dir = BackupRoot(s); Directory.CreateDirectory(dir); string id = Guid.NewGuid().ToString("N"), backupPath = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + id + ".d2s");
        using (var backup = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { backup.Write(original); backup.Flush(true); }
        if (Hash(File.ReadAllBytes(backupPath)) != hash) throw new ApiProblem("backup_verification", "No se pudo verificar el respaldo. No se ha reemplazado la partida.");
        var entry = new BackupEntry(id, s.SourcePath, backupPath, DateTimeOffset.UtcNow, hash); File.WriteAllText(backupPath + ".json", JsonSerializer.Serialize(entry));
        string temporary = Path.Combine(Path.GetDirectoryName(s.SourcePath)!, ".akara-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var temp = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { temp.Write(bytes); temp.Flush(true); }
            if (gameState() || Hash(ReadSave(s.SourcePath)) != hash) throw new ApiProblem("source_changed", "El juego se abrió o la partida cambió durante el guardado. El original sigue intacto.", 409);
            File.Replace(temporary, s.SourcePath, null);
            var written = ReadSave(s.SourcePath); if (Hash(written) != Hash(bytes) || !D2Save.VerifyChecksum(written)) throw new ApiProblem("write_verification", "No se ha podido verificar el archivo guardado. El respaldo está disponible.");
            s.Original = written; s.Hash = Hash(written); s.Plan = null;
            return new { backupPath, outputPath = s.SourcePath, hash = s.Hash, summary = Inspect(s, Read(written, s.Profile, s.Version)) };
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
