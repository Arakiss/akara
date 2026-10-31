using D2SSharp.Enums;
using D2SSharp.Model;
using static ForjaHoradrica.GameData;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private static (long Minimum, long Maximum) ExperienceBounds(GameData p, string className, int level)
    {
        int maximumLevel = I(p.Read("experience.txt").First(r => V(r,"Level")=="MaxLvl"),className,99);
        long At(int n) => long.Parse(V(p.Read("experience.txt").First(r => V(r,"Level")==Math.Max(0,n-1).ToString()),className));
        return (At(level),level < maximumLevel ? At(level+1)-1 : At(level));
    }
    private static int LevelForExperience(GameData p, string className, long experience)
    {
        int maximumLevel=I(p.Read("experience.txt").First(r=>V(r,"Level")=="MaxLvl"),className,99),level=1;
        if(experience<0||experience>ExperienceBounds(p,className,maximumLevel).Maximum) throw new ApiProblem("experience_range","La experiencia supera el límite nativo de esta clase.");
        for(int n=2;n<=maximumLevel;n++){if(experience<ExperienceBounds(p,className,n).Minimum) break;level=n;}
        return level;
    }
    private static void ApplyStatus(D2Save save, CharacterStatusChange? change, List<ChangeEntry> diff)
    {
        if(change is null)return;
        void Set(CharacterFlags flag,bool? value,string label)
        {
            if(!value.HasValue)return;bool before=save.Character.Flags.HasFlag(flag);
            save.Character.Flags=value.Value?save.Character.Flags|flag:save.Character.Flags&~flag;
            if(before!=value.Value)diff.Add(new("character",label,before,value.Value));
        }
        bool wasDead=save.Character.Flags.HasFlag(CharacterFlags.Dead);
        Set(CharacterFlags.Hardcore,change.Hardcore,"Hardcore");Set(CharacterFlags.Dead,change.Dead,"Marca de muerte");Set(CharacterFlags.Ladder,change.Ladder,"Escalera");
        if(change.Dead==false&&wasDead)save.Stats.SetStat((StatId)6,save.Stats.GetStat((StatId)7));
        if(change.Dead==true)save.Stats.SetStat((StatId)6,0);
    }
    private static WorldChange GoldenBirdRewards(WorldChange? custom=null) => new()
    {
        Quests=new[]{"normal","nightmare","hell"}.Select(d=>new QuestChange(d,"act3.the-golden-bird","rewarded")).Concat(custom?.Quests??[]).ToList(),
        Waypoints=custom?.Waypoints??[],ActiveDifficulty=custom?.ActiveDifficulty,Act=custom?.Act,MaxDifficulty=custom?.MaxDifficulty
    };
}
