using System.Text.Json;
using ForjaHoradrica;

internal static class LoadoutCommands
{
    public static async Task<object> Run(Arguments o, EditorService service, string? profileId)
    {
        var action = o.Positionals.ElementAtOrDefault(1) ?? "help";
        if (action == "help") return new
        {
            commands = new[] { "loadouts list", "loadouts show ID", "loadouts export --save SAVE --name NAME --out BUILD.json [--items-only]", "loadouts save --save SAVE --name NAME [--items-only]", "loadouts import --save SAVE --loadout @BUILD.json", "loadouts plan --save SAVE --loadout @BUILD.json --out PLAN.json [--items-only] [--discard-previous]", "loadouts plan --save SAVE --id ID --out PLAN.json", "loadouts delete ID" },
            schema = "schema loadout",
            note = "La biblioteca es local. Plan conserva el equipo anterior en el alijo por defecto y no escribe la partida. --discard-previous prepara su eliminación explícita, visible en el diff; aplicar conserva un respaldo."
        };
        if (action == "list") return service.Loadouts();
        if (action is "show" or "delete")
        {
            var id = o.Get("id") ?? o.Positionals.ElementAtOrDefault(2) ?? throw new UsageException("Falta el identificador de biblioteca.");
            return action == "show" ? service.GetLoadout(id) : service.DeleteLoadout(id);
        }
        var source = o.Get("save") ?? throw new UsageException("Falta --save RUTA.d2s.");
        var session = Cli.Id(service.Open(new OpenRequest(Path.GetFullPath(source), profileId)), "sessionId");
        bool includeCharacter = !o.Flag("items-only");
        if (action == "save") return service.SaveLoadout(new SaveLoadoutRequest(session, Cli.Required(o, "name"), IncludeCharacter: includeCharacter));
        if (action == "export")
        {
            var document = service.ExportLoadout(session, Cli.Required(o, "name"), includeCharacter: includeCharacter);
            var path = Path.GetFullPath(Cli.Required(o, "out")); Cli.WriteNewJson(path, document);
            return new { loadoutPath = path, document };
        }
        LoadoutDocument loadout;
        if (o.Get("id") is { } libraryId)
        {
            if (o.Get("loadout") is not null) throw new UsageException("Elige --id o --loadout.");
            loadout = service.GetLoadout(libraryId);
        }
        else loadout = JsonSerializer.Deserialize<LoadoutDocument>(await Cli.Input(Cli.Required(o, "loadout")), Cli.Json) ?? throw new UsageException("La configuración está vacía.");
        if (action == "import") return service.ImportLoadout(new ImportLoadoutRequest(session, loadout));
        if (action != "plan") throw new UsageException("Acción desconocida. Usa loadouts help.");
        var preview = service.Preview(new PreviewRequest { SessionId = session, Loadout = loadout, LoadoutIncludeCharacter = includeCharacter, LoadoutKeepPrevious = !o.Flag("discard-previous") });
        if (Cli.Node(preview)["canSave"]?.GetValue<bool>() != true) throw new ApiProblem("loadout_invalid", "La configuración no supera la validación.", 422, Cli.Node(preview)["validation"]);
        var plan = service.DetachPlan(session, Cli.Id(preview, "planId"));
        var output = Path.GetFullPath(Cli.Required(o, "out")); Cli.WriteNewJson(output, plan);
        return new { planPath = output, plan };
    }
}
