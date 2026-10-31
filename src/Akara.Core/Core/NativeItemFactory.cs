using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using D2SSharp.Enums;
using D2SSharp.Model;

namespace D2RBuilder
{
    // Creates only local item objects. It never reads or writes character saves.
    // Values are resolved from the selected installed mod tables, not vanilla constants.
    public sealed class ItemFactory
    {
        public static bool SupportsPropertyFunction(int function) => function is
            1 or 2 or 3 or 5 or 6 or 7 or 8 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24;
        readonly Dictionary<string, Dictionary<string, string>> bases = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> baseKinds = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Dictionary<string, string>> properties;
        readonly Dictionary<string, Dictionary<string, string>> statRows;
        readonly Dictionary<string, Dictionary<string, string>> types;
        readonly Dictionary<string, int> skills;
        readonly Dictionary<string, int> states = new(StringComparer.OrdinalIgnoreCase);
        readonly List<Dictionary<string, string>> uniques;
        readonly List<Dictionary<string, string>> sets;
        readonly List<Dictionary<string, string>> runes;
        readonly List<Dictionary<string, string>> prefixes;
        readonly List<Dictionary<string, string>> suffixes;
        readonly List<Dictionary<string, string>> automagic;
        public string RollMode { get; set; } = "mid";
        public string StringsDirectory { get; set; }
        public int PreferredRandomSkillId { get; set; } = 64;
        public ushort ItemFormatVersion { get; set; } = 101;
        public bool AllowNonSpawnableUniques { get; set; } = false;
        public List<string> Notes { get; } = new();

        public ItemFactory(string excelDirectory)
        {
            foreach (string kind in new[] { "weapons", "armor", "misc" })
                foreach (var row in ReadTable(Path.Combine(excelDirectory, kind + ".txt")))
                {
                    string code = V(row, "code");
                    if (code.Length == 0 || bases.ContainsKey(code)) continue;
                    bases.Add(code, row);
                    baseKinds.Add(code, kind);
                }
            properties = Index(ReadTable(Path.Combine(excelDirectory, "properties.txt")), "code");
            statRows = Index(ReadTable(Path.Combine(excelDirectory, "itemstatcost.txt")), "Stat");
            types = Index(ReadTable(Path.Combine(excelDirectory, "itemtypes.txt")), "Code");
            skills = new(StringComparer.OrdinalIgnoreCase);
            foreach (var row in ReadTable(Path.Combine(excelDirectory, "skills.txt")))
                if (V(row, "skill").Length > 0 && int.TryParse(V(row, "*Id"), out int id))
                    skills.TryAdd(V(row, "skill"), id);
            string statesPath = Path.Combine(excelDirectory, "states.txt");
            if (File.Exists(statesPath))
                foreach (var row in ReadTable(statesPath))
                    if (V(row, "state").Length > 0 && int.TryParse(V(row, "*ID"), out int stateId)) states.TryAdd(V(row, "state"), stateId);
            uniques = ReadTable(Path.Combine(excelDirectory, "uniqueitems.txt"));
            sets = ReadTable(Path.Combine(excelDirectory, "setitems.txt"));
            runes = ReadTable(Path.Combine(excelDirectory, "runes.txt"));
            prefixes = ReadTable(Path.Combine(excelDirectory, "magicprefix.txt"));
            suffixes = ReadTable(Path.Combine(excelDirectory, "magicsuffix.txt"));
            automagic = ReadTable(Path.Combine(excelDirectory, "automagic.txt"));
            StringsDirectory = Path.GetFullPath(Path.Combine(excelDirectory, "..", "..", "local", "lng", "strings"));
        }

