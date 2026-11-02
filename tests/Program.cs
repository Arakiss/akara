using System.Text.Json;
using ForjaHoradrica;
using D2SSharp.Model;
using D2SSharp.Data;
using D2SSharp.Enums;

string? fixtureDirectory = Environment.GetEnvironmentVariable("AKARA_TEST_FIXTURES");
string? sorceressFixture = args.Length > 0 ? args[0] : fixtureDirectory is null ? null : Path.Combine(fixtureDirectory, "sorceress.d2s");
string? warlockFixture = args.Length > 1 ? args[1] : fixtureDirectory is null ? null : Path.Combine(fixtureDirectory, "warlock.d2s");
string? excelPath = args.Length > 2 ? args[2] : Environment.GetEnvironmentVariable("AKARA_TEST_EXCEL");
if (args.Length is not (0 or 3) || !File.Exists(sorceressFixture) || !File.Exists(warlockFixture) || excelPath is null || !Directory.Exists(excelPath))
{
    Console.Error.WriteLine("Integration fixtures are required; no tests were run. Set AKARA_TEST_EXCEL to your private Reimagined3.1 excel directory and AKARA_TEST_FIXTURES to a private directory containing sorceress.d2s and warlock.d2s. Alternatively pass three arguments: sorceressFixture warlockFixture excelPath. See tests/README.md.");
    Environment.ExitCode = 2;
    return;
}
var nativeData = new TxtFileExternalData(excelPath, 105);
var sorceressBytes = File.ReadAllBytes(sorceressFixture!); var warlockBytes = File.ReadAllBytes(warlockFixture!);
if (sorceressBytes.Length < 16 || warlockBytes.Length < 16 || BitConverter.ToUInt32(sorceressBytes, 4) != 105 || BitConverter.ToUInt32(warlockBytes, 4) != 105)
{
    Console.Error.WriteLine("These integration tests require private version105 Sorceress and Warlock fixtures. No tests were run; see tests/README.md.");
    Environment.ExitCode = 2;
    return;
}
var sorceressTemplate = D2Save.Read(sorceressBytes, nativeData); var warlockTemplate = D2Save.Read(warlockBytes, nativeData);
if (sorceressTemplate.Character.Class != CharacterClass.Sorceress || sorceressTemplate.Character.Level != 1 || sorceressTemplate.Skills.SkillLevels.Any(p => p != 0) || warlockTemplate.Character.Class != CharacterClass.Warlock || warlockTemplate.Character.Level >= 99)
{
    Console.Error.WriteLine("Expected a fresh level1 Sorceress with no spent skill points and a Warlock below level99. No tests were run; see tests/README.md.");
    Environment.ExitCode = 2;
    return;
}
string sorceressName = sorceressTemplate.Character.Preview.Name, warlockName = warlockTemplate.Character.Preview.Name;
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
JsonElement J(object value) => JsonSerializer.SerializeToElement(value, json);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
var root = Path.Combine(Path.GetTempPath(), "akara-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Directory.CreateDirectory(Path.Combine(root, "sorceress")); Directory.CreateDirectory(Path.Combine(root, "warlock"));
var source = Path.Combine(root, "sorceress", sorceressName + ".d2s"); var warlock = Path.Combine(root, "warlock", warlockName + ".d2s");
File.Copy(sorceressFixture!, source); File.Copy(warlockFixture!, warlock);
var original = File.ReadAllBytes(source); string originalHash = EditorService.Hash(original);
bool gameOpen = false;
var service = new EditorService(Path.Combine(root, "appdata"), () => gameOpen);
var profile = J(service.AddProfile(new(excelPath, null, "Test profile"))).GetProperty("id").GetString()!;
var open = J(service.Open(new(source, profile))); string session = open.GetProperty("sessionId").GetString()!;
Check(open.GetProperty("validation").GetProperty("roundtrip").GetBoolean(), "Sorceress opens with identical original roundtrip");
Check(open.GetProperty("skills").GetArrayLength() >= 30, "Native Sorceress skill catalog present");
var warlockOpen = J(service.Open(new(warlock, profile)));
Check(warlockOpen.GetProperty("character").GetProperty("className").GetString() == "Warlock", "Warlock identified natively");
Check(warlockOpen.GetProperty("validation").GetProperty("roundtrip").GetBoolean(), "Warlock opens with identical roundtrip");
var warlockLevel = warlockOpen.GetProperty("character").GetProperty("level").GetInt32();
var warlockPreview = J(service.Preview(new() { SessionId = warlockOpen.GetProperty("sessionId").GetString()!, Level = warlockLevel + 1 }));
Check(warlockPreview.GetProperty("canSave").GetBoolean(), "Warlock level edit recalculates native growth and budget");
var noOp = J(service.Preview(new() { SessionId = session }));
Check(noOp.GetProperty("canSave").GetBoolean(), "No-op candidate validates");
var noOpExport = J(service.Export(new(session, noOp.GetProperty("planId").GetString()!, null)));
Check(File.ReadAllBytes(noOpExport.GetProperty("outputPath").GetString()!).AsSpan().SequenceEqual(original), "No-op export is byte identical");
var progressed = D2Save.Read(original, nativeData); progressed.Stats.SetStat((StatId)13, 17);
var progressDir = Path.Combine(root, "progressed"); Directory.CreateDirectory(progressDir); var progressPath = Path.Combine(progressDir, sorceressName + ".d2s"); var progressBytes = progressed.ToBytes(nativeData, 105); File.WriteAllBytes(progressPath, progressBytes);
var progressOpen = J(service.Open(new(progressPath, profile))); var attrs = progressOpen.GetProperty("character").GetProperty("attributes");
var fullForm = J(service.Preview(new() { SessionId = progressOpen.GetProperty("sessionId").GetString()!, Level = progressOpen.GetProperty("character").GetProperty("level").GetInt32(), Name = sorceressName, Attributes = new(){["strength"]=attrs.GetProperty("strength").GetInt64(),["dexterity"]=attrs.GetProperty("dexterity").GetInt64(),["vitality"]=attrs.GetProperty("vitality").GetInt64(),["energy"]=attrs.GetProperty("energy").GetInt64(),["gold"]=attrs.GetProperty("gold").GetInt64(),["stashGold"]=attrs.GetProperty("stashGold").GetInt64()}, Skills = progressOpen.GetProperty("skills").EnumerateArray().Select(x => new SkillChange(x.GetProperty("id").GetInt32(),x.GetProperty("points").GetInt32())).ToList() }));
Check(fullForm.GetProperty("canSave").GetBoolean() && fullForm.GetProperty("summary").GetProperty("character").GetProperty("experience").GetInt64()==17, "Full form at unchanged level preserves intermediate XP");
var fullExport = J(service.Export(new(progressOpen.GetProperty("sessionId").GetString()!,fullForm.GetProperty("planId").GetString()!,null)));
Check(File.ReadAllBytes(fullExport.GetProperty("outputPath").GetString()!).AsSpan().SequenceEqual(progressBytes), "Full unchanged form exports byte identical save");
var invalid = J(service.Preview(new() { SessionId = session, Attributes = new() { ["strength"] = 1000 } }));
Check(!invalid.GetProperty("canSave").GetBoolean(), "Overspent attribute budget rejected");
var invalidSkill = J(service.Preview(new() { SessionId = session, Skills = [new(64, 26)] }));
Check(!invalidSkill.GetProperty("canSave").GetBoolean(), "Native max skill cap rejected");
var invalidPrerequisite = J(service.Preview(new() { SessionId = session, Level = 85, Skills = [new(64, 1)] }));
Check(!invalidPrerequisite.GetProperty("canSave").GetBoolean(), "Missing prerequisite rejected");
var illegalSlot = J(service.Preview(new() { SessionId = session, Gear = [new() { Type = "unique", Name = "The Oculus", Slot = "helm" }] }));
Check(!illegalSlot.GetProperty("canSave").GetBoolean(), "Incompatible equipment slot rejected");
var preset = J(service.Preview(new() { SessionId = session, Preset = "orbe-tal-oculus" }));
Check(preset.GetProperty("canSave").GetBoolean(), "Native Orb/Tal preset validates: " + preset.GetProperty("validation").ToString());
var summary = preset.GetProperty("summary");
Check(summary.GetProperty("character").GetProperty("vitals").GetProperty("maxLife").GetDouble() == 986.5, "Q8 character life includes the three native Golden Bird rewards exactly once");
Check(summary.GetProperty("questRewards").GetProperty("anyaScrollsConsumed").GetInt32() == 3, "Anya marks consumed scroll in all three difficulties");
Check(summary.GetProperty("skills").EnumerateArray().Sum(x => x.GetProperty("points").GetInt32()) == 96, "Native level85 skill budget is96");
Check(summary.GetProperty("rightSkill").GetInt32() == 64 && summary.GetProperty("hotkeys")[1].GetProperty("skillId").GetInt32() == 54, "Orb preset assigns right click and native teleport hotkey");
var shield = summary.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("slot").GetString() == "shield");
Check(shield.GetProperty("runewordId").GetInt32() == 20635 && shield.GetProperty("sockets").GetArrayLength() == 4, "Spirit native ID and socket children survive roundtrip");
var tal = summary.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("slot").GetString() == "armor");
Check(tal.GetProperty("setBonusMask").GetInt32() == 5 && tal.GetProperty("sockets")[0].GetProperty("code").GetString() == "r22", "Tal partial set mask and Um child survive roundtrip");
string plan = preset.GetProperty("planId").GetString()!;
gameOpen = true;
try { service.Commit(new(session, plan, originalHash)); throw new Exception("Game guard failed"); } catch (ApiProblem ex) { Check(ex.Code == "game_running", "Game-open guard blocks replacement"); }
gameOpen = false;
try { service.Commit(new(session, plan, "BADHASH")); throw new Exception("Hash guard failed"); } catch (ApiProblem ex) { Check(ex.Code == "source_changed", "Source conflict blocks replacement"); }
var commit = J(service.Commit(new(session, plan, originalHash)));
Check(File.ReadAllBytes(commit.GetProperty("backupPath").GetString()!).AsSpan().SequenceEqual(original), "Commit backup byte identical to original");
Check(EditorService.Hash(File.ReadAllBytes(source)) == commit.GetProperty("hash").GetString(), "Atomic replacement verifies written hash");
var reopen = J(service.Open(new(source, profile)));
var repeated = J(service.Preview(new() { SessionId = reopen.GetProperty("sessionId").GetString()!, Preset = "orbe-tal-oculus" }));
Check(repeated.GetProperty("canSave").GetBoolean(), "Repeating preset safely moves previous charms and equipment to stash");
Check(repeated.GetProperty("summary").GetProperty("items").EnumerateArray().Count(x => x.GetProperty("page").GetString() == "inventory" && x.GetProperty("name").GetString() == "Hellfire Torch") == 1, "Repeated preset activates exactly one Torch");
var mercenary = J(service.Preview(new() { SessionId = reopen.GetProperty("sessionId").GetString()!, Mercenary = new(11,85,true), Gear = [new(){Type="runeword",Name="Infinity",BaseCode="7s8",MercSlot="weapon",Ethereal=true},new(){Type="runeword",Name="Fortitude",BaseCode="uth",MercSlot="armor",Ethereal=true},new(){Type="unique",Name="Andariel's Visage",MercSlot="helm",Sockets=["r08"]}] }));
Check(mercenary.GetProperty("canSave").GetBoolean(), "Native ActII mercenary and Infinity/Fortitude/Andariel validate: " + mercenary.GetProperty("validation").ToString());
var mercSummary = mercenary.GetProperty("summary").GetProperty("mercenary");
Check(mercSummary.GetProperty("exists").GetBoolean() && mercSummary.GetProperty("experience").GetInt64()==74562000 && mercSummary.GetProperty("items").GetArrayLength()==3, "Mercenary XP, existence and equipment survive binary roundtrip");
foreach (var item in mercSummary.GetProperty("items").EnumerateArray()) foreach (var listName in new[]{"stats","runewordStats"}) Check(item.GetProperty(listName).EnumerateArray().GroupBy(x => (x.GetProperty("id").GetInt32(),x.GetProperty("layer").GetInt32())).All(g=>g.Count()==1), "Mercenary "+item.GetProperty("name").GetString()+" "+listName+" has no duplicated paired ED");
var lowered = J(service.Preview(new(){SessionId=reopen.GetProperty("sessionId").GetString()!,Level=30,Attributes=new(){["strength"]=10,["dexterity"]=25,["vitality"]=10,["energy"]=35}}));
Check(!lowered.GetProperty("canSave").GetBoolean() && lowered.GetProperty("validation").GetProperty("errors").ToString().Contains("Character level", StringComparison.Ordinal), "Lowering level validates existing hard skills even when skills are omitted");
var backups = J(service.GetBackups(session)).GetProperty("entries");
var restored = J(service.Restore(new(session, backups[0].GetProperty("id").GetString()!, commit.GetProperty("hash").GetString()!)));
Check(File.ReadAllBytes(source).AsSpan().SequenceEqual(original), "Restore returns original bytes and preserves displaced version");
Check(EditorService.Hash(File.ReadAllBytes(sorceressFixture!)) == originalHash, "Real Sorceress fixture remained untouched");
Check(EditorService.Hash(File.ReadAllBytes(warlockFixture!)) == EditorService.Hash(File.ReadAllBytes(warlock)), "Real Warlock fixture remained untouched");
EvolutionChecks.Run(service, profile, Check, root, excelPath);
Console.WriteLine("AKARA core integration tests passed. Artifacts: " + root);
