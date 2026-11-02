using System.Text.Json.Nodes;
using ForjaHoradrica;

namespace Akara.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    internal async Task<string?> CheckItemDesignAsync(ItemDesignRequest design,ItemViewModel? item)
    {
        try
        {
            var request=BuildRequest();
            (request.ItemOperations??=[]).Add(new(){Operation=item is null?"create":"edit",ItemId=item?.Id??"",ClientId=item is null?"validation-"+Guid.NewGuid().ToString("N"):null,Design=design,Destination=item is null?new(){Page="stash"}:null});
            var preview=await bridge.CallAsync(core=>core.Preview(request));
            return preview.Bool("canSave")?null:string.Join(Environment.NewLine,preview.Obj("validation").Array("errors").Select(error=>error?.GetValue<string>()));
        }
        catch(Exception exception){return exception is ApiProblem problem?problem.Message:L.T("No se puede aplicar este diseño: ")+exception.Message;}
    }
    static string FormatGuide(JsonObject guide)
    {
        var parts = new List<string>();
        void Add(string label, string value) { if (!string.IsNullOrWhiteSpace(value)) parts.Add(label + Environment.NewLine + value); }
        Add(L.T("Antes de combatir"), guide.Text("opening"));
        Add(L.T("Cómo jugar"), guide.Text("strategy"));
        var hotkeys = guide.Array("hotkeys").OfType<JsonObject>().Select(h => h.Text("key") + " · " + Names.Skill(h.Text("skill"))).ToArray();
        if (hotkeys.Length > 0) Add(L.T("Controles"), string.Join(Environment.NewLine, hotkeys));
        Add(L.T("Talismanes"), guide.Text("charms"));
        Add(L.T("Resistencias"), guide.Text("resists"));
        var alternatives = guide.Array("alternatives").OfType<JsonObject>().Select(a => a.Text("name")).Where(n => n.Length > 0).ToArray();
        if (alternatives.Length > 0) Add(L.T("Alternativas en el alijo"), string.Join(", ", alternatives));
        var notes = guide.Array("notes").Select(n => n?.GetValue<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        if (notes.Length > 0) Add(L.T("Ten en cuenta"), string.Join(Environment.NewLine, notes));
        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    /// <summary>Native catalog data for modal designer controls. Values remain validated by Core.</summary>
    public async Task<IReadOnlyList<CatalogEntry>> GetCatalogAsync(string type, string? query = null, bool allPages = false)
    {
        if (IsSyntheticDemo || sessionId.Length == 0) return [];
        var values = new List<CatalogEntry>();
        while (true)
        {
            var result = await bridge.CallAsync(c => c.Catalog(sessionId, type, query, values.Count, 250));
            var page = result.Array("entries").OfType<JsonObject>().ToArray();
            values.AddRange(page.Select(n => new CatalogEntry(n.Text("id"), n.Text("name"), n.Text("type", type), n.Text("code"), n.Bool("supported", true), n.Int("levelRequired"), n)));
            if (!allPages || page.Length == 0 || values.Count >= result.Int("total")) break;
        }
        return values;
    }

    public async Task<ItemDesignRequest> CreateDesignFromItemAsync(ItemViewModel item)
    {
        var design = DesignFromNode(item.Data);
        // A magical item's visible prefix/suffix names are needed by the native factory.
        // Core exports numeric affix IDs separately; resolve them through its catalogs.
        async Task ResolveMagic(ItemDesignRequest value)
        {
            if (value.Quality == "magic")
            {
                if (value.PrefixIds.FirstOrDefault() is > 0 and var prefix)
                    value.Prefix = (await GetCatalogAsync("prefix", allPages: true)).FirstOrDefault(e => e.Data.Int("nativeId") == prefix)?.Name;
                if (value.SuffixIds.FirstOrDefault() is > 0 and var suffix)
                    value.Suffix = (await GetCatalogAsync("suffix", allPages: true)).FirstOrDefault(e => e.Data.Int("nativeId") == suffix)?.Name;
            }
            foreach (var child in value.SocketChildren) if (child is not null) await ResolveMagic(child);
        }
        await ResolveMagic(design); return design;
    }

    public static ItemDesignRequest DesignFromNode(JsonObject node)
    {
        static List<ItemStatSpec> Stats(JsonArray array) => array.OfType<JsonObject>().Select(s => new ItemStatSpec(s.Int("id"), s.Int("layer"), RawValue: s.Long("value"))).ToList();
        string quality = node.Text("quality"), runeword = node.Text("runewordName");
        var design = new ItemDesignRequest
        {
            BaseCode = node.Text("code"), Quality = quality is "craft" ? "crafted" : quality,
            Level = node.Int("level", 99), UniqueName = quality == "unique" ? node.Text("name") : null, SetName = quality == "set" ? node.Text("name") : null,
            RunewordName = runeword.Length > 0 ? runeword : null,
            PrefixIds = node.Array("prefixIds").Select(v => v!.GetValue<int>()).ToArray(), SuffixIds = node.Array("suffixIds").Select(v => v!.GetValue<int>()).ToArray(),
            RarePrefixId = node.Int("rarePrefixId"), RareSuffixId = node.Int("rareSuffixId"), Ethereal = node.Bool("ethereal"), Identified = node.Bool("identified", true),
            PersonalizedName = node.Text("personalizedName") is { Length: > 0 } name ? name : null,
            Defense = node.NullableInt("defense"), Durability = node.NullableInt("durability"), MaxDurability = node.NullableInt("maxDurability"), Quantity = node.NullableInt("quantity"),
            SocketCount = runeword.Length > 0 ? null : node.Array("sockets").Count,
            SocketChildren = runeword.Length > 0 ? [] : node.Array("sockets").Select(n => n is JsonObject child ? DesignFromNode(child) : null).ToList(),
            Stats = Stats(node.Array("stats")),
            RunewordStats = runeword.Length > 0 ? Stats(node.Array("runewordStats")) : null,
            SetBonusMask = quality == "set" ? checked((byte)node.Int("setBonusMask")) : null,
            SetBonusLists = quality == "set" ? node.Array("setBonusLists").OfType<JsonArray>().Select(Stats).ToList() : null
        };
        return design;
    }
}
