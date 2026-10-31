using D2SSharp.Model;

namespace ForjaHoradrica;

public sealed class ApiProblem(string code, string message, int status = 400, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public object? Details { get; } = details;
}

public sealed record OpenRequest(string Path, string? ProfileId);
public sealed record ProfileRequest(string ExcelPath, string? StringsPath, string? Name);
public sealed record SkillChange(int Id, int Points);
public sealed class GearChange
{
    public string Type { get; set; } = "unique";
    public string Name { get; set; } = "";
    public string? BaseCode { get; set; }
    public int Level { get; set; } = 99;
    public string? Slot { get; set; }
    public string? MercSlot { get; set; }
    public bool Ethereal { get; set; }
    public int? WeaponSet { get; set; }
    public string? ClientId { get; set; }
    public string? Page { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public List<string> Sockets { get; set; } = [];
}
public sealed class QuestRewardChange
{
    public bool Skills { get; set; }
    public bool Stats { get; set; }
    public bool Anya { get; set; }
}
public sealed record ItemStatChange(string ItemId, int StatId, int Layer, double Value);
public sealed record MercenaryChange(int? HirelingId, int? Level, bool Resurrect = false);
public sealed class CreateRequest
{
    public string? ProfileId { get; set; }
    public string ClassName { get; set; } = "Sorceress";
    public string Name { get; set; } = "Hero";
    public int Level { get; set; } = 1;
    public uint Version { get; set; } = 105;
    public string? Preset { get; set; }
}
public sealed record CloneRequest(string SessionId, string Name, string? PlanId = null);
public sealed record CharacterStatusChange(bool? Hardcore = null, bool? Dead = null, bool? Ladder = null);
public sealed class PreviewRequest
{
    public string SessionId { get; set; } = "";
    public string? Name { get; set; }
    public int? Level { get; set; }
    public long? Experience { get; set; }
    public CharacterStatusChange? Status { get; set; }
    public Dictionary<string, long>? Attributes { get; set; }
    public List<SkillChange>? Skills { get; set; }
    public List<GearChange>? Gear { get; set; }
    public List<string>? MoveToStashItemIds { get; set; }
    public List<ItemStatChange>? ItemStats { get; set; }
    public string? Preset { get; set; }
    public QuestRewardChange? QuestRewards { get; set; }
    public bool UnlockHell { get; set; }
    public bool UnlockWaypoints { get; set; }
    public bool FillTomes { get; set; }
    public bool FillBelt { get; set; }
    public Dictionary<int, int>? Hotkeys { get; set; }
    public Dictionary<int,string>? HotkeySides { get; set; }
    public int? LeftSkill { get; set; }
    public int? RightSkill { get; set; }
    public int? SwitchLeftSkill { get; set; }
    public int? SwitchRightSkill { get; set; }
    public MercenaryChange? Mercenary { get; set; }
    public WorldChange? World { get; set; }
    public List<ItemOperation>? ItemOperations { get; set; }
    public int? ActiveWeaponSet { get; set; }
    public LoadoutDocument? Loadout { get; set; }
    public bool LoadoutIncludeCharacter { get; set; } = true;
    public bool LoadoutKeepPrevious { get; set; } = true;
}
public sealed record ExportRequest(string SessionId, string PlanId, string? DestinationPath);
public sealed record CommitRequest(string SessionId, string PlanId, string ExpectedHash, string? DestinationPath = null);
public sealed record RestoreRequest(string SessionId, string BackupId, string ExpectedHash);
public sealed record ChangeEntry(string Section, string Label, object? Before, object? After);
public sealed record BackupEntry(string Id, string SourcePath, string Path, DateTimeOffset CreatedAt, string Hash);
public sealed class SaveSession
{
    public required string Id { get; init; }
    public required string SourcePath { get; init; }
    public required byte[] Original { get; set; }
    public required string Hash { get; set; }
    public required GameData Profile { get; init; }
    public required uint Version { get; init; }
    public required bool Roundtrip { get; init; }
    public DateTimeOffset OpenedAt { get; } = DateTimeOffset.UtcNow;
    public object Sync { get; } = new();
    public EditPlan? Plan { get; set; }
    public bool IsNew { get; init; }
}
public sealed record EditPlan(string Id, byte[] Bytes, IReadOnlyList<ChangeEntry> Diff, string[]? CharacterItemIds = null, string[]? MercItemIds = null);
public sealed class ItemDestination
{
    public string Owner { get; set; } = "character";
    public string Page { get; set; } = "stash";
    public string? Slot { get; set; }
    public int? WeaponSet { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
}
public sealed class ItemOperation
{
    public string Operation { get; set; } = "move";
    public string ItemId { get; set; } = "";
    public ItemDestination? Destination { get; set; }
    public string? ClientId { get; set; }
    public GearChange? Gear { get; set; }
    public ItemDesignRequest? Design { get; set; }
    public ItemBlueprintDocument? Blueprint { get; set; }
}
public sealed record ItemStatSpec(int Id, int Layer = 0, double? Value = null, long? RawValue = null);
public sealed class ItemDesignRequest
{
    public string BaseCode { get; set; } = "";
    public string Quality { get; set; } = "normal";
    public int Level { get; set; } = 99;
    public string? UniqueName { get; set; }
    public string? SetName { get; set; }
    public string? RunewordName { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public int[] PrefixIds { get; set; } = [];
    public int[] SuffixIds { get; set; } = [];
    public int RarePrefixId { get; set; }
    public int RareSuffixId { get; set; }
    public bool Ethereal { get; set; }
    public bool Identified { get; set; } = true;
    public string? PersonalizedName { get; set; }
    public int? Defense { get; set; }
    public int? Durability { get; set; }
    public int? MaxDurability { get; set; }
    public int? Quantity { get; set; }
    public int? SocketCount { get; set; }
    public List<ItemDesignRequest?> SocketChildren { get; set; } = [];
    public List<ItemStatSpec>? Stats { get; set; }
    public List<ItemStatSpec>? RunewordStats { get; set; }
    public List<List<ItemStatSpec>>? SetBonusLists { get; set; }
    public byte? SetBonusMask { get; set; }
}
public sealed record QuestChange(string Difficulty, string QuestId, string? State = null, ushort? Flags = null);
public sealed record WaypointChange(string Difficulty, int Id, bool Active);
public sealed class WorldChange
{
    public List<QuestChange> Quests { get; set; } = [];
    public List<WaypointChange> Waypoints { get; set; } = [];
    public string? ActiveDifficulty { get; set; }
    public int? Act { get; set; }
    public string? MaxDifficulty { get; set; }
}
public sealed class ItemBlueprintDocument
{
    public int SchemaVersion { get; set; } = 1;
    public uint SaveVersion { get; set; }
    public string ProfileFingerprint { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Quality { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public string DataHash { get; set; } = "";
    public System.Text.Json.JsonElement Description { get; set; }
}
public sealed record ProfileDescriptor(string ExcelPath, string StringsPath, string Name, string Fingerprint);
public sealed class DetachedPlanDocument
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? SourcePath { get; set; }
    public string ExpectedSourceHash { get; set; } = "";
    public required ProfileDescriptor Profile { get; set; }
    public uint SaveVersion { get; set; }
    public byte[] Candidate { get; set; } = [];
    public string CandidateHash { get; set; } = "";
    public List<ChangeEntry> Diff { get; set; } = [];
    public System.Text.Json.JsonElement Inspection { get; set; }
}
public sealed record LoadoutEntry(ItemDestination Destination,ItemBlueprintDocument Blueprint);
public sealed class LoadoutDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Build";
    public string ClassName { get; set; } = "";
    public uint SaveVersion { get; set; }
    public string ProfileFingerprint { get; set; } = "";
    public int? Level { get; set; }
    public Dictionary<string,long>? Attributes { get; set; }
    public List<SkillChange>? Skills { get; set; }
    public WorldChange? World { get; set; }
    public MercenaryChange? Mercenary { get; set; }
    public Dictionary<int,int>? Hotkeys { get; set; }
    public Dictionary<int,string>? HotkeySides { get; set; }
    public int? LeftSkill { get; set; }
    public int? RightSkill { get; set; }
    public int? SwitchLeftSkill { get; set; }
    public int? SwitchRightSkill { get; set; }
    public int? ActiveWeaponSet { get; set; }
    public List<LoadoutEntry> Entries { get; set; } = [];
}
public sealed record SaveLoadoutRequest(string SessionId,string Name,string? PlanId=null,bool IncludeCharacter=true);
public sealed record ImportLoadoutRequest(string SessionId,LoadoutDocument Document);
