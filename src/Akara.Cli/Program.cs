using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ForjaHoradrica;

return await Cli.Run(args);

internal static class Cli
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        MaxDepth = 64
    };
    static readonly string[] Commands = ["help", "version", "doctor", "profiles", "saves", "classes", "inspect", "world", "catalog", "schema", "presets", "preview", "plan", "validate", "apply", "export", "clone", "create", "batch", "items", "loadouts", "backups", "restore"];
    static bool verbose;
    internal static async Task<int> Run(string[] args)
    {
        try
        {
            var options = new Arguments(args);
            verbose = options.Flag("verbose");
            var command = options.Positionals.FirstOrDefault() ?? "help";
            if (options.Flag("version")) command = "version";
            if (options.Flag("help") || command is "--help" or "-h") command = "help";
            if (command == "help") { Console.WriteLine(Help()); return 0; }
            ValidateOptions(command, options);
            if (command == "version") { Write(new { name = "AKARA CLI", version = "0.6.0", planSchemaVersion = 1 }); return 0; }
            var service = new EditorService(options.Get("data-dir"));
            string? profileId = null;
            if (options.Get("excel") is { } excel)
                profileId = Node(service.AddProfile(new ProfileRequest(Path.GetFullPath(excel), options.Get("strings"), options.Get("profile-name"))))["id"]!.GetValue<string>();
            else profileId = options.Get("profile");
            object result;
            switch (command)
            {
                case "doctor":
                    options.Allow("data-dir", "excel", "strings", "profile-name", "profile", "verbose");
                    var boot = Node(service.Bootstrap());
                    result = new { application = "AKARA CLI", version = "0.6.0", runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, gameRunning = EditorService.GameRunning(), profileCount = boot["profiles"]?.AsArray().Count ?? 0, profiles = boot["profiles"], saveCount = boot["saves"]?.AsArray().Count ?? 0, standalone = true, capabilities = new[] { "native-txt-profiles", "byte-exact-roundtrip", "persistent-plans", "preview-diff", "hash-conflict-guards", "verified-backups", "atomic-replace", "json-stdin-stdout" } };
                    break;
                case "profiles": result = service.Profiles(); break;
                case "classes": result = service.Classes(profileId); break;
                case "saves": result = new { saves = Node(service.Bootstrap())["saves"] }; break;
                case "presets": result = service.Presets(profileId); break;
                case "inspect": result = Open(service, options, profileId); break;
                case "world": result = service.World(Id(Open(service, options, profileId), "sessionId")); break;
                case "catalog":
                    var inspection = Open(service, options, profileId);
                    result = service.Catalog(Id(inspection, "sessionId"), options.Get("type") ?? "unique", options.Get("search"), options.Number("offset", 0), options.Number("limit", 100));
                    break;
                case "schema": result = Schema(options.Positionals.ElementAtOrDefault(1) ?? "commands"); break;
                case "preview":
                    var opened = Open(service, options, profileId);
                    var changes = await Changes(options);
                    changes.SessionId = Id(opened, "sessionId");
                    result = service.Preview(changes);
                    break;
                case "plan":
                    var save = SavePath(options);
                    var document = service.CreateDetachedPlan(new OpenRequest(save, profileId), await Changes(options));
                    var planPath = Required(options, "out");
                    WriteNewJson(planPath, document);
                    result = new { planPath = Path.GetFullPath(planPath), plan = document };
                    break;
                case "validate":
                    result = service.ValidateDetachedPlan(await ReadPlan(options));
                    break;
                case "apply":
                    var applyPlan = await ReadPlan(options);
                    result = service.ApplyDetachedPlan(applyPlan, options.Get("expect") ?? applyPlan.ExpectedSourceHash);
                    break;
                case "export":
                    result = service.ExportDetachedPlan(await ReadPlan(options), options.Get("out") is { } dest ? Path.GetFullPath(dest) : null);
                    break;
                case "clone":
                    var source = Open(service, options, profileId);
                    var cloneChanges = options.Get("changes") is null ? new PreviewRequest() : await Changes(options);
                    cloneChanges.SessionId = Id(source, "sessionId");
                    cloneChanges.Name = Required(options, "name");
                    var preview = service.Preview(cloneChanges);
                    if (Node(preview)["canSave"]?.GetValue<bool>() != true) throw new ApiProblem("preview_invalid", "La copia no supera la validación.", 422, Node(preview)["validation"]);
                    result = service.Export(new ExportRequest(cloneChanges.SessionId, Id(preview, "planId"), Path.GetFullPath(Required(options, "out"))));
                    break;
                case "backups":
                    var backupSession = Open(service, options, profileId);
                    result = service.GetBackups(Id(backupSession, "sessionId"));
                    break;
                case "restore":
                    var restoreSession = Open(service, options, profileId);
                    result = service.Restore(new RestoreRequest(Id(restoreSession, "sessionId"), Required(options, "backup"), Required(options, "expect")));
                    break;
                case "create": case "batch": case "items":
                    result = await AdvancedCommands.Run(command, options, service, profileId);
                    break;
                case "loadouts": result = await LoadoutCommands.Run(options, service, profileId); break;
                default: throw new UsageException("Comando desconocido: " + command + ". Usa akara-cli help.");
            }
            // Every command has a closed option vocabulary. Typos must never silently change a write.
            options.EnsureConsumed();
            Write(result);
            if (command == "preview" && Node(result)["canSave"]?.GetValue<bool>() == false) return 3;
            return 0;
        }
        catch (UsageException ex) { Error("usage", ex.Message); return 2; }
        catch (ApiProblem ex) { Error(ex.Code, ex.Message, ex.Details); return ex.Status == 409 ? 4 : 3; }
        catch (JsonException ex) { Error("json_invalid", ex.Message); return 2; }
        catch (IOException ex) when ((ex.HResult & 0xffff) is 80 or 183) { Error("destination_exists", "El archivo de destino ya existe."); return 4; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { Error("io_failed", ex.Message); return 5; }
        catch (Exception ex) { Error("internal_error", verbose ? ex.ToString() : ex.Message); return 1; }
    }
    internal static JsonNode Node(object value) => JsonSerializer.SerializeToNode(value, Json)!;
    internal static string Id(object value, string key) => Node(value)[key]?.GetValue<string>() ?? throw new InvalidDataException("El núcleo no devolvió " + key + ".");
    internal static object Open(EditorService service, Arguments o, string? profile) => service.Open(new OpenRequest(SavePath(o), profile));
    internal static string SavePath(Arguments o) => Path.GetFullPath(o.Get("save") ?? o.Positionals.ElementAtOrDefault(1) ?? throw new UsageException("Falta --save RUTA.d2s."));
    internal static string Required(Arguments o, string name) => o.Get(name) ?? throw new UsageException("Falta --" + name + ".");
    internal static async Task<string> Input(string value)
    {
        string text = value == "-" ? await Console.In.ReadToEndAsync() : value.StartsWith('@') ? await File.ReadAllTextAsync(Path.GetFullPath(value[1..])) : value.TrimStart().StartsWith('{') || value.TrimStart().StartsWith('[') ? value : await File.ReadAllTextAsync(Path.GetFullPath(value));
        if (text.Length > 64 * 1024 * 1024) throw new UsageException("La entrada JSON supera 64 MiB.");
        return text;
    }
    internal static async Task<PreviewRequest> Changes(Arguments o) => JsonSerializer.Deserialize<PreviewRequest>(await Input(Required(o, "changes")), Json) ?? throw new UsageException("Falta el objeto JSON de cambios.");
    static async Task<DetachedPlanDocument> ReadPlan(Arguments o) => JsonSerializer.Deserialize<DetachedPlanDocument>(await Input(o.Get("plan") ?? o.Positionals.ElementAtOrDefault(1) ?? throw new UsageException("Falta --plan ARCHIVO.json.")), Json) ?? throw new UsageException("El plan está vacío.");
    internal static void WriteNewJson(string path, object value)
    {
        if (File.Exists(Path.GetFullPath(path))) throw new ApiProblem("destination_exists", "El archivo de destino ya existe: " + path, 409);
        using var stream = new FileStream(Path.GetFullPath(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, value, Json); stream.Flush(true);
    }
    static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value, Json));
    static void Error(string code, string message, object? details = null) => Console.Error.WriteLine(JsonSerializer.Serialize(new { error = new { code, message, details } }, Json));
    static void ValidateOptions(string command, Arguments options)
    {
        string[] allowed = command switch
        {
            "version" or "doctor" or "profiles" or "saves" or "classes" or "presets" => [],
            "inspect" or "world" or "backups" => ["save"],
            "catalog" => ["save", "type", "search", "offset", "limit"],
            "schema" => [],
            "preview" => ["save", "changes"],
            "plan" => ["save", "changes", "out"],
            "validate" => ["plan"],
            "apply" => ["plan", "expect"],
            "export" => ["plan", "out"],
            "clone" => ["save", "name", "out", "changes"],
            "restore" => ["save", "backup", "expect"],
            "create" => ["name", "class", "out", "preset", "level", "request"],
            "batch" => ["manifest", "out-dir"],
            "items" => (options.Positionals.ElementAtOrDefault(1) ?? "help") switch
            {
                "help" => [],
                "list" => ["save"],
                "export" => ["save", "id", "out"],
                "import" => ["save", "out", "blueprint", "page", "slot", "owner", "weapon-set"],
                "create" => ["save", "out", "changes", "page", "slot", "owner", "weapon-set"],
                "edit" => ["save", "id", "out", "changes", "page", "slot", "owner", "weapon-set"],
                _ => throw new UsageException("Acción de objetos desconocida. Usa items help.")
            },
            "loadouts" => (options.Positionals.ElementAtOrDefault(1) ?? "help") switch
            {
                "help" or "list" => [],
                "show" or "delete" => ["id"],
                "export" => ["save", "name", "out", "items-only"],
                "save" => ["save", "name", "items-only"],
                "import" => ["save", "loadout"],
                "plan" => ["save", "id", "loadout", "out", "items-only", "discard-previous"],
                _ => throw new UsageException("Acción de biblioteca desconocida. Usa loadouts help.")
            },
            _ => throw new UsageException("Comando desconocido: " + command + ".")
        };
        options.Allow("data-dir", "excel", "strings", "profile-name", "profile", "verbose", "help", "version");
        options.Allow(allowed); options.EnsureConsumed();
        int maxPositionals = command is "items" or "loadouts" ? 3 : command is "inspect" or "world" or "catalog" or "schema" or "preview" or "plan" or "validate" or "apply" or "export" or "clone" or "backups" or "restore" ? 2 : 1;
        if (options.Positionals.Count > maxPositionals) throw new UsageException("Demasiados argumentos para " + command + ".");
        if (command is "inspect" or "world" or "catalog" or "preview" or "plan" or "clone" or "backups" or "restore")
            if (options.Positionals.Count > 1 && options.Has("save")) throw new UsageException("Indica la partida como argumento o con --save, una sola vez.");
        if (command is "validate" or "apply" or "export")
            if (options.Positionals.Count > 1 && options.Has("plan")) throw new UsageException("Indica el plan como argumento o con --plan, una sola vez.");
        if (command == "items" && options.Positionals.Count > 2 && (options.Has("save") || options.Positionals[1] == "help")) throw new UsageException("Indica una sola partida para items.");
        if (command == "loadouts" && options.Positionals.Count > 2 && (options.Has("id") || options.Positionals[1] is not ("show" or "delete"))) throw new UsageException("Indica un solo identificador para show o delete; el resto de acciones usa opciones.");
    }
    static object Schema(string kind) => kind switch
    {
        "preview" => JsonSchema.For(typeof(PreviewRequest)),
        "plan" => JsonSchema.For(typeof(DetachedPlanDocument)),
        "create" => AdvancedCommands.CreateSchema(),
        "batch" => AdvancedCommands.BatchSchema(),
        "design" => JsonSchema.For(typeof(ItemDesignRequest)),
        "blueprint" => JsonSchema.For(typeof(ItemBlueprintDocument)),
        "world" => JsonSchema.For(typeof(WorldChange)),
        "loadout" => JsonSchema.For(typeof(LoadoutDocument)),
        "commands" => new { schemaVersion = 1, commandNames = Commands, commands = CommandDescriptions(), schemas = new[] { "preview", "plan", "create", "batch", "design", "blueprint", "world", "loadout" }, globalOptions = new[] { "--excel PATH", "--strings PATH", "--profile ID", "--data-dir PATH", "--verbose", "--help", "--version" }, exitCodes = new { success = 0, unexpected = 1, usageOrJson = 2, validation = 3, conflict = 4, filesystem = 5 }, input = "JSON inline, @file, path, or - for stdin", output = "JSON on stdout; errors as JSON on stderr; no prompts" },
        _ => throw new UsageException("Schema: commands, preview, plan, create, batch, design, blueprint, world o loadout.")
    };
    static object[] CommandDescriptions() =>
    [
        new { name = "doctor", syntax = "doctor", writes = "none", description = "Runtime, profile and game-state diagnostics" },
        new { name = "inspect", syntax = "inspect SAVE | inspect --save SAVE", writes = "none", description = "Complete native character, inventory, skills and mercenary" },
        new { name = "world", syntax = "world SAVE", writes = "none", description = "Discover quest IDs/flags, waypoint IDs and difficulty state" },
        new { name = "catalog", syntax = "catalog SAVE --type TYPE [--search TEXT] [--offset N] [--limit N]", writes = "none", description = "Native unique, set, runeword, base, prefix, suffix, stat, skills and hireling catalogs" },
        new { name = "preview", syntax = "preview SAVE --changes JSON", writes = "none", description = "Validate PreviewRequest and return diff; invalid drafts exit3" },
        new { name = "plan", syntax = "plan SAVE --changes JSON --out PLAN", writes = "new-plan-file", description = "Persist a detached candidate with source/profile fingerprints" },
        new { name = "validate", syntax = "validate PLAN", writes = "none", description = "Recheck the candidate, source and native data" },
        new { name = "apply", syntax = "apply PLAN [--expect SHA256]", writes = "backup-and-atomic-source-replace", description = "Apply an existing-character plan while the game is closed" },
        new { name = "export", syntax = "export PLAN [--out SAVE]", writes = "new-save-file", description = "Create a native save without overwrite" },
        new { name = "create", syntax = "create --name NAME --class CLASS --out SAVE [--level N] [--preset ID] | create --request JSON --out SAVE", writes = "new-save-file", description = "Create one of eight classes from native data without a save template" },
        new { name = "clone", syntax = "clone SAVE --name NAME --out NEW_SAVE [--changes JSON]", writes = "new-save-file", description = "Export an independent character copy" },
        new { name = "batch", syntax = "batch --manifest JSON --out-dir DIR", writes = "new-save-files", description = "Prevalidate all characters and destinations; roll back unchanged new outputs on failure" },
        new { name = "items", syntax = "items help", writes = "blueprint-or-plan-files", description = "Discover list, export, import, create and edit item subcommands" },
        new { name = "loadouts", syntax = "loadouts help", writes = "local-library-or-plan-files", description = "Export, import, store and plan complete class-specific builds" },
        new { name = "backups", syntax = "backups SAVE", writes = "none", description = "Discover verified backups belonging to a save" },
        new { name = "restore", syntax = "restore SAVE --backup ID --expect SHA256", writes = "backup-and-atomic-source-replace", description = "Restore a selected historical save, preserving the displaced state" }
    ];
    static string Help() => """
    AKARA CLI 0.6.0 — editor local de personajes D2R

    akara-cli doctor                             Perfiles, juego y diagnóstico
    akara-cli profiles | saves | classes | presets
                                                 Descubrir recursos locales
    akara-cli inspect SAVE.d2s                    Inspección completa JSON
    akara-cli world SAVE.d2s                      Misiones, dificultades y puntos de ruta
    akara-cli catalog SAVE --type unique --search Oculus
    akara-cli schema [commands|preview|plan|create|batch|design|blueprint|world|loadout]
    akara-cli preview SAVE --changes @changes.json
    akara-cli plan SAVE --changes @changes.json --out plan.json
    akara-cli validate plan.json                  Verificar plan sin escribir
    akara-cli export plan.json --out COPY.d2s     Copia nueva, sin sobrescribir
    akara-cli apply plan.json                     Backup verificado y reemplazo atómico
    akara-cli clone SAVE --name NAME --out NAME.d2s [--changes @changes.json]
    akara-cli create --name NAME --class CLASS --out NAME.d2s [--preset ID]
    akara-cli batch --manifest @squad.json --out-dir DIRECTORY
    akara-cli items help                          Blueprints y taller de objetos
    akara-cli loadouts help                       Biblioteca de builds completas
    akara-cli backups SAVE
    akara-cli restore SAVE --backup ID --expect SHA256

    Globales: --excel DIRECTORY --strings DIRECTORY --data-dir DIRECTORY
              --profile ID --verbose --help
    Entrada JSON: @archivo, ruta, JSON literal o - para stdin. Sin preguntas.
    stdout: JSON. stderr: JSON de error. Códigos: 0 éxito, 2 uso, 3 validación, 4 conflicto, 5 IO.
    Los planes contienen datos del personaje: guárdalos como una partida privada.
    """;
}

