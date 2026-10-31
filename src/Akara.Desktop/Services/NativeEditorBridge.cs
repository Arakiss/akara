using System.Text.Json;
using System.Text.Json.Nodes;
using ForjaHoradrica;
using Microsoft.Win32;
using System.Windows;

namespace Akara.Desktop.Services;

/// <summary>The desktop application calls the same Core directly. No web host or transport exists here.</summary>
public sealed class NativeEditorBridge
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        MaxDepth = 64
    };

    public EditorService Core { get; }
    public NativeEditorBridge(string? dataRoot = null, EditorService? core = null) => Core = core ?? new EditorService(dataRoot);
    public static JsonObject Object(object value) => JsonSerializer.SerializeToNode(value, Json)!.AsObject();
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    public Task<JsonObject> CallAsync(Func<EditorService, object> operation) => Task.Run(() => Object(operation(Core)));
    public Task<T> CallAsync<T>(Func<EditorService, T> operation) => Task.Run(() => operation(Core));
}

public interface IFileDialogs
{
    string? OpenFile(string title, string filter);
    string? SaveFile(string title, string fileName, string filter);
    string? ChooseFolder(string title);
    bool Confirm(string title, string message);
}

public sealed class WindowsFileDialogs : IFileDialogs
{
    public string? OpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public string? SaveFile(string title, string fileName, string filter)
    {
        // Core's exclusive create remains authoritative even if a file appears after this dialog.
        var dialog = new SaveFileDialog { Title = title, FileName = fileName, Filter = filter, AddExtension = true, OverwritePrompt = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public string? ChooseFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
    public bool Confirm(string title, string message) => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
