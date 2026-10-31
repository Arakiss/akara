using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForjaHoradrica;

internal static class AdvancedCommands
{
    internal sealed class SquadManifest
    {
        [System.Text.Json.Serialization.JsonRequired] public int SchemaVersion { get; set; } = 1;
        [System.Text.Json.Serialization.JsonRequired] public List<SquadCharacter> Characters { get; set; } = [];
    }
    internal sealed class SquadCharacter
    {
        [System.Text.Json.Serialization.JsonRequired] public string Name { get; set; } = "";
        [System.Text.Json.Serialization.JsonRequired] public int ClassId { get; set; }
        public string? Preset { get; set; }
        public int Level { get; set; } = 1;
    }
    static readonly string[] Classes = ["Amazon", "Sorceress", "Necromancer", "Paladin", "Barbarian", "Druid", "Assassin", "Warlock"];
    public static object CreateSchema() => JsonSchema.For(typeof(CreateRequest));
    public static object BatchSchema() => JsonSchema.For(typeof(SquadManifest));

    public static async Task<object> Run(string command, Arguments options, EditorService service, string? profileId)
    {
        if (command == "items") return await ItemCommands.Run(options, service, profileId);
        if (command == "batch") return await Batch(options, service, profileId);
        var destination = Path.GetFullPath(Cli.Required(options, "out"));
        if (File.Exists(destination)) throw new ApiProblem("destination_exists", "El destino ya existe: " + destination, 409);
        JsonObject request;
        if (options.Get("request") is { } input)
        {
            if (options.Get("name") is not null || options.Get("class") is not null || options.Get("level") is not null || options.Get("preset") is not null) throw new UsageException("--request no se combina con --name, --class, --level o --preset.");
            request = JsonNode.Parse(await Cli.Input(input))?.AsObject() ?? throw new UsageException("--request necesita un objeto JSON.");
            if (request["name"] is null || request["className"] is null) throw new UsageException("--request requiere name y className explícitos.");
            if (profileId is not null) request["profileId"] = profileId;
        }
        else request = new JsonObject { ["name"] = Cli.Required(options, "name"), ["className"] = ClassName(Cli.Required(options, "class")), ["level"] = options.Number("level", 1), ["version"] = 105, ["profileId"] = profileId, ["preset"] = options.Get("preset") };
        var plan = Prepare(service, request);
        var exported = service.ExportDetachedPlan(plan, destination);
        return new { character = plan.Inspection.GetProperty("character"), export = exported, diff = plan.Diff };
    }

    static DetachedPlanDocument Prepare(EditorService service, JsonObject request)
    {
        string? preset = request["preset"]?.GetValue<string>();
        request["preset"] = null;
        var typed = request.Deserialize<CreateRequest>(Cli.Json) ?? throw new UsageException("La solicitud de creación está vacía.");
        var created = service.Create(typed);
        var session = Cli.Id(created, "sessionId");
        var result = service.Preview(new PreviewRequest { SessionId = session, Preset = preset });
        if (Cli.Node(result)["canSave"]?.GetValue<bool>() != true) throw new ApiProblem("create_invalid", "La construcción no supera la validación.", 422, Cli.Node(result)["validation"]);
        return service.DetachPlan(session, Cli.Id(result, "planId"));
    }

    static async Task<object> Batch(Arguments o, EditorService service, string? profileId)
    {
        var manifest = JsonSerializer.Deserialize<SquadManifest>(await Cli.Input(Cli.Required(o, "manifest")), Cli.Json) ?? throw new UsageException("El manifiesto está vacío.");
        if (manifest.SchemaVersion != 1 || manifest.Characters.Count is < 1 or > 32) throw new UsageException("El manifiesto requiere schemaVersion 1 y entre 1 y 32 personajes.");
        if (manifest.Characters.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Characters.Count) throw new UsageException("El manifiesto repite nombres.");
        if (manifest.Characters.Any(c => c.ClassId is < 0 or > 7)) throw new UsageException("classId debe estar entre 0 y 7. Usa schema create para descubrir clases.");
        var output = Path.GetFullPath(Cli.Required(o, "out-dir"));
        var plans = new List<(DetachedPlanDocument Plan, string Path)>();
        // Validate the entire batch before creating its first character file.
        foreach (var character in manifest.Characters)
        {
            var plan = Prepare(service, new JsonObject { ["name"] = character.Name, ["className"] = Classes[character.ClassId], ["level"] = character.Level, ["version"] = 105, ["profileId"] = profileId, ["preset"] = character.Preset });
            var path = Path.Combine(output, plan.Inspection.GetProperty("character").GetProperty("name").GetString()! + ".d2s");
            if (File.Exists(path)) throw new ApiProblem("destination_exists", "El lote no sobrescribe personajes: " + path, 409);
            service.ValidateDetachedPlan(plan);
            plans.Add((plan, path));
        }
        if (EditorService.GameRunning()) throw new ApiProblem("game_running", "Cierra Diablo II antes de crear un lote de personajes.", 409);
        Directory.CreateDirectory(output);
        var written = new List<(string Path, string Hash)>();
        var results = new List<object>();
        try
        {
            foreach (var (plan, path) in plans)
            {
                if (EditorService.GameRunning()) throw new ApiProblem("game_running", "Diablo II se abrió durante el lote.", 409);
                var result = service.ExportDetachedPlan(plan, path);
                written.Add((path, plan.CandidateHash));
                results.Add(new { name = plan.Inspection.GetProperty("character").GetProperty("name").GetString(), path, hash = plan.CandidateHash, character = plan.Inspection.GetProperty("character"), diff = plan.Diff });
            }
            return new { schemaVersion = 1, outputDirectory = output, created = results.Count, characters = results };
        }
        catch (Exception error)
        {
            var preserved = new List<string>();
            foreach (var (path, hash) in written.AsEnumerable().Reverse())
            {
                // Roll back only files created by this batch which remain byte-identical.
                try { if (File.Exists(path) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == hash) File.Delete(path); else preserved.Add(path); }
                catch (IOException) { preserved.Add(path); }
                catch (UnauthorizedAccessException) { preserved.Add(path); }
            }
            if (preserved.Count > 0) throw new ApiProblem("batch_partial", "El lote falló y algunos archivos cambiaron o no pudieron retirarse. Se han conservado.", 409, new { files = preserved, cause = error.Message });
            throw;
        }
    }
    static string ClassName(string name)
    {
        if (int.TryParse(name, out int id) && id is >= 0 and <= 7) return Classes[id];
        var normalized = name.ToLowerInvariant() switch { "amazona" => "Amazon", "hechicera" => "Sorceress", "nigromante" or "necro" => "Necromancer", "paladin" or "paladín" => "Paladin", "barbaro" or "bárbaro" => "Barbarian", "druida" => "Druid", "asesina" => "Assassin", "brujo" => "Warlock", _ => name };
        return Classes.FirstOrDefault(c => c.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ?? throw new UsageException("Clase desconocida. Clases: " + string.Join(", ", Classes));
    }
}
