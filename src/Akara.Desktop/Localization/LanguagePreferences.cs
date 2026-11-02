using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Akara.Desktop.Localization;

public static class LanguagePreferences
{
    public static string PathFor(string? dataRoot) => Path.Combine(dataRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AKARA"), "desktop-preferences.json");
    public static string Load(string? dataRoot)
    {
        try
        {
            if (!File.Exists(PathFor(dataRoot))) return "en";
            using var data = JsonDocument.Parse(File.ReadAllText(PathFor(dataRoot)));
            return data.RootElement.TryGetProperty("language", out var language) && language.GetString() == "es" ? "es" : "en";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return "en"; }
    }
    public static void Save(string? dataRoot, string language)
    {
        if (language is not ("en" or "es")) throw new ArgumentException("Supported languages: en, es.", nameof(language));
        string path = PathFor(dataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { language }));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Apply(string language)
    {
        L.SetLanguage(language);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = L.Culture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = L.Culture;
    }
}
