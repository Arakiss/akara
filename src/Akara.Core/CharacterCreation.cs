using System.Text.Json;
using ForjaHoradrica.Characters;
using D2SSharp.Model;
using static ForjaHoradrica.GameData;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private GameData ResolveProfile(string? id) => id is not null ? profiles.GetValueOrDefault(id) ?? throw new ApiProblem("profile_missing", L.T("El perfil seleccionado no existe.")) : profiles.Values.FirstOrDefault() ?? throw new ApiProblem("profile_required", L.T("Selecciona un perfil de datos nativos."));
    public object Classes(string? profileId = null) => new { entries = ResolveProfile(profileId).Classes() };
    private object NewDraft(D2Save save, GameData profile)
    {
        profile.EnsureUnchanged(); var bytes = save.ToBytes(profile.External(save.Version), save.Version);
        if (!D2Save.VerifyChecksum(bytes) || !bytes.AsSpan().SequenceEqual(Read(bytes, profile, save.Version).ToBytes(profile.External(save.Version), save.Version))) throw new ApiProblem("new_character_encoding", L.T("El personaje nuevo no supera la verificación binaria."));
        var s = new SaveSession { Id = Guid.NewGuid().ToString("N"), SourcePath = Path.Combine(dataRoot, "drafts", save.Character.Preview.Name + ".d2s"), Original = bytes, Hash = Hash(bytes), Profile = profile, Version = save.Version, Roundtrip = true, IsNew = true };
        sessions[s.Id] = s; return Inspect(s, Read(bytes, profile, save.Version));
    }
    public object Create(CreateRequest request)
    {
        var p = ResolveProfile(request.ProfileId); var save = new NewCharacterFactory(p).Create(request.ClassName, request.Name, request.Level, request.Version);
        var inspection = NewDraft(save, p);
        if (request.Preset is null) return inspection;
        string id = JsonSerializer.SerializeToElement(inspection, ContractJson).GetProperty("sessionId").GetString()!;
        var preview = JsonSerializer.SerializeToElement(Preview(new() { SessionId = id, Preset = request.Preset }), ContractJson);
        if (!preview.GetProperty("canSave").GetBoolean()) throw new ApiProblem("preset_invalid", L.T("La plantilla no se puede aplicar al personaje nuevo."), 400, preview.GetProperty("validation"));
        var session = Session(id); session.Original = session.Plan!.Bytes; session.Hash = Hash(session.Original); session.Plan = null;
        return Inspect(session, Read(session.Original, p, session.Version));
    }
    public object Clone(CloneRequest request)
    {
        var source = Session(request.SessionId); lock (source.Sync)
        {
            var save = Read(request.PlanId is null ? source.Original : Plan(source, request.PlanId).Bytes, source.Profile, source.Version);
            ApplyCharacter(save, source.Profile, new() { Name = request.Name }, []); return NewDraft(save, source.Profile);
        }
    }
    private static object MercenaryDto(D2Save save, GameData p, ItemIdentities? identities)
    {
        var merc = save.Character.MercData; var rows = p.Read("hireling.txt");
        int version = save.Character.Preview.GameVersion == D2SSharp.Enums.GameVersion.Classic ? 0 : 100;
        var matching = rows.Where(r => I(r, "Id", -1) == merc.HirelingId && I(r, "Version") == version).ToArray();
        int coefficient = matching.Select(r => I(r, "Exp/Lvl")).FirstOrDefault(n => n > 0), level = 1;
        if (coefficient > 0) for (int n = 1; n <= 99; n++) { if ((long)coefficient * n * n * (n + 1) > merc.Experience) break; level = n; }
        var row = matching.Where(r => I(r, "Level") <= level).OrderByDescending(r => I(r, "Level")).FirstOrDefault() ?? matching.FirstOrDefault();
        string[] allowed = p.StatRows.Values.Any(r => V(r, "Stat") == "charm_weight") ? ["helm", "armor", "weapon", "shield", "amulet", "ring1", "ring2", "gloves", "belt", "boots"] : ["helm", "armor", "weapon", "shield"];
        return new { exists = merc.HasMerc, dead = merc.IsDead, typeId = merc.HirelingId, nameIndex = merc.NameIndex, experience = merc.Experience, level = merc.HasMerc ? level : 0, act = row is null ? 0 : I(row, "Act"), typeName = row is null ? "" : V(row, "Hireling"), aura = row is null ? null : Enumerable.Range(1, 6).Select(n => V(row, "Skill" + n)).FirstOrDefault(n => n is "Might" or "Defiance" or "Blessed Aim" or "Prayer" or "Thorns" or "Holy Freeze"), allowedSlots = allowed, items = save.MercItems?.Items.Select((item, index) => ItemDto(item, p, identities?.Id(item) ?? "m" + index, "mercenary", save)) ?? [] };
    }
}
