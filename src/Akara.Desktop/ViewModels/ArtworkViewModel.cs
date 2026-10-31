using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using Akara.Desktop.Services;

namespace Akara.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    readonly string? artworkDataRoot;
    string artworkStatus = "Los gráficos se leen de tu instalación del juego.";
    public string ArtworkStatus { get => artworkStatus; private set => Set(ref artworkStatus, value); }

    async Task PrepareArtworkAsync(JsonObject snapshot)
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == snapshot.Text("profileId")) ?? SelectedProfile;
        if (profile is null) return;
        string gameRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Diablo II Resurrected");
        for (var folder = new DirectoryInfo(profile.ExcelPath); folder is not null; folder = folder.Parent)
            if (Directory.Exists(Path.Combine(folder.FullName, "Data", "data"))) { gameRoot = folder.FullName; break; }
        IEnumerable<JsonObject> Walk(JsonObject item)
        {
            yield return item;
            foreach (var child in item.Array("sockets").OfType<JsonObject>().SelectMany(Walk)) yield return child;
        }
        var items = snapshot.Array("items").OfType<JsonObject>()
            .Concat(snapshot.Obj("mercenary").Array("items").OfType<JsonObject>()).SelectMany(Walk).ToArray();
        try
        {
            var result = await LocalGameArt.WarmupAsync(gameRoot, profile.ExcelPath, items, artworkDataRoot);
            if (Application.Current is { } application)
            {
                void UpdateResources()
                {
                    application.Resources["DisplayFont"] = LocalGameArt.TryGetDisplayFont() ?? new FontFamily("Palatino Linotype");
                    foreach (string key in new[] { "DiabloDivider", "DiabloPortraitFrame" })
                        if (LocalGameArt.TryGetUiArt(key) is { } image) application.Resources[key] = image;
                        else application.Resources.Remove(key);
                }
                if (application.Dispatcher.CheckAccess()) UpdateResources();
                else await application.Dispatcher.InvokeAsync(UpdateResources);
            }
            ArtworkStatus = result.Missing.Count == 0
                ? "Gráficos originales de tu instalación · " + result.ItemsLoaded + " objetos"
                : "Gráficos originales · " + result.ItemsLoaded + " objetos · " + result.Missing.Count + " imágenes no disponibles";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            ArtworkStatus = "No se pudieron leer los gráficos del juego. Las imágenes ausentes usan un marcador neutro.";
        }
    }
}
