using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-ES");
        string? Option(string key)
        {
            int index = Array.IndexOf(e.Args, "--" + key);
            return index >= 0 && index + 1 < e.Args.Length ? e.Args[index + 1] : null;
        }
        try
        {
            if (Option("capture") is not null || Option("self-test") is not null) RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            if (Option("self-test") is { } report)
            {
                int exit = await Testing.NativeSmoke.RunAsync(report, Option("open"), Option("excel"), Option("data-dir"));
                Shutdown(exit); return;
            }
            var vm = new MainViewModel(Option("data-dir"));
            await vm.InitializeAsync();
            if (Option("open") is { } path) await vm.OpenPathAsync(path);
            else if (Option("scenario") == "demo") await vm.CreateDemoAsync();
            if (Option("section") is { } section) vm.SelectedView = section;
            if (Option("capture") is { } capture)
            {
                int width = int.TryParse(Option("width"), out int w) ? Math.Clamp(w, 960, 3840) : 1600;
                int height = int.TryParse(Option("height"), out int h) ? Math.Clamp(h, 700, 2160) : 1000;
                await CaptureAsync(vm, capture, width, height);
                Shutdown(0); return;
            }
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var window = new MainWindow { DataContext = vm };
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            string text = exception.ToString();
            if (Option("capture") is { } capture) await File.WriteAllTextAsync(capture + ".error.txt", text);
            else if (Option("self-test") is { } report) await File.WriteAllTextAsync(report + ".error.txt", text);
            else MessageBox.Show(exception.Message, "AKARA", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task CaptureAsync(MainViewModel vm, string path, int width, int height)
    {
        // The very same control used by the native window is rendered offscreen.
        // No Window.Show, input injection, browser, or screenshot of the desktop.
        var root = new WorkbenchView { DataContext = vm, Width = width, Height = height };
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using (var file = File.Create(fullPath)) encoder.Save(file);
        var metadata = new
        {
            framework = typeof(Application).Assembly.GetName().Name,
            rootControl = root.GetType().FullName,
            width, height,
            actualWidth = root.ActualWidth,
            actualHeight = root.ActualHeight,
            section = vm.SelectedView,
            visibleWindowCreated = false,
            native = true,
            browser = false
        };
        await File.WriteAllTextAsync(fullPath + ".json", JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    }
}
