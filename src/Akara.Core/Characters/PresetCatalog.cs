using D2SSharp.Enums;
using D2SSharp.Model;

namespace ForjaHoradrica.Characters;

public sealed record PresetDefinition(string Id, string Name, int ClassId, string ClassName, string Description);

/// <summary>Native Reimagined level85 presets resolved against the active tables.</summary>
public static class PresetCatalog
{
    private sealed record Build(string Id, string Class, string Name, string CharacterName, string Description, int Strength, int Dexterity, int EnergyExtra,
        string Skiller, Dictionary<string, int> Skills, string[] Hotkeys, string Left, string Right);

    private static Dictionary<string, int> S(params (string Name, int Points)[] skills) => skills.ToDictionary(s => s.Name, s => s.Points, StringComparer.OrdinalIgnoreCase);
    private static readonly Build[] Builds =
    [
        new("squad-sorceress", "Sorceress", "Hechicera · Orbe y tormenta", "Akara-Hechicera", "Orbe helado, Relámpago y Cadena de rayos, con Tormenta activa. Tres piezas de Tal Rasha, Oculus, equipoII de buffs y mercenario completo con Infinito.",124,25,0,"Chilling",
            S(("Ice Bolt",1),("Frost Nova",1),("Frozen Orb",25),("Cold Mastery",1),("Frozen Armor",1),("Warmth",1),("Charged Bolt",1),("Static Field",1),("Telekinesis",1),("Nova",1),("Lightning",25),("Chain Lightning",25),("Teleport",1),("Thunder Storm",1),("Lightning Mastery",10)),
            ["Frozen Orb","Teleport","Static Field","Frozen Armor","Thunder Storm","Chain Lightning","Lightning","townportal"],"Attack","Frozen Orb"),
        new("squad-amazon", "Amazon", "Amazona · Furia de rayos", "Akara-Amazona", "Jabalinas eléctricas, Golpe cargado para jefes, Jab físico y Valquiria. Equipo de nivel 85 nativo, mercenario completo y equipo II de buffs.", 136, 131, 0, "Harpoonist's",
            S(("Lightning Fury",25),("Charged Strike",25),("Lightning Strike",25),("Lightning Bolt",12),("Critical Strike",1),("Penetrate",1),("Pierce",1),("Dodge",1),("Evade",1),("Inner Sight",1),("Slow Missiles",1),("Valkyrie",1),("Jab",1)),
            ["Lightning Fury","Charged Strike","Valkyrie","Teleport","Inner Sight","Slow Missiles","Jab","Lightning Strike"],"Charged Strike","Lightning Fury"),
        new("squad-paladin", "Paladin", "Paladín · Martillos", "Akara-Paladin", "Martillos con Concentración, Escudo sagrado y movilidad. El equipo principal alcanza el escalón125 FCR con propiedades nativas.", 131, 150, 0, "Lion Branded",
            S(("Blessed Hammer",25),("Vigor",25),("Blessed Aim",25),("Concentration",12),("Holy Bolt",1),("Might",1),("Prayer",1),("Cleansing",1),("Defiance",1),("Smite",1),("Shield Throw",1),("Holy Shield",1),("Redemption",1)),
            ["Blessed Hammer","Concentration","Holy Shield","Teleport","Vigor","Redemption","Cleansing","Smite"],"Blessed Hammer","Concentration"),
        new("squad-barbarian", "Barbarian", "Bárbaro · Torbellino", "Akara-Barbaro", "Doble Grief, Torbellino y Berserk contra inmunidad física. Gritos nativos, hallazgo de objetos y mercenario con Decrepificar.", 136, 136, 0, "Sounding",
            S(("Whirlwind",25),("Melee Mastery",25),("Battle Orders",25),("Shout",6),("Bash",1),("Stun",1),("Concentrate",1),("Leap",1),("Leap Attack",1),("Howl",1),("Battle Command",1),("Iron Skin",1),("Natural Resistance",1),("Increased Speed",1),("Find Potion",1),("Find Item",1),("Berserk",1),("Taunt",1),("Battle Cry",1)),
            ["Whirlwind","Berserk","Find Item","Leap Attack","Shout","Battle Cry","Find Potion","Leap"],"Berserk","Whirlwind"),
        new("squad-necromancer", "Necromancer", "Nigromante · Ejército y explosiones", "Akara-Necro", "Esqueletos, Amplificar daño y Explosión de cadáveres. Golem, resucitados y alternativas de preinvocación y Fanatismo en alijo.", 131, 40, 0, "Graverobber's",
            S(("Raise Skeleton",25),("Skeleton Mastery",25),("Corpse Explosion",25),("Raise Skeletal Mage",8),("Clay Golem",1),("Golem Mastery",1),("Summon Resist",1),("BloodGolem",1),("IronGolem",1),("Revive",1),("Teeth",1),("Bone Armor",1),("Amplify Damage",1),("Decrepify",1),("Dim Vision",1),("Lower Resist",1),("Corpse Field",1)),
            ["Raise Skeleton","Corpse Explosion","Amplify Damage","Teleport","Clay Golem","Revive","Decrepify","Raise Skeletal Mage"],"Attack","Corpse Explosion"),
        new("squad-druid", "Druid", "Druida · Viento y huracán", "Akara-Druida", "Tornado físico y Huracán de frío, Armadura de ciclón, Roble y Oso. Mercenario con Decrepificar para apoyar el daño físico.", 131, 20, 0, "Nature's",
            S(("Tornado",25),("Hurricane",25),("Twister",25),("Cyclone Armor",15),("Oak Sage",3),("Raven",1),("Summon Grizzly",1),("Summon Spirit Wolf",1)),
            ["Tornado","Hurricane","Cyclone Armor","Teleport","Oak Sage","Summon Grizzly","Raven","Summon Spirit Wolf"],"Tornado","Teleport"),
        new("squad-assassin", "Assassin", "Asesina · Trampas de rayos", "Akara-Asesina", "Centinela de rayos, Centinela mortal, fuego auxiliar y Maestra de las sombras. Ráfaga de velocidad para colocar trampas; Fundido como defensa alternativa.", 131, 85, 0, "Entrapping",
            S(("Lightning Sentry",25),("Charged Bolt Sentry",25),("Shock Field",25),("Death Sentry",10),("Claw Mastery",1),("Quickness",1),("Fade",1),("Weapon Block",1),("Shadow Warrior",1),("Shadow Master",5),("Fire Trauma",1)),
            ["Lightning Sentry","Death Sentry","Shadow Master","Teleport","Quickness","Fade","Charged Bolt Sentry","Fire Trauma"],"Attack","Lightning Sentry"),
        new("squad-warlock", "Warlock", "Brujo · Apocalipsis y demonios", "Akara-Brujo", "Apocalipsis, Ola de llamas y sus sinergias nativas; dominio demoníaco y todas las invocaciones, pacto y vínculo desbloqueados legalmente.", 131, 20, 40, "Chaotic",
            S(("Apocalypse",25),("Flame Wave",25),("Ring of Fire",25),("Sigil Lethargy",1),("Sigil Rancor",1),("Sigil Death",1),("Summon Goatman",1),("Demonic Mastery",9),("Blood Oath",1),("Death Mark",1),("Blood Boil",1),("Engorge",1),("Summon Tainted",1),("Summon Defiler",1),("Bind Demon",1),("Consume",1)),
            ["Apocalypse","Flame Wave","Summon Defiler","Teleport","Blood Oath","Death Mark","Consume","Bind Demon"],"Attack","Apocalypse")
    ];