internal sealed class UsageException(string message) : Exception(message);

internal sealed class Arguments
{
    readonly Dictionary<string, string?> values = new(StringComparer.Ordinal);
    readonly HashSet<string> consumed = new(StringComparer.Ordinal);
    public List<string> Positionals { get; } = [];
    public Arguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "-v") { if (!values.TryAdd(arg == "-h" ? "help" : "version", "true")) throw new UsageException("Opción repetida: " + arg); continue; }
            if (!arg.StartsWith("--")) { Positionals.Add(arg); continue; }
            string name = arg[2..], value;
            int equal = name.IndexOf('=');
            if (equal >= 0) { value = name[(equal + 1)..]; name = name[..equal]; }
            else if (name is "help" or "verbose" or "version" or "items-only" or "discard-previous") value = "true";
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) value = args[++i];
            else throw new UsageException("Falta el valor de --" + name + ".");
            if (!values.TryAdd(name, value)) throw new UsageException("Opción repetida: --" + name + ".");
        }
    }
    public string? Get(string name) { consumed.Add(name); return values.GetValueOrDefault(name); }
    public bool Has(string name) => values.ContainsKey(name);
    public bool Flag(string name) { consumed.Add(name); return values.TryGetValue(name, out var value) ? bool.TryParse(value ?? "true", out var enabled) ? enabled : throw new UsageException("--" + name + " admite true o false.") : false; }
    public int Number(string name, int fallback) => Get(name) is { } value ? int.TryParse(value, out var n) ? n : throw new UsageException("--" + name + " debe ser un entero.") : fallback;
    public void Allow(params string[] names) { foreach (var name in names) consumed.Add(name); }
    public void EnsureConsumed() { var unknown = values.Keys.Where(k => !consumed.Contains(k)).ToArray(); if (unknown.Length > 0) throw new UsageException("Opciones desconocidas: " + string.Join(", ", unknown.Select(k => "--" + k))); }
}

