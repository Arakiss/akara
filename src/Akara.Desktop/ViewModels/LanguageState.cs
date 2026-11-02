using System.Collections.ObjectModel;
using Akara.Desktop.Localization;

namespace Akara.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    public bool IsEnglish => L.Language == "en";
    public bool IsSpanish => L.IsSpanish;
    public bool CanChangeLanguage => !IsBusy && !HasInputErrors;
    static ObservableCollection<NavEntry> Navigation() =>
    [
        new("character", L.T("Personaje"), "\uE77B"), new("skills", L.T("Habilidades"), "\uE734"),
        new("equipment", L.T("Equipo"), "\uE8B7"), new("mercenary", L.T("Mercenario"), "\uE716"),
        new("world", L.T("Mundo"), "\uE909"), new("workshop", L.T("Taller"), "\uE90F"),
        new("library", L.T("Biblioteca"), "\uE8F1"), new("changes", L.T("Cambios"), "\uE9D9")
    ];
    public Task ChangeLanguageAsync(string language)
    {
        if (!CanChangeLanguage || language == L.Language) return Task.CompletedTask;
        return RunAsync(async () =>
        {
            LanguagePreferences.Save(artworkDataRoot, language);
            LanguagePreferences.Apply(language);
            Replace(NavItems, Navigation());
            // Replay the same in-memory request to refresh localized validation and diffs.
            // This does not open, write or discard a save, or promote an unreviewed draft.
            bool wasReviewed = reviewed;
            string section = SelectedView;
            if (HasCharacter && !IsSyntheticDemo)
            {
                await PreviewCoreAsync(wasReviewed);
                await RefreshPresetsCoreAsync();
            }
            SelectedView = section;
            RefreshInventory();
            StatusText = L.T("Idioma cambiado. El borrador se conserva.");
            Raise("");
        });
    }
}
