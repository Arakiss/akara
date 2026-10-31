using System.Text.Json;
using System.Text.Json.Nodes;
using ForjaHoradrica;

internal static class ItemCommands
{
    public static async Task<object> Run(Arguments options, EditorService service, string? profileId)
    {
        var action = options.Positionals.ElementAtOrDefault(1) ?? "help";
        if (action == "help") return new
        {
            commands = new[] { "items list --save SAVE", "items export --save SAVE --id ITEM_ID --out item.akara-item.json", "items import --save SAVE --blueprint @item.akara-item.json --out plan.json", "items create --save SAVE --changes @design.json --out plan.json", "items edit --save SAVE --id ITEM_ID --changes @design.json --out plan.json" },
            destinationOptions = new[] { "--page stash|inventory|cube|belt|equipped", "--slot weapon|shield|helm|armor|amulet|ring1|ring2|gloves|belt|boots", "--owner character|mercenary", "--weapon-set 1|2" },
            schemas = new[] { "schema design", "schema blueprint", "schema preview" },
            note = "Crear e importar generan un plan revisable. Usa apply o export para guardarlo. Las bases y propiedades proceden del perfil nativo."
        };
        if (action is not ("list" or "export" or "import" or "create" or "edit")) throw new UsageException("Acción de objetos desconocida. Usa items help.");
        var savePath = Path.GetFullPath(options.Get("save") ?? options.Positionals.ElementAtOrDefault(2) ?? throw new UsageException("Falta --save RUTA.d2s."));
        var inspection = service.Open(new OpenRequest(savePath, profileId));
        var sessionId = Cli.Id(inspection, "sessionId");
        if (action == "list")
        {
            var root = Cli.Node(inspection);
            return new { sourceHash = root["sourceHash"], character = root["character"], items = root["items"], mercenary = root["mercenary"] };
        }
        var output = Path.GetFullPath(Cli.Required(options, "out"));
        if (File.Exists(output)) throw new ApiProblem("destination_exists", "El destino ya existe: " + output, 409);
        if (action == "export")
        {
            var blueprint = service.ExportBlueprint(sessionId, Cli.Required(options, "id"));
            Cli.WriteNewJson(output, blueprint);
            return new { blueprintPath = output, blueprint };
        }
        string? owner = options.Get("owner"), page = options.Get("page"), slot = options.Get("slot"), set = options.Get("weapon-set");
        var destination = action == "edit" && owner is null && page is null && slot is null && set is null ? null : new ItemDestination { Owner = owner ?? "character", Page = page ?? (slot is not null ? "equipped" : "stash"), Slot = slot, WeaponSet = set is not null ? int.TryParse(set, out var n) && n is 1 or 2 ? n : throw new UsageException("--weapon-set debe ser 1 o 2.") : null };
        var operation = new ItemOperation { Operation = action == "edit" ? "edit" : "create", ItemId = action == "edit" ? Cli.Required(options, "id") : "", ClientId = action == "edit" ? null : "cli-created-item", Destination = destination };
        if (action == "import") operation.Blueprint = JsonSerializer.Deserialize<ItemBlueprintDocument>(await Cli.Input(Cli.Required(options, "blueprint")), Cli.Json) ?? throw new UsageException("El blueprint está vacío.");
        else operation.Design = JsonSerializer.Deserialize<ItemDesignRequest>(await Cli.Input(Cli.Required(options, "changes")), Cli.Json) ?? throw new UsageException("El diseño está vacío.");
        var preview = service.Preview(new PreviewRequest { SessionId = sessionId, ItemOperations = [operation] });
        if (Cli.Node(preview)["canSave"]?.GetValue<bool>() != true) throw new ApiProblem("item_invalid", "El objeto no supera la validación nativa.", 422, Cli.Node(preview)["validation"]);
        var plan = service.DetachPlan(sessionId, Cli.Id(preview, "planId"));
        Cli.WriteNewJson(output, plan);
        return new { planPath = output, plan };
    }
}
