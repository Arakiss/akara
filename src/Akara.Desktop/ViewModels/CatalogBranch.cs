namespace Akara.Desktop.ViewModels;

/// <summary>A presentation branch; the catalog entries still come from the selected Core profile.</summary>
public sealed class CatalogBranch(string key, string title) : ObservableModel
{
    IEnumerable<CatalogEntry> entries = [];
    bool expanded;
    public string Key { get; } = key;
    public string Title { get; } = title;
    public IEnumerable<CatalogEntry> Entries { get => entries; set => Set(ref entries, value); }
    public bool IsExpanded { get => expanded; set => Set(ref expanded, value); }
}
