using System.Text.Json.Nodes;

namespace Akara.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    // An explicitly requested design preview. It contains no save bytes, profile tables or writeable session.
    void LoadSyntheticDemo()
    {
        IsSyntheticDemo = true; sessionId = "";
        JsonObject Item(string id, string name, string code, string slot, string quality, int width = 2, int height = 2, string page = "equipped", int x = 0, int y = 0, int? weaponSet = null, string owner = "character") => new()
        {
            ["id"] = id, ["name"] = name, ["baseName"] = name, ["code"] = code, ["quality"] = quality,
            ["level"] = 85, ["slot"] = slot, ["page"] = page, ["owner"] = owner, ["width"] = width, ["height"] = height, ["x"] = x, ["y"] = y, ["weaponSet"] = weaponSet,
            ["stats"] = new JsonArray(new JsonObject { ["id"] = 127, ["label"] = "Todas las habilidades", ["layer"] = 0, ["value"] = 2, ["displayValue"] = 2 }), ["sockets"] = new JsonArray()
        };
        var items = new JsonArray
        {
            Item("i0","Cresta de arlequín","uap","helm","unique"), Item("i1","El Óculo","oba","weapon","unique",1,3,weaponSet:1),
            Item("i2","Espíritu","uit","shield","normal",2,4,weaponSet:1), Item("i3","Custodia de Tal Rasha","uth","armor","set",2,3),
            Item("i4","Juicio de Tal Rasha","amu","amulet","set",1,1), Item("i5","Malla de Tal Rasha","zmb","belt","set",2,1),
            Item("i6","Puño del mago","tgl","gloves","unique"), Item("i7","Viajero de guerra","xtb","boots","unique"),
            Item("i8","Piedra de Jordán","rin","ring1","unique",1,1), Item("i9","Piedra de Jordán","rin","ring2","unique",1,1),
            Item("i10","Llamada a las armas","crs","weapon","normal",2,3,weaponSet:2), Item("i11","Espíritu","uit","shield","normal",2,4,weaponSet:2),
            Item("i12","Tomo del portal","tbk","","normal",1,2,"inventory",0,0), Item("i13","Tomo de identificación","ibk","","normal",1,2,"inventory",1,0),
            Item("i14","Cubo horádrico","box","","normal",2,2,"inventory",2,0), Item("i15","Antorcha del Infierno","cm2","","unique",1,3,"inventory",4,0),
            Item("i16","Talismán de escarcha","cm3","","magic",1,3,"inventory",5,0), Item("i17","Talismán de rayos","cm3","","magic",1,3,"inventory",6,0),
            Item("i18","Talismán de rayos","cm3","","magic",1,3,"inventory",7,0), Item("i19","Annihilus","cm1","","unique",1,1,"inventory",8,0),
            Item("i20","Talismán antiguo","cm1","","magic",1,1,"inventory",8,1), Item("i21","Talismán antiguo","cm1","","magic",1,1,"inventory",9,1),
            Item("i22","Talismán antiguo","cm1","","magic",1,1,"inventory",0,3), Item("i23","Talismán antiguo","cm1","","magic",1,1,"inventory",1,3),
            Item("i24","Talismán antiguo","cm1","","magic",1,1,"inventory",2,3), Item("i25","Talismán antiguo","cm1","","magic",1,1,"inventory",3,3)
        };
        var merc = new JsonArray
        {
            Item("m0","Infinito","7s8","weapon","normal",2,4,owner:"mercenary"),Item("m1","Fortaleza","utp","armor","normal",2,3,owner:"mercenary"),
            Item("m2","Rostro de Andariel","usk","helm","unique",owner:"mercenary"),Item("m3","Caleidoscopio de Mara","amu","amulet","unique",1,1,owner:"mercenary"),
            Item("m4","Imposición de manos","ulg","gloves","set",owner:"mercenary"),Item("m5","Cordel de orejas","zlb","belt","unique",2,1,owner:"mercenary"),
            Item("m6","Jinete sangriento","xhb","boots","unique",owner:"mercenary"),Item("m7","Helada del cuervo","rin","ring1","unique",1,1,owner:"mercenary"),Item("m8","Proyector de briznas","rin","ring2","unique",1,1,owner:"mercenary")
        };
        merc[0]!["twoHanded"] = true;
        var skills = new JsonArray();
        foreach (var (id, name, points, tab) in new[] { (64,"Frozen Orb",25,3),(65,"Cold Mastery",1,3),(39,"Ice Bolt",1,3),(44,"Frost Nova",1,3),(40,"Frozen Armor",1,3),(49,"Lightning",25,2),(53,"Chain Lightning",25,2),(63,"Lightning Mastery",10,2),(57,"Thunder Storm",1,2),(54,"Teleport",1,2),(42,"Static Field",1,2),(37,"Warmth",1,1) })
            skills.Add(new JsonObject { ["id"] = id, ["name"] = name, ["points"] = points, ["maxPoints"] = 25, ["requiredLevel"] = 30, ["tabId"] = tab });
        var snapshot = new JsonObject
        {
            ["isNew"] = false, ["activeWeaponSet"] = 1, ["sourcePath"] = "", ["sourceHash"] = "", ["profileId"] = "design-preview",
            ["character"] = new JsonObject { ["name"] = "Seren", ["className"] = "Sorceress", ["classId"] = 1, ["level"] = 85, ["experience"] = 1050299747,
                ["attributes"] = new JsonObject { ["strength"] = 124, ["dexterity"] = 25, ["vitality"] = 331, ["energy"] = 35, ["gold"] = 200000, ["stashGold"] = 2000000, ["unspentStats"] = 0, ["unspentSkills"] = 0 },
                ["vitals"] = new JsonObject { ["maxLife"] = 986.5, ["maxMana"] = 325 } },
            ["inventoryGrid"] = new JsonObject { ["width"] = 10, ["height"] = 8 }, ["stashGrid"] = new JsonObject { ["width"] = 10, ["height"] = 10 }, ["cubeGrid"] = new JsonObject { ["width"] = 3, ["height"] = 4 },
            ["items"] = items, ["skills"] = skills, ["mercenary"] = new JsonObject { ["exists"] = true, ["typeName"] = "Guardián del desierto", ["level"] = 85, ["aura"] = "Fuerza", ["items"] = merc },
            ["validation"] = new JsonObject { ["canEdit"] = false, ["checksum"] = false, ["roundtrip"] = false }
        };
        SourceSnapshot = (JsonObject)snapshot.DeepClone(); ApplySnapshot(snapshot, true);
        foreach (var skill in Skills) skill.IsReadOnly = true;
        IsDirty = false; reviewed = validPlan = false; StatusText = "Vista de diseño · datos de muestra, sin partida abierta.";
        ValidationMessage = "Abre una partida local para editar y guardar.";
        foreach (var name in new[] { nameof(IsSyntheticDemo), nameof(DemoLabel), nameof(HasCharacter), nameof(IsCharacterSelected), nameof(CanEdit), nameof(CanCommit), nameof(ValidationMessage) }) Raise(name);
    }
}
