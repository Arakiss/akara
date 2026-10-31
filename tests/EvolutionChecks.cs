using System.Text.Json;
using ForjaHoradrica;
using D2SSharp.Data;
using D2SSharp.IO;
using D2SSharp.Model;

public static class EvolutionChecks
{
    public static void Run(EditorService service, string profile, Action<bool, string> check, string artifactRoot, string excelPath)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        JsonElement J(object value) => JsonSerializer.SerializeToElement(value, options);
        string Id(JsonElement value) => value.GetProperty("sessionId").GetString()!;
        foreach (string className in new[] { "Amazon", "Sorceress", "Necromancer", "Paladin", "Barbarian", "Druid", "Assassin", "Warlock" })
        {
            var created = J(service.Create(new() { ProfileId = profile, ClassName = className, Name = "QA" + className }));
            check(created.GetProperty("isNew").GetBoolean() && created.GetProperty("sourcePath").ValueKind == JsonValueKind.Null && created.GetProperty("character").GetProperty("className").GetString() == className, "Native new-character draft " + className);
            var preview = J(service.Preview(new() { SessionId = Id(created) })); check(preview.GetProperty("canSave").GetBoolean(), "Native empty preview " + className);
        }
        var source = J(service.Create(new() { ProfileId = profile, ClassName = "Sorceress", Name = "QASorceress", Level = 85 })); string id = Id(source);
        var itemPlan = J(service.Preview(new() { SessionId = id, ItemOperations = [new() { Operation = "create", ClientId = "made", Gear = new() { Type = "unique", Name = "The Oculus" }, Destination = new() { Page = "stash" } }, new() { Operation = "copy", ItemId = "made", ClientId = "copied", Destination = new() { Page = "inventory" } }, new() { Operation = "delete", ItemId = "i1" }, new() { Operation = "move", ItemId = "i0", Destination = new() { Page = "stash" } }] }));
        check(itemPlan.GetProperty("canSave").GetBoolean(), "Ordered create/copy/delete/move operations validate");
        var items = itemPlan.GetProperty("summary").GetProperty("items").EnumerateArray().ToArray();
        check(items.Any(i => i.GetProperty("id").GetString() == "i0") && items.All(i => i.GetProperty("id").GetString() != "i1") && items.Any(i => i.GetProperty("id").GetString() == "copied"), "Preview IDs survive removal and owner/container changes");
        string planId = itemPlan.GetProperty("planId").GetString()!;
        var made = service.ExportBlueprint(id, "made", planId); var copied = service.ExportBlueprint(id, "copied", planId);
        var data = new TxtFileExternalData(excelPath,105); var a = new BitReader(made.Data); var b = new BitReader(copied.Data); var originalItem = Item.Read(ref a,data,105); var copyItem = Item.Read(ref b,data,105);
        check(made.Code == "oba" && copied.Code == "oba" && originalItem.ItemSeed != copyItem.ItemSeed, "Blueprint resolves preview client IDs and copies have fresh seeds");
        var import = J(service.Preview(new() { SessionId = id, ItemOperations = [new() { Operation = "create", ClientId = "imported", Blueprint = copied, Destination = new() { Page = "stash" } }] }));
        check(import.GetProperty("canSave").GetBoolean(), "Exported native item blueprint imports through ordered create");
        var designer = J(service.Preview(new() { SessionId = id, ItemOperations = [new() { Operation = "create", ClientId = "crafted", Design = new() { BaseCode = "7cr", Quality = "crafted", PersonalizedName = "AKARA", Stats = [new(17,0,250),new(18,0,250)] }, Destination = new() { Page = "stash" } }] }));
        check(designer.GetProperty("canSave").GetBoolean(), "Crafted designer preserves paired250ED and artisan signature");
        var badPair = J(service.Preview(new() { SessionId = id, ItemOperations = [new() { Operation = "create", Design = new() { BaseCode = "7cr", Stats = [new(17,0,250)] } }] }));
        check(!badPair.GetProperty("canSave").GetBoolean(), "Designer rejects incomplete ED pair without silently changing stats");
        var world = J(service.Preview(new() { SessionId = id, World = new() { Quests = [new("normal","act1.den-of-evil","rewarded"),new("normal","act3.the-golden-bird","rewarded"),new("hell","act5.prison-of-ice","rewarded")], Waypoints = [new("hell",38,true)], MaxDifficulty = "hell", ActiveDifficulty = "hell", Act = 1 } }));
        check(world.GetProperty("canSave").GetBoolean(), "Granular quests, waypoints and active difficulty validate together");
        var state = J(service.World(id,world.GetProperty("planId").GetString()));
        check(state.GetProperty("activeDifficulty").GetString()=="hell" && state.GetProperty("difficulties")[2].GetProperty("waypoints")[38].GetProperty("active").GetBoolean(), "World preview reads the actual candidate flags");
        var clone = J(service.Clone(new(id,"QAClone")));
        check(clone.GetProperty("isNew").GetBoolean() && clone.GetProperty("character").GetProperty("name").GetString()=="QAClone" && source.GetProperty("character").GetProperty("name").GetString()=="QASorceress", "Clone creates an independent renamed draft");
        var current = J(service.Preview(new(){SessionId=id}));
        var detached = service.DetachPlan(id,current.GetProperty("planId").GetString()!); var detachedJson=JsonSerializer.Serialize(detached,options); var restored=JsonSerializer.Deserialize<DetachedPlanDocument>(detachedJson,options)!;
        check(J(service.ValidateDetachedPlan(restored)).GetProperty("valid").GetBoolean(), "New-character detached plans validate after JSON persistence");
        string? hybridSession=null,hybridPlan=null;
        foreach (var preset in J(service.Presets(profile)).GetProperty("entries").EnumerateArray())
        {
            string className=preset.GetProperty("className").GetString()!, presetId=preset.GetProperty("id").GetString()!;
            var created=J(service.Create(new(){ProfileId=profile,ClassName=className,Name="B"+className}));
            var result=J(service.Preview(new(){SessionId=Id(created),Preset=presetId}));
            check(result.GetProperty("canSave").GetBoolean(), "Full native class preset "+presetId+": "+result.GetProperty("validation").ToString());
            if(presetId=="squad-paladin")check(result.GetProperty("summary").GetProperty("hotkeys")[0].GetProperty("side").GetString()=="left"&&result.GetProperty("summary").GetProperty("hotkeys")[7].GetProperty("side").GetString()=="left"&&result.GetProperty("summary").GetProperty("hotkeys")[1].GetProperty("side").GetString()=="right","Paladin Hammer/Smite bind left while Concentration stays right");
            if(presetId=="squad-sorceress"){hybridSession=Id(created);hybridPlan=result.GetProperty("planId").GetString();}
        }
        var loadout=service.ExportLoadout(hybridSession!,"QA hybrid",hybridPlan);
        check(loadout.Entries.Count(e=>e.Destination.Owner=="mercenary")==9&&loadout.Entries.Count(e=>e.Destination.Page=="equipped"&&e.Destination.Owner=="character")==12,"Hybrid loadout captures both weapon sets and all nine mercenary items");
        var xpPlan=J(service.Preview(new(){SessionId=id,Experience=source.GetProperty("character").GetProperty("experience").GetInt64()+12345}));
        check(xpPlan.GetProperty("canSave").GetBoolean(),"Direct XP edit respects native level bounds");
        var xpClone=J(service.Clone(new(id,"QAXP",xpPlan.GetProperty("planId").GetString())));
        var applied=J(service.Preview(new(){SessionId=Id(xpClone),Loadout=loadout,LoadoutKeepPrevious=false}));
        check(applied.GetProperty("canSave").GetBoolean(),"Full hybrid library applies to a different draft");
        check(applied.GetProperty("summary").GetProperty("character").GetProperty("experience").GetInt64()==source.GetProperty("character").GetProperty("experience").GetInt64()+12345,"Library preserves target XP when the level is unchanged");
        var stored=J(service.SaveLoadout(new(hybridSession!,"QA hybrid",hybridPlan)));string libraryId=stored.GetProperty("id").GetString()!;
        check(service.GetLoadout(libraryId).Entries.Count==loadout.Entries.Count,"Local library save and read preserve the complete build");
        service.DeleteLoadout(libraryId);check(J(service.Loadouts()).GetProperty("entries").GetArrayLength()==0,"Deleting a library entry only removes the owned local entry");
        var baked=J(service.Clone(new(Id(xpClone),"QABaked",applied.GetProperty("planId").GetString())));
        var noWorldChange=J(service.Preview(new(){SessionId=Id(baked),World=loadout.World,Skills=loadout.Skills,Hotkeys=loadout.Hotkeys,HotkeySides=loadout.HotkeySides,LeftSkill=loadout.LeftSkill,RightSkill=loadout.RightSkill,SwitchLeftSkill=loadout.SwitchLeftSkill,SwitchRightSkill=loadout.SwitchRightSkill}));
        check(noWorldChange.GetProperty("canSave").GetBoolean()&&noWorldChange.GetProperty("diff").GetArrayLength()==0,"World, skills and bindings repeated unchanged produce no fake differences");
        var clearBindings=J(service.Preview(new(){SessionId=id,Hotkeys=Enumerable.Range(0,16).ToDictionary(n=>n,n=>32767),SwitchLeftSkill=65535,SwitchRightSkill=32767}));
        check(clearBindings.GetProperty("canSave").GetBoolean(),"Both native32767 and legacy65535 unbound sentinels clear bindings");
        var status=J(service.Preview(new(){SessionId=id,Status=new(true,false,true)}));check(status.GetProperty("canSave").GetBoolean()&&status.GetProperty("summary").GetProperty("character").GetProperty("status").GetProperty("hardcore").GetBoolean(),"Explicit local Hardcore/Ladder status edits validate");
        var ethDesign=new ItemDesignRequest{BaseCode="utp",Quality="normal",Ethereal=true,Defense=1200,MaxDurability=30,Durability=20};
        var eth=J(service.Preview(new(){SessionId=id,ItemOperations=[new(){Operation="create",ClientId="eth",Design=ethDesign},new(){Operation="edit",ItemId="eth",Design=ethDesign}]}));
        check(eth.GetProperty("canSave").GetBoolean()&&eth.GetProperty("summary").GetProperty("items").EnumerateArray().Single(i=>i.GetProperty("id").GetString()=="eth").GetProperty("defense").GetInt32()==1200,"Editing ethereal final physical values does not multiply them twice");
        var gap=J(service.Preview(new(){SessionId=id,ItemOperations=[new(){Operation="create",Design=new(){BaseCode="uit",SocketCount=2,SocketChildren=[null,new(){BaseCode="r01"}]}}]}));
        check(!gap.GetProperty("canSave").GetBoolean(),"Designer rejects unencodable gaps between occupied sockets");
        var originalRequest=new PreviewRequest{SessionId=id,Preset="orbe-tal-oculus"};var first=J(service.Preview(originalRequest));var second=J(service.Preview(originalRequest));
        check(first.GetProperty("canSave").GetBoolean()&&second.GetProperty("canSave").GetBoolean()&&originalRequest.Gear is null&&originalRequest.World is null,"Repeated Core preview does not mutate the caller's typed draft");
    }
}