internal static class JsonSchema
{
    public static object For(Type root)
    {
        var defs = new JsonObject(); var visited = new HashSet<Type>();
        JsonObject Describe(Type type)
        {
            var nullable = Nullable.GetUnderlyingType(type); if (nullable is not null) return new JsonObject { ["anyOf"] = new JsonArray(Describe(nullable), new JsonObject { ["type"] = "null" }) };
            if (type == typeof(string) || type == typeof(DateTimeOffset) || type == typeof(DateTime)) return new JsonObject { ["type"] = "string" };
            if (type == typeof(byte[])) return new JsonObject { ["type"] = "string", ["contentEncoding"] = "base64" };
            if (type == typeof(bool)) return new JsonObject { ["type"] = "boolean" };
            if (type.IsEnum) return new JsonObject { ["type"] = "integer", ["description"] = string.Join(", ", Enum.GetNames(type)) };
            if (type.IsPrimitive || type == typeof(decimal)) return new JsonObject { ["type"] = type == typeof(float) || type == typeof(double) || type == typeof(decimal) ? "number" : "integer" };
            if (type == typeof(object) || type == typeof(JsonElement) || typeof(JsonNode).IsAssignableFrom(type)) return new JsonObject();
            var dictionary = type.GetInterfaces().Append(type).FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            if (dictionary is not null) return new JsonObject { ["type"] = "object", ["additionalProperties"] = Describe(dictionary.GetGenericArguments()[1]) };
            if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type)) return new JsonObject { ["type"] = "array", ["items"] = Describe(type.IsArray ? type.GetElementType()! : type.GetGenericArguments().FirstOrDefault() ?? typeof(object)) };
            if (visited.Add(type))
            {
                var properties = new JsonObject(); defs[type.Name] = new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
                foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead)) properties[JsonNamingPolicy.CamelCase.ConvertName(prop.Name)] = Describe(prop.PropertyType);
                Constrain(type, defs[type.Name]!.AsObject(), properties);
            }
            return new JsonObject { ["$ref"] = "#/$defs/" + type.Name };
        }
        var schema = Describe(root); schema["$schema"] = "https://json-schema.org/draft/2020-12/schema"; schema["$defs"] = defs;
        return schema;
    }
    static void Constrain(Type type, JsonObject definition, JsonObject properties)
    {
        void EnumValues(string key, params string[] values) { if (properties[key] is JsonObject property) property["enum"] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()); }
        void Range(string key, long minimum, long maximum) { if (properties[key] is JsonObject property) { property["minimum"] = minimum; property["maximum"] = maximum; } }
        if (type == typeof(CreateRequest))
        {
            EnumValues("className", "Amazon", "Sorceress", "Necromancer", "Paladin", "Barbarian", "Druid", "Assassin", "Warlock");
            properties["name"]!["pattern"] = "^[A-Za-z_-]{2,15}$";
            properties["version"]!["const"] = 105;
            definition["required"] = new JsonArray("name", "className");
        }
        if (type == typeof(ItemDesignRequest))
        {
            EnumValues("quality", "normal", "inferior", "superior", "magic", "rare", "crafted", "unique", "set");
            Range("level", 1, 99); Range("socketCount", 0, 15); Range("rarePrefixId", 0, 255); Range("rareSuffixId", 0, 255);
            definition["required"] = new JsonArray("baseCode");
            definition["description"] = "Native base, stat ranges, affix compatibility and socket capacity are validated against the selected profile. Discover them through catalog.";
        }
        if (type == typeof(ItemStatSpec))
        {
            Range("id", 0, 510); Range("layer", 0, int.MaxValue);
            definition["required"] = new JsonArray("id");
            definition["oneOf"] = new JsonArray(new JsonObject { ["required"] = new JsonArray("value"), ["not"] = new JsonObject { ["required"] = new JsonArray("rawValue") } }, new JsonObject { ["required"] = new JsonArray("rawValue"), ["not"] = new JsonObject { ["required"] = new JsonArray("value") } });
        }
        if (type == typeof(ItemDestination)) { EnumValues("owner", "character", "mercenary"); EnumValues("page", "inventory", "stash", "cube", "belt", "equipped"); Range("weaponSet", 1, 2); Range("x", 0, 255); Range("y", 0, 255); }
        if (type == typeof(ItemOperation)) EnumValues("operation", "create", "edit", "copy", "move", "equip", "delete");
        if (type == typeof(AdvancedCommands.SquadManifest)) definition["required"] = new JsonArray("schemaVersion", "characters");
        if (type == typeof(AdvancedCommands.SquadCharacter)) { definition["required"] = new JsonArray("name", "classId"); Range("classId", 0, 7); properties["name"]!["pattern"] = "^[A-Za-z_-]{2,15}$"; }
        if (type == typeof(DetachedPlanDocument) || type == typeof(ItemBlueprintDocument) || type == typeof(LoadoutDocument) || type == typeof(AdvancedCommands.SquadManifest)) properties["schemaVersion"]!["const"] = 1;
    }
}
