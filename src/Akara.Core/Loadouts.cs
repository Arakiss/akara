using System.Text.Json;
using D2SSharp.Enums;
using D2SSharp.Model;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    public LoadoutDocument ExportLoadout(string sessionId,string name,string? planId=null,bool includeCharacter=true)
    {
        var s=Session(sessionId);lock(s.Sync)
        {
            var plan=planId is null?null:Plan(s,planId);var save=Read(plan?.Bytes??s.Original,s.Profile,s.Version);
            var ids=new ItemIdentities(save,plan?.CharacterItemIds,plan?.MercItemIds);
            var doc=new LoadoutDocument{Name=ValidLoadoutName(name),ClassName=save.Character.Class.ToString(),SaveVersion=s.Version,ProfileFingerprint=s.Profile.Fingerprint(),ActiveWeaponSet=ActiveSet(save)};
            void Add(Item item,string owner)
            {
                string id=ids.Id(item);var data=SerializeItem(item,s.Profile,s.Version);
                var blueprint=new ItemBlueprintDocument{SaveVersion=s.Version,ProfileFingerprint=doc.ProfileFingerprint,Code=item.ItemCodeString,Name=ItemName(item,s.Profile),Quality=item.Quality.ToString().ToLowerInvariant(),Data=data,DataHash=Hash(data),Description=JsonSerializer.SerializeToElement(ItemDto(item,s.Profile,id,owner,save),ContractJson)};
                var destination=new ItemDestination{Owner=owner,Page=item.Position.Mode==ItemMode.Equipped?"equipped":"inventory",Slot=item.Position.Mode==ItemMode.Equipped?Slot(item):null,WeaponSet=ItemWeaponSet(item,save,owner)};
                doc.Entries.Add(new(destination,blueprint));
            }
            foreach(var item in save.Items.Where(i=>i.Position.Mode==ItemMode.Equipped||i.Position.Mode==ItemMode.Stored&&i.Position.StorePage==StorePage.Inventory&&i.ItemCodeString is "cm1" or "cm2" or "cm3"))Add(item,"character");
            foreach(var item in save.MercItems?.Items??[])Add(item,"mercenary");
            if(save.Character.MercData.HasMerc){var merc=JsonSerializer.SerializeToElement(MercenaryDto(save,s.Profile,ids),ContractJson);doc.Mercenary=new(save.Character.MercData.HirelingId,merc.GetProperty("level").GetInt32(),true);}
            if(includeCharacter)
            {
                doc.Level=save.Character.Level;doc.Attributes=new(){["strength"]=save.Stats.GetStat((StatId)0),["dexterity"]=save.Stats.GetStat((StatId)2),["vitality"]=save.Stats.GetStat((StatId)3),["energy"]=save.Stats.GetStat((StatId)1)};
                doc.Skills=save.Skills.SkillLevels.Select((value,index)=>new SkillChange(save.Skills.GetSkillId(index),value)).ToList();
                doc.Hotkeys=save.Character.SkillHotkeys.Select((key,index)=>new{key,index}).ToDictionary(k=>k.index,k=>(int)k.key.SkillId);
                doc.HotkeySides=save.Character.SkillHotkeys.Select((key,index)=>new{key,index}).ToDictionary(k=>k.index,k=>k.key.Side.ToString().ToLowerInvariant());
                doc.LeftSkill=save.Character.LeftSkill.SkillId;doc.RightSkill=save.Character.RightSkill.SkillId;doc.SwitchLeftSkill=save.Character.SwitchLeftSkill.SkillId;doc.SwitchRightSkill=save.Character.SwitchRightSkill.SkillId;
                int active=Array.FindIndex(save.Character.TownDifficulty,b=>(b&0x80)!=0);if(active<0)active=0;int acts=save.Character.Preview.GameVersion==GameVersion.Classic?4:5;
                doc.World=new(){ActiveDifficulty=DifficultyNames[active],Act=(save.Character.TownDifficulty[active]&7)+1,MaxDifficulty=DifficultyNames[Math.Min(2,(int)(((uint)save.Character.Flags>>8)&31)/acts)]};string[] rewards=["DenOfEvil","RadamentsLair","TheFallenAngel","LamEsensTome","TheGoldenBird","PrisonOfIce"];
                for(int i=0;i<3;i++)foreach(var quest in QuestEntries(Difficulties(save)[i]).Where(q=>rewards.Contains(q.Property.Name)&&Reward(q.Flags)))
                    if(quest.Property.Name!="PrisonOfIce"||(quest.Flags&QuestFlags.Custom3)!=0)doc.World.Quests.Add(new(DifficultyNames[i],quest.Id,"rewarded"));
                var waypoints=new[]{save.Waypoints.Normal,save.Waypoints.Nightmare,save.Waypoints.Hell};for(int i=0;i<3;i++)for(int wp=0;wp<39;wp++)if(waypoints[i].IsWaypointActive(wp))doc.World.Waypoints.Add(new(DifficultyNames[i],wp,true));
            }
            return doc;
        }
    }
    private static string ValidLoadoutName(string name)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Length>100||name.Any(char.IsControl))throw new ApiProblem("loadout_name","Escribe un nombre de biblioteca de hasta100 caracteres.");return name.Trim();
    }
    private static PreviewRequest ExpandLoadout(PreviewRequest request,D2Save save,GameData profile)
    {
        var doc=request.Loadout!;ValidateLoadout(doc,profile,save.Version);
        if(request.Preset is not null)throw new ApiProblem("loadout_preset","Aplica una plantilla o una biblioteca en cada borrador, no ambas a la vez.");
        if(!doc.ClassName.Equals(save.Character.Class.ToString(),StringComparison.OrdinalIgnoreCase))throw new ApiProblem("loadout_class","La biblioteca corresponde a otra clase de personaje.");
        var operations=new List<ItemOperation>();
        foreach(var item in save.Items.Select((item,index)=>new{item,index}).Where(x=>x.item.Position.Mode==ItemMode.Equipped||x.item.Position.Mode==ItemMode.Stored&&x.item.Position.StorePage==StorePage.Inventory&&x.item.ItemCodeString is "cm1" or "cm2" or "cm3"))operations.Add(new(){Operation=request.LoadoutKeepPrevious?"move":"delete",ItemId="i"+item.index,Destination=new(){Page="stash"}});
        if(save.MercItems is not null)for(int i=0;i<save.MercItems.Items.Count;i++)operations.Add(new(){Operation=request.LoadoutKeepPrevious?"move":"delete",ItemId="m"+i,Destination=new(){Page="stash"}});
        int n=0;foreach(var entry in doc.Entries)operations.Add(new(){Operation="create",Blueprint=entry.Blueprint,Destination=entry.Destination,ClientId="loadout-"+n++});
        request.ItemOperations=operations.Concat(request.ItemOperations??[]).ToList();request.ActiveWeaponSet??=doc.ActiveWeaponSet;request.Mercenary??=doc.Mercenary;
        if(request.LoadoutIncludeCharacter)
        {
            request.Level??=doc.Level;var attributes=doc.Attributes is null?new Dictionary<string,long>():new(doc.Attributes,StringComparer.OrdinalIgnoreCase);if(request.Attributes is not null)foreach(var kv in request.Attributes)attributes[kv.Key]=kv.Value;request.Attributes=attributes;
            request.Skills??=doc.Skills;request.Hotkeys??=doc.Hotkeys;request.HotkeySides??=doc.HotkeySides;request.LeftSkill??=doc.LeftSkill;request.RightSkill??=doc.RightSkill;request.SwitchLeftSkill??=doc.SwitchLeftSkill;request.SwitchRightSkill??=doc.SwitchRightSkill;
            if(doc.World is not null)request.World=new(){Quests=doc.World.Quests.Concat(request.World?.Quests??[]).GroupBy(q=>(q.Difficulty,q.QuestId)).Select(g=>g.Last()).ToList(),Waypoints=doc.World.Waypoints.Concat(request.World?.Waypoints??[]).GroupBy(w=>(w.Difficulty,w.Id)).Select(g=>g.Last()).ToList(),ActiveDifficulty=request.World?.ActiveDifficulty??doc.World.ActiveDifficulty,Act=request.World?.Act??doc.World.Act,MaxDifficulty=request.World?.MaxDifficulty??doc.World.MaxDifficulty};
        }
        return request;
    }
    private static void ValidateLoadout(LoadoutDocument doc,GameData profile,uint version)
    {
        if(doc is null||doc.SchemaVersion!=1||doc.SaveVersion!=version||doc.ProfileFingerprint!=profile.Fingerprint()||doc.Entries is null||doc.Entries.Count>400||doc.Entries.Any(e=>e is null||e.Blueprint is null||e.Destination is null))throw new ApiProblem("loadout_format","La biblioteca no coincide con la versión o los datos nativos seleccionados.");
        ValidLoadoutName(doc.Name);
        foreach(var entry in doc.Entries)ImportBlueprintItem(entry.Blueprint,profile,version);
    }
    public object Loadouts()
    {
        string dir=Path.Combine(dataRoot,"loadouts");if(!Directory.Exists(dir))return new{entries=Array.Empty<object>()};var entries=new List<object>();
        foreach(var file in Directory.EnumerateFiles(dir,"*.json").Take(1000))try{var doc=JsonSerializer.Deserialize<LoadoutDocument>(File.ReadAllText(file),ContractJson);if(doc is not null)entries.Add(new{id=Path.GetFileNameWithoutExtension(file),doc.Name,doc.ClassName,doc.Level,itemCount=doc.Entries.Count,hasCharacterBuild=doc.Skills is not null});}catch(JsonException){}
        return new{entries};
    }
    private string LoadoutPath(string id){if(!Guid.TryParseExact(id,"N",out _))throw new ApiProblem("loadout_id","El identificador de biblioteca no es válido.");return Path.Combine(dataRoot,"loadouts",id+".json");}
    public LoadoutDocument GetLoadout(string id){string path=LoadoutPath(id);if(!File.Exists(path))throw new ApiProblem("loadout_missing","La biblioteca ya no existe.",404);if(new FileInfo(path).Length>16*1024*1024)throw new ApiProblem("loadout_size","La biblioteca es demasiado grande.");return JsonSerializer.Deserialize<LoadoutDocument>(File.ReadAllText(path),ContractJson)??throw new ApiProblem("loadout_format","La biblioteca está vacía.");}
    public object SaveLoadout(SaveLoadoutRequest request)=>StoreLoadout(ExportLoadout(request.SessionId,request.Name,request.PlanId,request.IncludeCharacter));
    public object ImportLoadout(ImportLoadoutRequest request){var s=Session(request.SessionId);ValidateLoadout(request.Document,s.Profile,s.Version);return StoreLoadout(request.Document);}
    private object StoreLoadout(LoadoutDocument document){string id=Guid.NewGuid().ToString("N"),path=LoadoutPath(id);WriteNewAtomic(path,JsonSerializer.SerializeToUtf8Bytes(document,ContractJson));return new{id,path,document};}
    public object DeleteLoadout(string id){string path=LoadoutPath(id);if(!File.Exists(path))throw new ApiProblem("loadout_missing","La biblioteca ya no existe.",404);File.Delete(path);return new{deleted=true};}
}
