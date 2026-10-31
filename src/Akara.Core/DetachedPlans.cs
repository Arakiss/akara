using System.Text.Json;
using D2SSharp.Model;

namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private static readonly JsonSerializerOptions ContractJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, MaxDepth = 64 };
    public DetachedPlanDocument CreateDetachedPlan(OpenRequest source, PreviewRequest changes)
    {
        var opened = JsonSerializer.SerializeToElement(Open(source), ContractJson);
        changes.SessionId = opened.GetProperty("sessionId").GetString()!;
        var result = JsonSerializer.SerializeToElement(Preview(changes), ContractJson);
        if (!result.GetProperty("canSave").GetBoolean()) throw new ApiProblem("plan_invalid", "El plan contiene cambios no válidos.", 400, result.GetProperty("validation"));
        return DetachPlan(changes.SessionId, result.GetProperty("planId").GetString()!);
    }
    public DetachedPlanDocument DetachPlan(string sessionId, string planId)
    {
        var s = Session(sessionId); lock (s.Sync)
        {
            var plan = Plan(s, planId); s.Profile.EnsureUnchanged();
            return new() { SourcePath = s.IsNew ? null : s.SourcePath, ExpectedSourceHash = s.Hash, Profile = new(s.Profile.ExcelPath, s.Profile.StringsPath, s.Profile.Name, s.Profile.Fingerprint()), SaveVersion = s.Version, Candidate = plan.Bytes.ToArray(), CandidateHash = Hash(plan.Bytes), Diff = plan.Diff.ToList(), Inspection = JsonSerializer.SerializeToElement(Inspect(s, Read(plan.Bytes, s.Profile, s.Version)), ContractJson) };
        }
    }
    private SaveSession ImportDetachedPlan(DetachedPlanDocument document)
    {
        if (document.SchemaVersion != 1 || document.Profile is null || document.Candidate is null || document.Candidate.Length is < 400 or > 16 * 1024 * 1024) throw new ApiProblem("plan_format", "El archivo no es un plan de AKARA compatible.");
        var p = new GameData(document.Profile.ExcelPath, document.Profile.StringsPath, document.Profile.Name);
        if (p.Fingerprint() != document.Profile.Fingerprint) throw new ApiProblem("profile_changed", "Las tablas no coinciden con las usadas para preparar este plan.", 409);
        if (document.SaveVersion is < 97 or > 105 || BitConverter.ToUInt32(document.Candidate, 4) != document.SaveVersion || !D2Save.VerifyChecksum(document.Candidate) || Hash(document.Candidate) != document.CandidateHash) throw new ApiProblem("plan_candidate", "El candidato del plan no supera su hash, versión o checksum.");
        var candidate = Read(document.Candidate, p, document.SaveVersion);
        if (!document.Candidate.AsSpan().SequenceEqual(candidate.ToBytes(p.External(document.SaveVersion), document.SaveVersion))) throw new ApiProblem("plan_roundtrip", "El candidato no conserva una codificación binaria estable.");
        ValidatePlacement(candidate, p);
        ValidateEquipment(candidate, p);
        foreach (var item in candidate.Items.Concat(candidate.MercItems?.Items ?? [])) ValidateStatUniqueness(item);
        ApplyCharacter(candidate, p, new(), []);
        byte[] original;
        if (document.SourcePath is not null)
        {
            original = ReadSave(document.SourcePath);
            if (Hash(original) != document.ExpectedSourceHash) throw new ApiProblem("source_changed", "La partida cambió desde que se preparó este plan.", 409);
            if (BitConverter.ToUInt32(original, 4) != document.SaveVersion) throw new ApiProblem("plan_version", "El plan no puede convertir la versión de la partida.");
        }
        else original = document.Candidate.ToArray();
        var s = new SaveSession { Id = Guid.NewGuid().ToString("N"), SourcePath = document.SourcePath is null ? Path.Combine(dataRoot, "drafts", candidate.Character.Preview.Name + ".d2s") : Path.GetFullPath(document.SourcePath), Original = original, Hash = document.ExpectedSourceHash, Profile = p, Version = document.SaveVersion, Roundtrip = true, IsNew = document.SourcePath is null, Plan = new(Guid.NewGuid().ToString("N"), document.Candidate.ToArray(), document.Diff) };
        profiles[p.Id] = p; sessions[s.Id] = s; return s;
    }
    public object ValidateDetachedPlan(DetachedPlanDocument document)
    {
        var s = ImportDetachedPlan(document);
        return new { valid = true, candidateHash = document.CandidateHash, sourceHash = document.ExpectedSourceHash, summary = Inspect(s, Read(document.Candidate, s.Profile, s.Version)) };
    }
    public object ExportDetachedPlan(DetachedPlanDocument document, string? destinationPath = null)
    {
        var s = ImportDetachedPlan(document); return Export(new(s.Id, s.Plan!.Id, destinationPath));
    }
    public object ApplyDetachedPlan(DetachedPlanDocument document, string expectedHash, string? destinationPath = null)
    {
        if (expectedHash != document.ExpectedSourceHash) throw new ApiProblem("source_changed", "El hash esperado no coincide con el plan.", 409);
        var s = ImportDetachedPlan(document);
        if (s.IsNew) return Export(new(s.Id, s.Plan!.Id, destinationPath));
        if (destinationPath is not null) throw new ApiProblem("plan_destination", "Un plan sobre una partida existente guarda en su origen; usa export para crear una copia.");
        return Commit(new(s.Id, s.Plan!.Id, expectedHash));
    }
}