        public Item CreateUnique(string name, int level = 99)
        {
            var row = FindOne(uniques, "index", name);
            if (V(row, "disabled") == "1") throw new InvalidOperationException("Item is disabled: " + name);
            if (V(row, "spawnable") != "1")
            {
                if (!AllowNonSpawnableUniques) throw new InvalidOperationException("Unique is special-drop/nonspawnable; explicitly enable AllowNonSpawnableUniques: " + name);
                Notes.Add("Special-drop/nonspawnable unique explicitly requested: " + name);
            }
            var item = CreateBase(V(row, "code"), level, ItemQuality.Unique);
            item.QualityData = new SetUniqueQualityData { SetUniqueFileIndex = CheckedUShort(V(row, "*ID")) };
            ApplySequence(item, item.Stats, row, "prop", "par", "min", "max", 12);
            Finish(item);
            return item;
        }

        public Item CreateSet(string name, int level = 99)
        {
            var row = FindOne(sets, "index", name);
            CheckSpawnable(row, name);
            if (V(row, "add func") != "2" && V(row, "add func").Length > 0)
                throw new NotSupportedException("Set additional-property function is not supported: " + V(row, "add func"));
            var item = CreateBase(V(row, "item"), level, ItemQuality.Set);
            item.QualityData = new SetUniqueQualityData { SetUniqueFileIndex = CheckedUShort(V(row, "*ID")) };
            ApplySequence(item, item.Stats, row, "prop", "par", "min", "max", 9);
            byte mask = 0;
            for (int n = 1; n <= 5; n++)
            {
                var stats = new List<Stat>();
                foreach (string letter in new[] { "a", "b" })
                {
                    string suffix = n.ToString(CultureInfo.InvariantCulture) + letter;
                    string code = V(row, "aprop" + suffix);
                    if (code.Length > 0)
                        ApplyProperty(item, stats, code, V(row, "apar" + suffix), V(row, "amin" + suffix), V(row, "amax" + suffix));
                }
                if (stats.Count > 0)
                {
                    mask |= (byte)(1 << (n - 1));
                    item.SetBonusStats.Add(stats);
                }
            }
            item.SetItemMask = mask;
            Finish(item);
            return item;
        }

        public Item CreateRuneword(string name, string baseCode, int level = 99)
        {
            var row = FindRuneword(name);
            var kit = CreateRunewordKit(name, baseCode, level);
            var item = kit[0];
            string jsonPath = Path.Combine(StringsDirectory, "item-runes.json");
            if (!File.Exists(jsonPath)) throw new FileNotFoundException("Runeword string table is required. Set StringsDirectory to the mod data/local/lng/strings directory.", jsonPath);
            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var ids = document.RootElement.EnumerateArray().Where(e => string.Equals(e.GetProperty("Key").GetString(), V(row, "Name"), StringComparison.OrdinalIgnoreCase)).ToArray();
            if (ids.Length != 1) throw new InvalidDataException("Runeword has no unique native string ID: " + V(row, "Name"));
            item.RunewordId = checked((ushort)ids[0].GetProperty("id").GetInt32());
            item.Flags |= ItemFlags.Runeword;
            item.RunewordStats = new List<Stat>();
            ApplySequence(item, item.RunewordStats, row, "T1Code", "T1Param", "T1Min", "T1Max", 7);
            for (int n = 1; n < kit.Count; n++) InsertRune(item, kit[n]);
            // Native v105 save fixtures persist rune children and T1 runeword stats.
            // The engine derives gems.txt socket bonuses from the children on load;
            // adding those modifiers to parent.Stats would apply them twice.
            Finish(item);
            return item;
        }

        public Item CreateMagic(string baseCode, string prefixName, string? suffixName = null, int level = 99)
        {
            var item = CreateBase(baseCode, level, ItemQuality.Magic);
            var quality = new MagicQualityData();
            if (!string.IsNullOrWhiteSpace(prefixName))
            {
                var row = FindAffix(prefixes, prefixName, baseCode, level);
                quality.PrefixId = checked((ushort)(prefixes.IndexOf(row) + 1));
                ApplyAffix(item, row);
            }
            if (!string.IsNullOrWhiteSpace(suffixName))
            {
                var row = FindAffix(suffixes, suffixName, baseCode, level);
                quality.SuffixId = checked((ushort)(suffixes.IndexOf(row) + 1));
                ApplyAffix(item, row);
            }
            if (quality.PrefixId == 0 && quality.SuffixId == 0) throw new ArgumentException("A magic item needs at least one legal affix.");
            item.QualityData = quality;
            ApplyBaseAutoAffix(item);
            Finish(item);
            return item;
        }

