using System.Security.Cryptography;
using D2RBuilder;
using D2SSharp.Enums;
using D2SSharp.Model;

namespace ForjaHoradrica.Characters;

/// <summary>Creates canonical v105 models in memory, without save templates or disk writes.</summary>
public sealed class NewCharacterFactory(GameData profile)
{
    public D2Save Create(string className, string name, int level = 1, uint version = 105)
    {
        if (version != 105) throw new ApiProblem("create_version", L.T("La creación está verificada para partidas de versión 105."));
        if (!Enum.TryParse<CharacterClass>(className, true, out var characterClass) || !Enum.IsDefined(characterClass))
            throw new ApiProblem("create_class", L.T("Selecciona una de las ocho clases nativas."));
        if (name.Length is < 2 or > 15 || name.Any(c => !(char.IsAsciiLetter(c) || c is '-' or '_')))
            throw new ApiProblem("create_name", L.T("El nombre debe tener entre 2 y 15 letras ASCII, guiones o guiones bajos."));
        var row = profile.Read("charstats.txt").SingleOrDefault(r => GameData.V(r, "class").Equals(characterClass.ToString(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ApiProblem("create_class", L.T("El perfil no contiene los atributos iniciales de esta clase."));
        var maximum = profile.Read("experience.txt").Single(r => GameData.V(r, "Level") == "MaxLvl");
        if (level < 1 || level > GameData.I(maximum, characterClass.ToString())) throw new ApiProblem("create_level", L.T("El nivel no corresponde al límite nativo de esta clase."));
        int offset = SkillsSection.ClassSkillOffsets[(int)characterClass];
        var classSkills = profile.Read("skills.txt").Where(r => GameData.I(r, "*Id", -1) >= offset && GameData.I(r, "*Id", -1) < offset + 30).ToList();
        if (classSkills.Count != 30 || classSkills.Any(r => GameData.V(r, "charclass").Length == 0))
            throw new ApiProblem("create_skill_layout", L.T("El perfil no contiene las 30 habilidades canónicas de esta clase."));
        uint now = checked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        uint seed; do { seed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)); } while (seed == 0);
        var save = new D2Save
        {
            Version = version,
            Character = new Character
            {
                Name = name, Class = characterClass, Flags = CharacterFlags.None,
                NumStats = 16, NumSkills = 30, Level = checked((byte)level),
                CreateTime = now, LastSaveTime = now, MapSeed = seed, WeaponSwitch = 0,
                TownDifficulty = [0x80, 0, 0],
                Preview = new PreviewData { Name = name, GameVersion = GameVersion.ReignOfTheWarlock },
                SkillHotkeys = Enumerable.Range(0, 16).Select(_ => new Skill { SkillId = ushort.MaxValue, Side = Side.Right, ItemIndex = ushort.MaxValue }).ToArray(),
                LeftSkill = new Skill { SkillId = 0, Side = Side.Left, ItemIndex = ushort.MaxValue },
                RightSkill = new Skill { SkillId = 0, Side = Side.Right, ItemIndex = ushort.MaxValue },
                SwitchLeftSkill = new Skill { SkillId = 0, Side = Side.Left, ItemIndex = ushort.MaxValue },
                SwitchRightSkill = new Skill { SkillId = 0, Side = Side.Right, ItemIndex = ushort.MaxValue }
            },
            Skills = new SkillsSection { CharacterClass = characterClass, SkillLevels = new byte[30] },
            MercItems = new MercItemsSection(), IronGolem = new IronGolemSection(), Demon = new DemonSection()
        };
        // Preview XP/save-time slots remain zero, matching the project's native
        // v105 fixtures. Authoritative level/XP live in the character and stats;
        // no unsupported assumption is made about the six preview mode slots.
        int I(string key) => GameData.I(row, key);
        int strength = I("str"), energy = I("int"), dexterity = I("dex"), vitality = I("vit");
        long life = (vitality + I("hpadd")) * 256L + (level - 1) * I("LifePerLevel") * 64L;
        long mana = energy * 256L + (level - 1) * I("ManaPerLevel") * 64L;
        long stamina = I("stamina") * 256L + (level - 1) * I("StaminaPerLevel") * 64L;
        var xp = profile.Read("experience.txt").Single(r => GameData.V(r, "Level") == (level - 1).ToString());
        long[] stats = [strength, energy, dexterity, vitality, (level - 1) * I("StatPerLevel"), (level - 1) * I("SkillsPerLevel"), life, life, mana, mana, stamina, stamina, level, long.Parse(GameData.V(xp, characterClass.ToString())), 0, 0];
        for (int id = 0; id < stats.Length; id++) save.Stats.SetStat((StatId)id, stats[id]);
        // The mod gives these utility items to every class. Add only bases that
        // exist in this profile, and pack them using its actual inventory grid.
        var factory = new ItemFactory(profile.ExcelPath) { ItemFormatVersion = 101, RollMode = "mid" };
        foreach (string code in new[] { "tbk", "ibk", "box", "bag" })
        {
            if (!profile.Bases.ContainsKey(code)) continue;
            var item = factory.CreateNormal(code, 1);
            if (code is "tbk" or "ibk")
            {
                item.Quantity = GameData.I(profile.Bases[code], "maxstack", 20);
                item.QualityData = new NormalScrollBookQualityData { SpellType = (byte)(code == "ibk" ? 1 : 0) };
            }
            PackInventory(save, item);
        }
        profile.EnsureUnchanged();
        var bytes = save.ToBytes(profile.External(version), version);
        var readback = D2Save.Read(bytes, profile.External(version));
        if (!D2Save.VerifyChecksum(bytes) || !bytes.AsSpan().SequenceEqual(readback.ToBytes(profile.External(version), version)))
            throw new ApiProblem("create_roundtrip", L.T("La nueva partida no superó la comprobación de lectura y escritura."));
        if (readback.Character.Class != characterClass || readback.Character.Preview.Name != name || readback.Skills.SkillLevels.Length != 30)
            throw new ApiProblem("create_identity", L.T("La identidad nueva no sobrevivió a la codificación nativa."));
        return readback;
    }

    private void PackInventory(D2Save save, Item item)
    {
        var grid = profile.Grid(save, "inventory"); var size = profile.Size(item); var cells = new bool[grid.Width, grid.Height];
        foreach (var old in save.Items)
        {
            var oldSize = profile.Size(old);
            for (int y = old.Position.InvY; y < old.Position.InvY + oldSize.Height; y++)
                for (int x = old.Position.InvX; x < old.Position.InvX + oldSize.Width; x++) cells[x, y] = true;
        }
        for (int y = 0; y <= grid.Height - size.Height; y++) for (int x = 0; x <= grid.Width - size.Width; x++)
        {
            bool free = true;
            for (int yy = y; yy < y + size.Height; yy++) for (int xx = x; xx < x + size.Width; xx++) if (cells[xx, yy]) free = false;
            if (!free) continue;
            item.Position = new ItemPosition { Mode = ItemMode.Stored, StorePage = StorePage.Inventory, InvX = (byte)x, InvY = (byte)y };
            save.Items.Add(item); return;
        }
        throw new ApiProblem("create_inventory", L.T("No caben los objetos iniciales del perfil."));
    }
}
