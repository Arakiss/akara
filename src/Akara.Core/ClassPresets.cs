using ForjaHoradrica.Characters;
using D2SSharp.Enums;
using D2SSharp.Model;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private static object[] PresetEntries(GameData p, bool guide)
    {
        var entries = new List<object> { new { id = "orbe-tal-oculus", name = "Orbe helado · Tal Rasha y Oculus", classId = 1, className = "Sorceress", description = "Orbe helado con tres piezas de Tal Rasha y equipo nativo.", level = 85, guide = guide ? (object)new { opening = "Activa la armadura de hielo y usa Teletransporte para colocarte.", strategy = "Orbe helado es el ataque principal; Campo estático reduce la vida de jefes. El equipo se conserva al cambiar de conjunto.", hotkeys = new[] { new { key = "F1", skill = "Frozen Orb" }, new { key = "F2", skill = "Teleport" }, new { key = "F3", skill = "Static Field" }, new { key = "F4", skill = "Frozen Armor" } } } : null } };
        entries.AddRange(PresetCatalog.List(p).Select(d => (object)new { d.Id, d.Name, d.ClassId, d.ClassName, d.Description, level = 85, guide = guide ? PresetCatalog.Guide(p, d.Id) : null }));
        return entries.ToArray();
    }
    public object Presets(string? profileId = null) => new { entries = PresetEntries(ResolveProfile(profileId), true) };
    private static PreviewRequest ApplyClassPreset(PreviewRequest request, D2Save save, GameData p)
    {
        var definition = PresetCatalog.List(p).FirstOrDefault(d => d.Id == request.Preset) ?? throw new ApiProblem("preset_unknown", "La plantilla no existe en este perfil.");
        if (definition.ClassId != (int)save.Character.Class) throw new ApiProblem("preset_class", "La plantilla corresponde a otra clase.");
        var value = PresetCatalog.Create(p, definition.Id); value.SessionId = request.SessionId; value.Name = request.Name;
        value.Level = request.Level ?? value.Level;
        if (request.Attributes is not null) foreach (var kv in request.Attributes) value.Attributes![kv.Key] = kv.Value;
        value.Skills = request.Skills ?? value.Skills; value.Gear = (value.Gear ?? []).Concat(request.Gear ?? []).ToList();
        value.ItemOperations = request.ItemOperations; value.ItemStats = request.ItemStats; value.World = GoldenBirdRewards(request.World); value.Experience=request.Experience; value.Status=request.Status;
        value.MoveToStashItemIds = (request.MoveToStashItemIds ?? []).Concat(save.Items.Select((item, index) => new { item, index }).Where(x => x.item.Position.Mode == ItemMode.Stored && x.item.Position.StorePage == StorePage.Inventory && x.item.ItemCodeString is "cm1" or "cm2" or "cm3").Select(x => "i" + x.index)).Distinct().ToList();
        value.Hotkeys = request.Hotkeys ?? value.Hotkeys; if(request.HotkeySides is not null){value.HotkeySides??=new();foreach(var kv in request.HotkeySides)value.HotkeySides[kv.Key]=kv.Value;} value.LeftSkill = request.LeftSkill ?? value.LeftSkill; value.RightSkill = request.RightSkill ?? value.RightSkill;value.SwitchLeftSkill=request.SwitchLeftSkill;value.SwitchRightSkill=request.SwitchRightSkill;
        value.Mercenary = request.Mercenary ?? value.Mercenary; value.QuestRewards = request.QuestRewards ?? value.QuestRewards; value.ActiveWeaponSet = request.ActiveWeaponSet ?? value.ActiveWeaponSet;
        return value;
    }
    private object[] SaveDirectories()
    {
        if (!Directory.Exists(saveRoot)) return [];
        var dirs = new List<string> { saveRoot }; string mods = Path.Combine(saveRoot, "mods"); if (Directory.Exists(mods)) dirs.AddRange(Directory.EnumerateDirectories(mods).Take(40));
        return dirs.Select(path => (object)new { path, label = Path.GetFileName(path), profileId = path.Contains("Reimagined", StringComparison.OrdinalIgnoreCase) ? profiles.Values.FirstOrDefault(p => p.StatRows.Values.Any(r => GameData.V(r, "Stat") == "charm_weight"))?.Id : null }).ToArray();
    }
}