        // Adds/fills a legal socket on a local object. Socket bonuses are derived
        // by the game from this child, not duplicated in the parent's stat list.
        public void AddRuneSocket(Item item, string runeCode)
        {
            if (item.Flags.HasFlag(ItemFlags.Runeword)) throw new InvalidOperationException("Cannot change a finished runeword's sockets.");
            if (!bases.TryGetValue(item.ItemCodeString, out var row) || I(row, "gemsockets") < 1)
                throw new ArgumentException("Item cannot have a socket.");
            if (item.Sockets.Count == 0)
            {
                item.Flags |= ItemFlags.Socketed;
                item.Sockets.Add(null);
            }
            InsertRune(item, CreateBase(runeCode, item.ItemLevel, ItemQuality.Normal));
            Finish(item);
        }
        public Item CreateMagicFromIds(string baseCode, int prefixId, int suffixId, int level = 99)
        {
            if (prefixId == 0 && suffixId == 0) throw new ArgumentException("Selecciona al menos un afijo.");
            var item = CreateBase(baseCode, level, ItemQuality.Magic);
            item.QualityData = new MagicQualityData { PrefixId = checked((ushort)prefixId), SuffixId = checked((ushort)suffixId) };
            if (prefixId > 0) ApplyNativeAffix(item, prefixId, true);
            if (suffixId > 0) ApplyNativeAffix(item, suffixId, false);
            ApplyBaseAutoAffix(item);
            Finish(item); return item;
        }

