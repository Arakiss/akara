using System.Reflection;
using System.Text.RegularExpressions;
using D2SSharp.Enums;
using D2SSharp.Model;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private sealed record QuestAccess(string Id, string Name, int Act, object Container, PropertyInfo Property)
    {
        public QuestFlags Flags { get => (QuestFlags)Property.GetValue(Container)!; set => Property.SetValue(Container, value); }
    }
    private static int DifficultyIndex(string value) => value.ToLowerInvariant() switch { "normal" => 0, "nightmare" => 1, "hell" => 2, _ => throw new ApiProblem("difficulty_invalid", L.T("Dificultad no válida.")) };
    private static readonly string[] DifficultyNames = ["normal", "nightmare", "hell"];
    private static string QuestState(QuestFlags flags) => Reward(flags) ? "rewarded" : (flags & QuestFlags.PrimaryGoalAchieved) != 0 ? "completed" : flags == 0 ? "notStarted" : "started";
    private static IEnumerable<QuestAccess> QuestEntries(QuestsDifficulty q)
    {
        string[] names = ["ActI", "ActII", "ActIII", "ActIV", "ActV"];
        for (int act = 0; act < names.Length; act++)
        {
            var container = q.GetType().GetProperty(names[act])!.GetValue(q)!;
            foreach (var property in container.GetType().GetProperties().Where(p => p.PropertyType == typeof(QuestFlags)))
            {
                string human = Regex.Replace(property.Name, "(?<!^)([A-Z])", " $1");
                string id = "act" + (act + 1) + "." + human.Replace(' ', '-').ToLowerInvariant();
                yield return new(id, human, act + 1, container, property);
            }
        }
    }
    private static readonly string[] WaypointNames = ["Rogue Encampment", "Cold Plains", "Stony Field", "Dark Wood", "Black Marsh", "Outer Cloister", "Jail Level1", "Inner Cloister", "Catacombs Level2", "Lut Gholein", "Sewers Level2", "Dry Hills", "Halls of the Dead Level2", "Far Oasis", "Lost City", "Palace Cellar Level1", "Arcane Sanctuary", "Canyon of the Magi", "Kurast Docks", "Spider Forest", "Great Marsh", "Flayer Jungle", "Lower Kurast", "Kurast Bazaar", "Upper Kurast", "Travincal", "Durance of Hate Level2", "Pandemonium Fortress", "City of the Damned", "River of Flame", "Harrogath", "Frigid Highlands", "Arreat Plateau", "Crystalline Passage", "Halls of Pain", "Glacial Trail", "Frozen Tundra", "The Ancients' Way", "Worldstone Keep Level2"];
    private static int WaypointAct(int id) => id < 9 ? 1 : id < 18 ? 2 : id < 27 ? 3 : id < 30 ? 4 : 5;
    public object World(string sessionId, string? planId = null)
    {
        var s = Session(sessionId); lock (s.Sync)
        {
            var save = Read(planId is null ? s.Original : Plan(s, planId).Bytes, s.Profile, s.Version);
            var waypoints = new[] { save.Waypoints.Normal, save.Waypoints.Nightmare, save.Waypoints.Hell };
            int active = Array.FindIndex(save.Character.TownDifficulty, b => (b & 0x80) != 0); if (active < 0) active = 0;
            int actsPerDifficulty = save.Character.Preview.GameVersion == GameVersion.Classic ? 4 : 5;
            return new { activeDifficulty = DifficultyNames[active], act = (save.Character.TownDifficulty[active] & 7) + 1, maxDifficulty = DifficultyNames[Math.Min(2, (int)(((uint)save.Character.Flags >> 8) & 31) / actsPerDifficulty)], difficulties = Difficulties(save).Select((q, index) => new { id = DifficultyNames[index], quests = QuestEntries(q).Select(e => new { id = e.Id, name = e.Name, act = e.Act, flags = (ushort)e.Flags, state = Reward(e.Flags) ? "rewarded" : (e.Flags & QuestFlags.PrimaryGoalAchieved) != 0 ? "completed" : e.Flags == 0 ? "notStarted" : "started", rewardClaimed = Reward(e.Flags), anyaConsumed = e.Property.Name == "PrisonOfIce" && (e.Flags & QuestFlags.Custom3) != 0 }), waypoints = Enumerable.Range(0, 39).Select(id => new { id, name = WaypointNames[id], act = WaypointAct(id), active = waypoints[index].IsWaypointActive(id) }) }) };
        }
    }
    private static void ApplyWorld(D2Save save, WorldChange? change, List<ChangeEntry> diff)
    {
        if (change is null) return;
        if (change.Quests is null || change.Waypoints is null || change.Quests.Count > 300 || change.Waypoints.Count > 117) throw new ApiProblem("world_operations", L.T("La lista de cambios del mundo no es válida."));
        int beforeBirds = Difficulties(save).Count(q => Reward(q.ActIII.TheGoldenBird));
        foreach (var patch in change.Quests)
        {
            var q = Difficulties(save)[DifficultyIndex(patch.Difficulty)]; var entry = QuestEntries(q).FirstOrDefault(e => e.Id == patch.QuestId) ?? throw new ApiProblem("quest_unknown", L.T("La misión no existe: ") + patch.QuestId);
            if (patch.Flags.HasValue == (patch.State is not null)) throw new ApiProblem("quest_state", L.T("Indica state o flags para la misión."));
            var before = entry.Flags;
            if (patch.Flags.HasValue) entry.Flags = (QuestFlags)patch.Flags.Value;
            else entry.Flags = patch.State switch { "notStarted" => QuestFlags.None, "started" => QuestFlags.Started, "completed" => QuestFlags.PrimaryGoalAchieved | QuestFlags.RewardPending | QuestFlags.QuestLog, "rewarded" => Completed(entry.Flags), _ => throw new ApiProblem("quest_state", L.T("Estado de misión no válido.")) };
            if (entry.Property.Name == "PrisonOfIce" && patch.State == "rewarded") entry.Flags |= QuestFlags.Custom3 | QuestFlags.Custom4;
            if(before!=entry.Flags)diff.Add(new("world", entry.Name + " · " + patch.Difficulty, patch.Flags.HasValue ? (object)(ushort)before : QuestState(before), patch.Flags.HasValue ? (object)(ushort)entry.Flags : QuestState(entry.Flags)));
            if (entry.Property.Name == "PrisonOfIce" && (before & QuestFlags.Custom3) != (entry.Flags & QuestFlags.Custom3)) diff.Add(new("world", L.T("Resistencias de Anya · ") + patch.Difficulty, (before & QuestFlags.Custom3) != 0 ? 10 : 0, (entry.Flags & QuestFlags.Custom3) != 0 ? 10 : 0));
        }
        int lifeChange = (Difficulties(save).Count(q => Reward(q.ActIII.TheGoldenBird)) - beforeBirds) * 20 * 256;
        if (lifeChange != 0) { foreach (int id in new[] { 6, 7 }) { long value = save.Stats.GetStat((StatId)id) + lifeChange; if (value <= 0) throw new ApiProblem("quest_life", L.T("No se puede retirar esa recompensa de vida del personaje actual.")); save.Stats.SetStat((StatId)id, value); } }
        var waypoints = new[] { save.Waypoints.Normal, save.Waypoints.Nightmare, save.Waypoints.Hell };
        foreach (var patch in change.Waypoints) { if (patch.Id < 0 || patch.Id >= 39) throw new ApiProblem("waypoint_unknown", L.T("El punto de ruta no existe.")); int difficulty = DifficultyIndex(patch.Difficulty); bool before = waypoints[difficulty].IsWaypointActive(patch.Id); waypoints[difficulty].SetWaypoint(patch.Id, patch.Active); if(before!=patch.Active)diff.Add(new("world", WaypointNames[patch.Id] + " · " + patch.Difficulty, before, patch.Active)); }
        if (change.MaxDifficulty is not null) { int acts = save.Character.Preview.GameVersion == GameVersion.Classic ? 4 : 5; int before=Math.Min(2,(int)(((uint)save.Character.Flags>>8)&31)/acts),target=DifficultyIndex(change.MaxDifficulty);if(before!=target){int progression=target*acts;save.Character.Flags=(CharacterFlags)(((uint)save.Character.Flags&~0x1F00u)|((uint)progression<<8));diff.Add(new("world",L.T("Dificultad máxima"),DifficultyNames[before],change.MaxDifficulty));} }
        if (change.ActiveDifficulty is not null || change.Act.HasValue)
        {
            int index = change.ActiveDifficulty is null ? Array.FindIndex(save.Character.TownDifficulty, b => (b & 0x80) != 0) : DifficultyIndex(change.ActiveDifficulty); if (index < 0) index = 0;
            int act = change.Act ?? ((save.Character.TownDifficulty[index] & 7) + 1), maxAct = save.Character.Preview.GameVersion == GameVersion.Classic ? 4 : 5;
            if (act < 1 || act > maxAct) throw new ApiProblem("act_invalid", L.T("El acto está fuera de los límites del modo seleccionado."));
            int beforeIndex=Array.FindIndex(save.Character.TownDifficulty,b=>(b&0x80)!=0),beforeAct=beforeIndex<0?1:(save.Character.TownDifficulty[beforeIndex]&7)+1;
            for (int i = 0; i < 3; i++) save.Character.TownDifficulty[i] &= 0x7F;
            save.Character.TownDifficulty[index] = (byte)((save.Character.TownDifficulty[index]&0x78)|0x80|(act-1));
            if(beforeIndex!=index||beforeAct!=act)diff.Add(new("world", L.T("Lugar de inicio"),beforeIndex<0?null:DifficultyNames[beforeIndex]+L.T(" · acto ")+beforeAct,DifficultyNames[index] + L.T(" · acto ") + act));
        }
    }
}