    public static IReadOnlyList<PresetDefinition> List(GameData profile) => Builds
        .Where(b => profile.Read("charstats.txt").Any(r => GameData.V(r, "class") == b.Class))
        .Select(b => new PresetDefinition(b.Id, L.T(b.Name), (int)Enum.Parse<CharacterClass>(b.Class), b.Class, L.T(b.Description))).ToArray();

    public static PreviewRequest Create(GameData profile, string id)
    {
        var b = Find(id); var cls = Enum.Parse<CharacterClass>(b.Class);
        var chr = profile.Read("charstats.txt").Single(r => GameData.V(r, "class") == b.Class);
        var rows = profile.Read("skills.txt");
        int Skill(string name) => name == "Attack" ? 0 : GameData.I(rows.Single(r => GameData.V(r, "skill").Equals(name, StringComparison.OrdinalIgnoreCase)), "*Id", -1);
        int statBudget = 84 * GameData.I(chr, "StatPerLevel", 5) + 15;
        int energy = GameData.I(chr, "int") + b.EnergyExtra;
        int vitality = GameData.I(chr, "vit") + statBudget - (b.Strength - GameData.I(chr, "str")) - (b.Dexterity - GameData.I(chr, "dex")) - b.EnergyExtra;
        if (statBudget != 435 || vitality < GameData.I(chr, "vit")) throw new ApiProblem("preset_attributes", L.T("El perfil no admite el presupuesto de 435 de esta plantilla."));
        int offset = SkillsSection.ClassSkillOffsets[(int)cls];
        if (b.Skills.Values.Sum() != 96) throw new ApiProblem("preset_skills", L.T("El catálogo no suma 96 puntos."));
        foreach (var skill in b.Skills)
        {
            var row = rows.Single(r => GameData.V(r, "skill").Equals(skill.Key, StringComparison.OrdinalIgnoreCase)); int skillId = GameData.I(row, "*Id", -1);
            if (skillId < offset || skillId >= offset + 30 || skill.Value > GameData.I(row, "maxlvl") || skill.Value > 85 - GameData.I(row, "reqlevel") + 1)
                throw new ApiProblem("preset_skill_cap", L.T("La habilidad no corresponde al perfil de la plantilla: ") + skill.Key);
            foreach (string key in new[] { "reqskill1", "reqskill2", "reqskill3" })
            {
                string dependency = GameData.V(row, key);
                if (dependency.Length > 0 && (!b.Skills.TryGetValue(dependency, out int points) || points < 1)) throw new ApiProblem("preset_prerequisite", skill.Key + L.T(" necesita ") + dependency);
            }
        }
        var request = new PreviewRequest
        {
            Level = 85, ActiveWeaponSet = 1,
            Attributes = new(StringComparer.OrdinalIgnoreCase) { ["strength"] = b.Strength, ["dexterity"] = b.Dexterity, ["energy"] = energy, ["vitality"] = vitality, ["gold"] = 100000, ["stashGold"] = 500000 },
            Skills = b.Skills.Select(s => new SkillChange(Skill(s.Key), s.Value)).ToList(),
            QuestRewards = new() { Skills = true, Stats = true, Anya = true }, UnlockHell = true, UnlockWaypoints = true,
            FillTomes = true, FillBelt = true, Mercenary = new(11, 85, true),
            World = new() { Quests = new[]{"normal","nightmare","hell"}.Select(d=>new QuestChange(d,"act3.the-golden-bird","rewarded")).ToList() },
            LeftSkill = Skill(b.Left), RightSkill = Skill(b.Right),
            Hotkeys = b.Hotkeys.Select((name, index) => (name, index)).ToDictionary(x => x.index, x => Skill(x.name)),
            Gear = Equipment(b)
        };
        request.Hotkeys[8] = Skill("Battle Command"); request.Hotkeys[9] = Skill("Battle Orders");
        if(b.Class=="Paladin")request.HotkeySides=new(){[0]="left",[7]="left"};
        foreach (var gear in request.Gear)
        {
            if (gear.Type == "unique" && !profile.Read("uniqueitems.txt").Any(r => GameData.V(r, "index").Equals(gear.Name, StringComparison.OrdinalIgnoreCase) && GameData.V(r, "disabled") != "1" && GameData.I(r, "lvl req") <= 85)) throw new ApiProblem("preset_item", L.T("El objeto nativo no está disponible a nivel 85: ") + gear.Name);
            if (gear.Type == "set" && !profile.Read("setitems.txt").Any(r => GameData.V(r, "index").Equals(gear.Name, StringComparison.OrdinalIgnoreCase) && GameData.I(r, "lvl req") <= 85)) throw new ApiProblem("preset_item", L.T("La pieza de conjunto no está disponible a nivel 85: ") + gear.Name);
        }
        profile.EnsureUnchanged(); return request;
    }