        // The game also creates the runeword when this kit is inserted in order.
        public List<Item> CreateRunewordKit(string name, string baseCode, int level = 99)
        {
            var row = FindRuneword(name);
            if (V(row, "complete") != "1") throw new InvalidOperationException("Runeword is not enabled: " + name);
            if (!bases.TryGetValue(baseCode, out var baseRow)) throw new ArgumentException("Unknown base code: " + baseCode);
            string baseType = V(baseRow, "type");
            bool included = Enumerable.Range(1, 6).Select(i => V(row, "itype" + i)).Any(t => t.Length > 0 && IsType(baseType, t, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
            bool excluded = Enumerable.Range(1, 3).Select(i => V(row, "etype" + i)).Any(t => t.Length > 0 && IsType(baseType, t, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
            if (!included || excluded) throw new ArgumentException("Base type is not allowed for " + name + ": " + baseCode);
            var runeCodes = Enumerable.Range(1, 6).Select(i => V(row, "Rune" + i)).Where(s => s.Length > 0).ToArray();
            if (runeCodes.Length == 0 || runeCodes.Length > I(baseRow, "gemsockets"))
                throw new InvalidOperationException("Base does not support required socket count: " + baseCode);
            var item = CreateBase(baseCode, level, ItemQuality.Normal);
            item.Flags |= ItemFlags.Socketed;
            item.Sockets = Enumerable.Repeat<Item?>(null, runeCodes.Length).ToList();
            var result = new List<Item> { item };
            foreach (string code in runeCodes) result.Add(CreateBase(code, level, ItemQuality.Normal));
            return result;
        }

        Dictionary<string, string> FindRuneword(string name)
        {
            var matches = runes.Where(r => V(r, "Name").Equals(name, StringComparison.OrdinalIgnoreCase) || V(r, "*Rune Name").Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1) throw new ArgumentException("Runeword must match exactly one row: " + name);
            return matches[0];
        }

        void InsertRune(Item parent, Item rune)
        {
            string type = V(bases[rune.ItemCodeString], "type");
            if (!IsType(type, "rune", new HashSet<string>(StringComparer.OrdinalIgnoreCase))) throw new ArgumentException("Socket child is not a rune: " + rune.ItemCodeString);
            int slot = parent.Sockets.FindIndex(i => i == null);
            if (slot < 0) throw new InvalidOperationException("No empty socket remains.");
            rune.Position = new ItemPosition { Mode = ItemMode.Socketed, StorePage = StorePage.Null, BodyLocation = BodyLocation.None, InvX = (byte)slot, InvY = 0 };
            parent.Sockets[slot] = rune;
        }

        void ApplyAffix(Item item, Dictionary<string, string> row)
        {
            for (int n = 1; n <= 3; n++)
                if (V(row, "mod" + n + "code").Length > 0)
                    ApplyProperty(item, item.Stats, V(row, "mod" + n + "code"), V(row, "mod" + n + "param"), V(row, "mod" + n + "min"), V(row, "mod" + n + "max"));
        }

        Dictionary<string, string> FindAffix(List<Dictionary<string, string>> rows, string name, string baseCode, int level)
        {
            var eligible = rows.Where(r => V(r, "name").Equals(name, StringComparison.OrdinalIgnoreCase) && IsAffixAllowed(r, baseCode, level)).OrderByDescending(r => I(r, "level")).ToList();
            if (eligible.Count == 0) throw new ArgumentException("No legal affix " + name + " for " + baseCode + " at level " + level);
            if (eligible.Count > 1 && I(eligible[0], "level") == I(eligible[1], "level")) throw new ArgumentException("Ambiguous affix: " + name);
            return eligible[0];
        }

        bool IsAffixAllowed(Dictionary<string, string> row, string baseCode, int level)
        {
            if (V(row, "spawnable") != "1" || I(row, "level") > level || (I(row, "maxlevel") > 0 && level > I(row, "maxlevel"))) return false;
            string actual = V(bases[baseCode], "type");
            bool included = Enumerable.Range(1, 7).Select(n => V(row, "itype" + n)).Any(t => t.Length > 0 && IsType(actual, t, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
            bool excluded = Enumerable.Range(1, 5).Select(n => V(row, "etype" + n)).Any(t => t.Length > 0 && IsType(actual, t, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
            return included && !excluded;
        }

        public Dictionary<string, string> GetBaseInfo(string code)
        {
            if (!bases.TryGetValue(code, out var row)) throw new ArgumentException("Unknown base: " + code);
            return new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase);
        }
        public void ApplyNativeAffix(Item item, int id, bool prefix)
        {
            var rows = prefix ? prefixes : suffixes;
            if (id < 1 || id > rows.Count || !IsAffixAllowed(rows[id - 1], item.ItemCodeString, item.ItemLevel)) throw new ArgumentException("El afijo no corresponde a esta base o nivel.");
            ApplyAffix(item, rows[id - 1]); Finish(item);
        }

        public Item CreateNormal(string code, int level = 99)
        {
            var item = CreateBase(code, level, ItemQuality.Normal);
            if (code is "tbk" or "ibk") item.QualityData = new NormalScrollBookQualityData { SpellType = (byte)(code == "ibk" ? 1 : 0) };
            if (IsType(V(bases[code], "type"), "char", new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            {
                item.QualityData = new NormalCharmQualityData();
                ApplyBaseAutoAffix(item);
            }
            Finish(item);
            return item;
        }

        /// <summary>Ensures the selected native automod, without doubling existing modifiers.</summary>
        public void ApplyBaseAutoAffix(Item item)
        {
            // Unique/set charms carry their own explicitly defined weights.
            if (item.Quality is ItemQuality.Unique or ItemQuality.Set) return;
            int group = I(bases[item.ItemCodeString], "auto prefix");
            if (group == 0) return;
            Dictionary<string, string> row;
            if (item.AutoAffixId is > 0)
            {
                int index = item.AutoAffixId.Value - 1;
                if (index >= automagic.Count || I(automagic[index], "group") != group || !IsAffixAllowed(automagic[index], item.ItemCodeString, item.ItemLevel))
                    throw new InvalidDataException("El automodificador no corresponde a la base y nivel del objeto.");
                row = automagic[index];
            }
            else
            {
                var eligible = automagic.Where(r => I(r, "group") == group && IsAffixAllowed(r, item.ItemCodeString, item.ItemLevel)).ToList();
                if (eligible.Count != 1) throw new NotSupportedException("El grupo de automodificadores necesita una selección nativa explícita: " + group);
                row = eligible[0]; item.AutoAffixId = checked((ushort)(automagic.IndexOf(row) + 1));
            }
            var native = new List<Stat>();
            for (int n = 1; n <= 3; n++)
                if (V(row, "mod" + n + "code").Length > 0)
                    ApplyProperty(item, native, V(row, "mod" + n + "code"), V(row, "mod" + n + "param"), V(row, "mod" + n + "min"), V(row, "mod" + n + "max"));
            foreach (var stat in native)
                if (!item.Stats.Any(s => s.Id == stat.Id && s.Layer == stat.Layer)) item.Stats.Add(stat);
            Finish(item);
        }

        public void MakeEthereal(Item item)
        {
            if (item.Flags.HasFlag(ItemFlags.Ethereal)) return;
            if (item.Quality is ItemQuality.Set or ItemQuality.Inferior || !item.MaxDurability.HasValue || item.MaxDurability <= 0) throw new ArgumentException("Esta base o calidad no admite etéreo.");
            item.Flags |= ItemFlags.Ethereal;
            if (item.Defense.HasValue) item.Defense = item.Defense.Value * 3 / 2;
            item.MaxDurability = item.MaxDurability.Value / 2 + 1;
            item.Durability = item.MaxDurability;
        }

        Item CreateBase(string code, int level, ItemQuality quality)
        {
            if (!bases.TryGetValue(code, out var row)) throw new ArgumentException("Unknown item code: " + code);
            if (level < 1 || level > 99) throw new ArgumentOutOfRangeException(nameof(level), "Item level must be 1..99.");
            var item = new Item
            {
                ItemCodeString = code, Quality = quality, ItemLevel = (byte)level,
                Version = ItemFormatVersion, ItemSeed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)),
                Flags = ItemFlags.Identified | ItemFlags.Init,
                Position = new ItemPosition { Mode = ItemMode.Stored, StorePage = StorePage.Stash, InvX = 0, InvY = 0 }
            };
            if (V(row, "compactsave") == "1") item.Flags |= ItemFlags.CompactSave;
            if (baseKinds[code] == "armor") item.Defense = Pick(I(row, "minac"), I(row, "maxac"));
            if (baseKinds[code] == "weapons" || baseKinds[code] == "armor")
            {
                item.MaxDurability = V(row, "nodurability") == "1" ? 0 : I(row, "durability");
                item.Durability = item.MaxDurability;
            }
            if (V(row, "stackable") == "1") item.Quantity = Math.Max(1, I(row, "minstack"));
            return item;
        }

        void Finish(Item item)
        {
            // Paired stats must be emitted in ID order. The writer emits followers
            // together with the leading stat (e.g. maxED17 followed by minED18).
            static void SortStats(List<Stat> stats)
            {
                // Native zero modifiers are omitted on save. Keep generated
                // objects canonical so blueprint comparison does not demand an
                // absent no-op field after decoding.
                stats.RemoveAll(s => s.Value == 0);
                stats.Sort((a, b) => a.Id != b.Id ? ((int)a.Id).CompareTo((int)b.Id) : a.Layer.CompareTo(b.Layer));
            }
            SortStats(item.Stats);
            foreach (var list in item.SetBonusStats) SortStats(list);
            if (item.RunewordStats is not null) SortStats(item.RunewordStats);
            // Enhanced-defense items roll maximum base defense + 1 before their ED modifier.
            int armorPercentId = I(statRows["item_armor_percent"], "*ID");
            if (item.Defense.HasValue && (item.Quality is ItemQuality.Unique or ItemQuality.Set or ItemQuality.Magic or ItemQuality.Rare or ItemQuality.Craft) && item.Stats.Any(s => (int)s.Id == armorPercentId && s.Value > 0))
            {
                int defense = I(bases[item.ItemCodeString], "maxac") + 1;
                item.Defense = item.Flags.HasFlag(ItemFlags.Ethereal) ? defense * 3 / 2 : defense;
            }
            ValidateStatList(item.Stats);
            foreach (var list in item.SetBonusStats) ValidateStatList(list);
            if (item.RunewordStats != null) ValidateStatList(item.RunewordStats);
            if (item.Stats.Any(s => s.Id == StatId.NumSockets))
                throw new InvalidOperationException("Socket stat must be handled as socket metadata, not as an item modifier.");
        }

        void ApplySequence(Item item, List<Stat> stats, Dictionary<string, string> row, string cp, string pp, string lp, string hp, int count)
        {
            for (int i = 1; i <= count; i++)
            {
                string code = V(row, cp + i);
                if (code.Length > 0) ApplyProperty(item, stats, code, V(row, pp + i), V(row, lp + i), V(row, hp + i));
            }
        }

        void ApplyProperty(Item item, List<Stat> stats, string code, string parameter, string min, string max)
        {
            if (!properties.TryGetValue(code, out var property)) throw new NotSupportedException("Unknown property: " + code);
            // Several functions use Min and Max as different fields (proc chance/level,
            // damage minimum/maximum); they are not always an ordinary roll interval.
            int lo = Parse(min), hi = Parse(max), common = lo <= hi ? Pick(lo, hi) : lo;
            for (int i = 1; i <= 7; i++)
            {
                string f = V(property, "func" + i);
                if (f.Length == 0) continue;
                int func = Parse(f), layer = 0;
                long value;
                string stat = V(property, "stat" + i);
                switch (func)
                {
                    case 1: case 2: case 3: case 8: value = common; break;
                    case 15:
                        if (stat == "mindamage") { ApplyFlatDamage(item, stats, lo, true); continue; }
                        value = lo; break;
                    case 16:
                        if (stat == "maxdamage") { ApplyFlatDamage(item, stats, hi, false); continue; }
                        value = hi; break;
                    case 17:
                        value = Parse(parameter) != 0 ? Parse(parameter) : common;
                        if (stat == "maxdamage") { ApplyFlatDamage(item, stats, checked((int)value), false); continue; }
                        break;
                    case 10:
                        int tab = Parse(parameter);
                        layer = tab % 3 + 8 * (tab / 3); value = common;
                        break;
                    case 21: layer = Parse(V(property, "val" + i)); value = common; break;
                    case 22: case 24:
                        layer = stat == "state" && states.TryGetValue(parameter, out int stateId) ? stateId : ResolveSkill(parameter);
                        value = common; break;
                    case 11:
                        layer = (ResolveSkill(parameter) << 6) | hi;
                        value = lo == 0 ? 5 : lo;
                        break;
                    case 12:
                        layer = PreferredRandomSkillId >= lo && PreferredRandomSkillId <= hi ? PreferredRandomSkillId : Pick(lo, hi);
                        value = Parse(parameter);
                        Notes.Add(code + " resolved to skill " + layer + " at +" + value);
                        break;
                    case 19:
                        if (lo < 1 || lo > 255 || hi < 1 || hi > 63) throw new NotSupportedException("Las cargas requieren cantidad y nivel explícitos dentro del rango nativo.");
                        layer = (ResolveSkill(parameter) << 6) | hi;
                        value = (lo << 8) | lo;
                        break;
                    case 13:
                        value = common;
                        if (item.MaxDurability is > 0)
                            item.Durability = Math.Min(255, item.MaxDurability.Value * (100 + common) / 100);
                        break;
                    case 18:
                        // Native time-of-day fields pack phase + two signed
                        // ten-bit endpoints into one value; they are not a roll.
                        value = Math.Clamp(Parse(parameter), 0, 3) + 4L * (Math.Clamp(lo + 256, 0, 1023) + (Math.Clamp(hi + 256, 0, 1023) << 10));
                        break;
                    case 23:
                        // The property applies the ethereal bonus, unlike the
                        // random-drop operation which also halves durability.
                        if (!item.Flags.HasFlag(ItemFlags.Ethereal) && item.MaxDurability is > 0)
                        {
                            item.Flags |= ItemFlags.Ethereal;
                            if (item.Defense.HasValue) item.Defense = item.Defense.Value * 3 / 2;
                        }
                        continue;
                    case 20:
                        AddStat(stats, "item_indesctructible", 1, 0);
                        continue;
                    case 7:
                        AddStat(stats, "item_maxdamage_percent", common, 0);
                        AddStat(stats, "item_mindamage_percent", common, 0);
                        continue;
                    case 5:
                        ApplyFlatDamage(item, stats, common, true);
                        continue;
                    case 6:
                        ApplyFlatDamage(item, stats, common, false);
                        continue;
                    case 14:
                        var baseRow = bases[item.ItemCodeString];
                        int capacity = Math.Min(I(baseRow, "gemsockets"), I(baseRow, "invwidth") * I(baseRow, "invheight"));
                        int socketCount = Math.Min(capacity, Math.Max(1, common));
                        if (socketCount > 0)
                        {
                            item.Flags |= ItemFlags.Socketed;
                            item.Sockets = Enumerable.Repeat<Item?>(null, socketCount).ToList();
                        }
                        continue;
                    default: throw new NotSupportedException("Property " + code + " uses unsupported function " + func + ". No item was written.");
                }
                if (stat.Length == 0) throw new NotSupportedException("Property " + code + " requires an implicit stat (function " + func + ").");
                AddStat(stats, stat, value, layer);
            }
        }

        // Behavioral reference (independent C# implementation):
        // https://github.com/ThePhrozenKeep/D2MOO/blob/master/source/D2Common/src/Items/ItemMods.cpp
        // PropertyFunc05/06 are also used by funcs15/16/17.
        // Non-weapons affect all damage modes; weapon bonuses affect only modes
        // present on their native base. Testing the 2handed flag alone is wrong
        // for barbarian swords and omits javelin/throwing damage entirely.
        void ApplyFlatDamage(Item item, List<Stat> stats, int amount, bool minimum)
        {
            var row = bases[item.ItemCodeString];
            bool weapon = baseKinds[item.ItemCodeString] == "weapons";
            int one = I(row, minimum ? "mindam" : "maxdam");
            int two = I(row, minimum ? "2handmindam" : "2handmaxdam");
            int missile = I(row, minimum ? "minmisdam" : "maxmisdam");
            bool throwable = I(row, "minmisdam") > 0 || I(row, "maxmisdam") > 0 || IsType(V(row, "type"), "thro", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            void Add(string name, int baseDamage)
            {
                int value = amount;
                if (baseDamage != 0 && baseDamage + value <= 0) value = (minimum ? 1 : 0) - baseDamage;
                if (value != 0) AddStat(stats, name, value, 0);
            }
            if (!weapon || one != 0 || two == 0) Add(minimum ? "mindamage" : "maxdamage", one);
            if (!weapon || two != 0 || one == 0) Add(minimum ? "secondary_mindamage" : "secondary_maxdamage", two);
            if (!weapon || throwable) Add(minimum ? "item_throw_mindamage" : "item_throw_maxdamage", missile);
        }

        void AddStat(List<Stat> list, string name, long value, int layer)
        {
            if (!statRows.TryGetValue(name, out var row)) throw new InvalidDataException("Stat missing from installed tables: " + name);
            int bits = I(row, "Save Bits");
            if (bits == 0)
            {
                Notes.Add("Runtime-only stat is not serialized: " + name);
                return;
            }
            int shift = I(row, "ValShift");
            long semantic = checked(value << shift);
            var id = (StatId)I(row, "*ID");
            var existing = list.FirstOrDefault(s => s.Id == id && s.Layer == layer);
            if (existing == null) list.Add(new Stat { Id = id, Layer = layer, Value = semantic });
            else existing.Value = checked(existing.Value + semantic);
        }

        void ValidateStatList(List<Stat> list)
        {
            foreach (Stat s in list)
            {
                var row = statRows.Values.Single(r => I(r, "*ID") == (int)s.Id);
                int bits = I(row, "Save Bits"), shift = I(row, "ValShift"), paramBits = I(row, "Save Param Bits");
                long encoded = (s.Value >> shift) + I(row, "Save Add");
                if (bits > 0 && bits < 32)
                {
                    // Native ITEMS_WriteBitsToBitstream saturates generated
                    // table values to their saved field. Explicit workshop raw
                    // values are validated separately, without this adjustment.
                    // Reference: D2MOO D2Common/src/Items/Items.cpp.
                    long limited = Math.Clamp(encoded, 0, (1L << bits) - 1);
                    if (limited != encoded)
                    {
                        s.Value = (limited - I(row, "Save Add")) << shift;
                        Notes.Add("Valor nativo ajustado al campo guardable: " + V(row, "Stat"));
                        encoded = limited;
                    }
                }
                if (bits <= 0 || bits > 32 || encoded < 0 || encoded > ((1L << bits) - 1))
                    throw new InvalidDataException("Stat does not fit installed save encoding: " + V(row, "Stat"));
                if (s.Layer < 0 || (paramBits == 0 ? s.Layer != 0 : (long)s.Layer >= (1L << paramBits)))
                    throw new InvalidDataException("Stat parameter does not fit installed save encoding: " + V(row, "Stat"));
            }
        }

        int ResolveSkill(string name)
        {
            if (int.TryParse(name, out int id)) return id;
            if (skills.TryGetValue(name, out id)) return id;
            throw new InvalidDataException("Skill name not found in installed table: " + name);
        }

        bool IsType(string actual, string expected, HashSet<string> visited)
        {
            if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) return true;
            if (!visited.Add(actual) || !types.TryGetValue(actual, out var row)) return false;
            return new[] { V(row, "Equiv1"), V(row, "Equiv2") }.Any(t => t.Length > 0 && IsType(t, expected, visited));
        }

        int Pick(int min, int max)
        {
            if (min > max) throw new InvalidDataException("Invalid ordinary roll range " + min + ".." + max);
            return RollMode.ToLowerInvariant() switch
            {
                "min" => min, "max" => max, "mid" => min + (max - min) / 2,
                _ => throw new ArgumentException("RollMode must be min, mid, or max.")
            };
        }

        static void CheckSpawnable(Dictionary<string, string> r, string name)
        {
            if (V(r, "disabled") == "1" || V(r, "spawnable") != "1") throw new InvalidOperationException("Item is disabled or nonspawnable: " + name);
        }
        static ushort CheckedUShort(string s) => checked((ushort)Parse(s));
        static int Parse(string s) => string.IsNullOrWhiteSpace(s) ? 0 : int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
        static int I(Dictionary<string, string> r, string c) => Parse(V(r, c));
        static string V(Dictionary<string, string> r, string c) => r.TryGetValue(c, out var v) ? v : "";
        static Dictionary<string, string> FindOne(List<Dictionary<string, string>> rows, string col, string value)
        {
            var hits = rows.Where(r => V(r, col).Equals(value, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count != 1) throw new ArgumentException("Expected exactly one " + col + " row: " + value);
            return hits[0];
        }
        static Dictionary<string, Dictionary<string, string>> Index(List<Dictionary<string, string>> rows, string key)
        {
            var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows) if (V(r, key).Length > 0) map.TryAdd(V(r, key), r);
            return map;
        }
        static List<Dictionary<string, string>> ReadTable(string path)
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length < 2) throw new InvalidDataException("Empty table: " + path);
            string[] headers = lines[0].TrimStart('\uFEFF').Split('\t');
            var rows = new List<Dictionary<string, string>>();
            foreach (string line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] cells = line.Split('\t');
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++) row[headers[i]] = i < cells.Length ? cells[i].Trim().Trim('"') : "";
                rows.Add(row);
            }
            return rows;
        }
    }
}
