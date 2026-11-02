using System.Globalization;
using System.Text.Json;

namespace ForjaHoradrica.Localization;

/// <summary>Presentation text only. Native identifiers, save data and JSON field names stay unchanged.</summary>
public static class EditorText
{
    static readonly IReadOnlyDictionary<string,string> English = Load();
    static string language = "en";
    public static string Language => language;
    public static bool IsSpanish => language == "es";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsSpanish ? "es-ES" : "en-US");
    public static void SetLanguage(string value)
    {
        if (value is not ("en" or "es")) throw new ArgumentException("Supported languages: en, es.", nameof(value));
        language = value;
    }
    public static string T(string source) => IsSpanish ? source : English.GetValueOrDefault(source, source);
    public static string F(string source, params object?[] arguments) => string.Format(Culture, T(source), arguments);
    public static bool HasTranslation(string source) => English.ContainsKey(source);
    static IReadOnlyDictionary<string,string> Load()
    {
        using var stream = typeof(EditorText).Assembly.GetManifestResourceStream("Akara.Core.Localization.en.json")
            ?? throw new InvalidOperationException("The English language catalog is missing.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
    }
}