    private static List<GearChange> Equipment(Build b)
    {
        var g = new List<GearChange>();
        void U(string name, string? slot = null, params string[] sockets) => g.Add(new() { Type = "unique", Name = name, Slot = slot, Level = 85, Page = slot is null ? "stash" : null, Sockets = sockets.ToList() });
        void T(string name, string? slot = null, params string[] sockets) => g.Add(new() { Type = "set", Name = name, Slot = slot, Level = 85, Page = slot is null ? "stash" : null,Sockets=sockets.ToList() });
        void R(string name, string code, string? slot = null, int? set = null) => g.Add(new() { Type = "runeword", Name = name, BaseCode = code, Slot = slot, WeaponSet = set, Level = 85, Page = slot is null ? "stash" : null });
        if (b.Class == "Barbarian")
        {
            U("Arreat's Face", "helm", "r22"); R("Grief", "7cr", "weapon", 1); R("Grief", "7cr", "shield", 1);
            R("Fortitude", "utp", "armor"); U("Highlord's Wrath", "amulet"); U("String of Ears", "belt"); T("Laying of Hands", "gloves"); U("Gore Rider", "boots");
            U("Raven Frost", "ring1"); U("Bul-Kathos' Wedding Band", "ring2");
        }
        else if(b.Class=="Sorceress")
        {
            U("Harlequin Crest","helm","r22");U("The Oculus","weapon");R("Spirit","uit","shield",1);
            T("Tal Rasha's Guardianship","armor","r22");T("Tal Rasha's Adjudication","amulet");T("Tal Rasha's Fine-Spun Cloth","belt");
            U("Magefist","gloves");U("War Traveler","boots");U("The Stone of Jordan","ring1");U("The Stone of Jordan","ring2");
        }
        else
        {
            string helm = b.Class is "Amazon" or "Paladin" or "Assassin" ? "Griffon's Eye" : b.Class == "Druid" ? "Jalal's Mane" : "Harlequin Crest";
            U(helm, "helm", "r22");
            if (b.Class == "Amazon") U("Titan's Revenge", "weapon"); else R("Heart of the Oak", "fla", "weapon", 1);
            if (b.Class == "Necromancer") U("Homunculus", "shield", "r22"); else R("Spirit", "uit", "shield", 1);
            R("Enigma", "utp", "armor"); U("Mara's Kaleidoscope", "amulet");
            if (b.Class == "Amazon") { U("Thundergod's Vigor", "belt"); T("Laying of Hands", "gloves"); U("Silkweave", "boots"); U("Raven Frost", "ring1"); U("The Stone of Jordan", "ring2"); }
            else { U("Arachnid Mesh", "belt"); if (b.Class == "Assassin") T("Laying of Hands", "gloves"); else if (b.Class == "Necromancer") T("Trang-Oul's Claws", "gloves"); else U("Magefist", "gloves"); U(b.Class == "Necromancer" ? "Marrowwalk" : "War Traveler", "boots"); U("The Stone of Jordan", "ring1"); U("The Stone of Jordan", "ring2"); }
        }
        R("Call to Arms", "crs", "weapon", 2); R("Spirit", "uit", "shield", 2);
        foreach (string unique in new[] { "Annihilus", "Hellfire Torch", "Gheed's Fortune" }) g.Add(new() { Type = "unique", Name = unique, Level = 85, Page = "inventory" });
        for (int n = 0; n < 4; n++) {string skiller=b.Class=="Sorceress"&&n>=2?"Sparking":b.Skiller;g.Add(new() { Type = "magic", BaseCode = "cm3", Name = skiller + " Grand Charm of Substinence", Prefix = skiller, Suffix = "of Substinence", Level = 85, Page = "inventory" });}
        for (int n = 0; n < (b.Class=="Sorceress"?10:16); n++) g.Add(new() { Type = "magic", BaseCode = "cm1", Name = "Ancient Small Charm", Prefix = "Ancient", Level = 85, Page = "inventory" });
        switch (b.Class)
        {
            case "Sorceress":U("Nightwing's Veil");U("Arachnid Mesh");U("Mara's Kaleidoscope");U("Raven Frost");U("Thundergod's Vigor");break;
            case "Amazon": U("Thunderstroke"); U("Stormshield"); U("Razortail"); U("Highlord's Wrath"); R("Chains of Honor", "utp"); U("War Traveler"); break;
            case "Paladin": U("Herald of Zakarum"); U("Harlequin Crest"); U("Skin of the Vipermagi"); U("Thundergod's Vigor"); break;
            case "Barbarian": R("Enigma", "utp"); U("Stormshield"); U("Dracul's Grasp"); T("Guillaume's Face"); U("Thundergod's Vigor"); break;
            case "Necromancer": U("Arm of King Leoric"); R("Beast", "7wa"); R("Spirit", "uit"); T("Trang-Oul's Wing"); U("Harlequin Crest"); break;
            case "Druid": U("Harlequin Crest"); U("Nightwing's Veil"); U("Stormshield"); U("Skin of the Vipermagi"); break;
            case "Assassin": U("Bartuc's Cut-Throat"); U("Jade Talon"); U("Magefist"); U("Stormshield"); U("Raven Frost"); break;
            case "Warlock": U("Ars Al'Diablolos"); U("Codex of the Crimson Pyre"); T("Trang-Oul's Claws"); U("Skin of the Vipermagi"); break;
        }
        for (int n = 0; n < 3; n++) g.Add(new() { Type = "base", Name = "Token of Absolution", BaseCode = "toa", Level = 85, Page = "stash" });
        string mercWeapon = b.Class is "Barbarian" or "Druid" ? "The Reaper's Toll" : b.Class == "Paladin" ? "Insight" : "Infinity";
        g.Add(new() { Type = mercWeapon == "The Reaper's Toll" ? "unique" : "runeword", Name = mercWeapon, BaseCode = "7s8", MercSlot = "weapon", Ethereal = true, Level = 85 });
        g.Add(new() { Type = "runeword", Name = "Fortitude", BaseCode = "utp", MercSlot = "armor", Ethereal = true, Level = 85 });
        g.Add(new() { Type = "unique", Name = "Andariel's Visage", MercSlot = "helm", Ethereal = true, Level = 85, Sockets = ["r08"] });
        g.Add(new() { Type = "unique", Name = "Mara's Kaleidoscope", MercSlot = "amulet", Level = 85 });
        g.Add(new() { Type = "set", Name = "Laying of Hands", MercSlot = "gloves", Level = 85 });
        g.Add(new() { Type = "unique", Name = "String of Ears", MercSlot = "belt", Level = 85 });
        g.Add(new() { Type = "unique", Name = "Gore Rider", MercSlot = "boots", Level = 85 });
        g.Add(new() { Type = "unique", Name = "Raven Frost", MercSlot = "ring1", Level = 85 });
        g.Add(new() { Type = "unique", Name = "Wisp Projector", MercSlot = "ring2", Level = 85 });
        return g;
    }

    public static object Guide(GameData profile, string id)
    {
        var b = Find(id); var request = Create(profile, id);
        string strategy = b.Class switch
        {
            "Sorceress" => L.T("Activa Armadura de hielo con F4 y Tormenta con F5. Orbe helado(F1) alterna con Cadena de rayos(F6) para grupos y Relámpago(F7) contra objetivos resistentes al frío. Campo estático(F3) abre los combates con jefes; Teletransporte(F2) reúne al mercenario y coloca el Orbe. F8 abre portal."),
            "Amazon" => L.T("F1 contra grupos; F2 cuerpo a cuerpo contra jefes. F7 Jab es la alternativa física si una inmunidad eléctrica permanece. Invoca Valquiria con F3; F4 teletransporta."),
            "Paladin" => L.T("Deja Concentración(F2) en el botón derecho y Martillo en el izquierdo; usa Mayús+clic izquierdo para lanzar quieto. Activa Escudo sagrado(F3). Después de Teletransporte(F4), vuelve a Concentración. El mercenario cubre enemigos inmunes a magia."),
            "Barbarian" => L.T("Torbellino(F1) para grupos; Berserk(F2) convierte el ataque en daño mágico contra inmunidad física. Salto ofensivo(F4) desplaza; Grito(F5) antes de combatir y Hallar objeto(F3) al terminar. El mercenario aplica Decrepificar. Enigma queda como alternativa de movilidad."),
            "Necromancer" => L.T("Levanta esqueletos(F1), magos(F8), golem(F5) y resucitados(F6). Amplifica daño(F3); tras la primera baja, encadena Explosión de cadáveres(F2). Decrepificar(F7) controla jefes; Teletransporte(F4) reúne el ejército. Leoric sirve para preinvocar y Beast para Fanatismo alternativo."),
            "Druid" => L.T("Invoca Roble(F5), Oso(F6) y apoyo(F7/F8). Activa Huracán(F2) y Armadura de ciclón(F3), y renuévalos cuando expiren. Tornado(F1) es físico y Huracán es frío; el mercenario aplica Decrepificar para apoyar el daño físico."),
            "Assassin" => L.T("Invoca Maestra de las sombras(F3); Ráfaga de velocidad(F5) acelera la colocación de trampas. Pon Centinelas de rayos(F1) y añade Centinela mortal(F2) al caer el primer enemigo. La bomba de fuego(F8) aporta otro elemento. Fundido(F6) sustituye a Ráfaga como opción defensiva."),
            _ => L.T("Invoca Desolladores(F3) y activa Pacto de sangre(F5). Marca el objetivo con F6; Apocalipsis(F1) y Ola de llamas(F2) aportan fuego. Consumir(F7) y Vincular demonio(F8) están aprendidos: el vínculo se elige dentro del juego, no se entrega un demonio inventado. Los grimorios alternativos del alijo son nativos y uno concede Reducir resistencias.")
        };
        return new
        {
            Id = b.Id, CharacterName = b.CharacterName, ClassName = b.Class, Build = L.T(b.Name), Level = 85,
            HardSkillPoints = 96, AllocatedAttributePoints = 435, Skills = b.Skills, Attributes = request.Attributes,
            Hotkeys = b.Hotkeys.Select((name, index) => new { Key = "F" + (index + 1), Skill = name, Id = request.Hotkeys![index] }).Concat(new[] { new { Key = "F9", Skill = "Battle Command", Id = 155 }, new { Key = "F10", Skill = "Battle Orders", Id = 149 } }),
            Opening = L.T("Empieza en el equipo I. Pulsa W para cambiar al equipo II: lanza Comando de batalla (F9), después Órdenes de batalla (F10), y vuelve al I con W. Son buffs temporales; hay que lanzarlos al entrar y renovarlos cuando expiren."),
            Strategy = strategy,
            Charms = L.T("Cuatro grandes de habilidades y dieciséis Ancient pequeños, más Annihilus, Torch y Gheed: peso nativo 34, inferior al límite 41. Deben permanecer en el inventario activo."),
            Resists = L.T("Los pequeños aportan 80 a todas las resistencias; Annihilus y Torch suman 20, y los tres pergaminos de Anya aportan 30. El equipo y las runas cubren el resto de la penalización de Infierno (−120). El cálculo final se verifica al generar cada partida."),
            Alternatives = request.Gear!.Where(g => g.Page == "stash").Select(g => new { g.Type, g.Name, g.BaseCode }),
            Notes = new[] { L.T("Todos los objetos nuevos tienen nivel 85, propiedades de las tablas instaladas y tiradas intermedias."), L.T("No se añadieron afijos imposibles, corrupción ni mejoras ocultas."), L.T("Hay tres Tokens of Absolution por personaje para redistribuir habilidades y atributos."), L.T("Cambiar alternativas puede modificar bonificaciones, velocidad, bloqueo y resistencias; los requisitos se comprueban con el equipo y los talismanes presentes."), L.T("La verificación binaria y de tablas no equivale a una prueba dentro del motor. El primer arranque de estas clases nuevas queda pendiente de comprobación en el juego.") }
        };
    }
    private static Build Find(string id) => Builds.SingleOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new ApiProblem("preset_unknown", L.T("La plantilla no existe: ") + id);
}
