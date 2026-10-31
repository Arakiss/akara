using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using Akara.Desktop.Services;
using Akara.Desktop.ViewModels;
using ForjaHoradrica;

namespace Akara.Desktop.Testing;

/// <summary>Exercises opening state using only private copies and the real native commands.</summary>
internal static class SaveOpeningChecks
{
    internal static async Task RunAsync(byte[] fixture, string excelPath, string evidenceRoot, List<string> checks)
    {
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static bool SamePath(string? left, string right) => left is not null && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        static void Healthy(MainViewModel vm) => Require(vm.ErrorText.Length == 0, "Save-opening check: " + vm.ErrorText);
        string root = Path.Combine(evidenceRoot, "opening");
        string discoveredRoot = Path.Combine(root, "saved-games"), externalRoot = Path.Combine(root, "external");
        Directory.CreateDirectory(discoveredRoot); Directory.CreateDirectory(externalRoot);
        string discovered = Path.Combine(discoveredRoot, "Discovered.d2s");
        string externalA = Path.Combine(externalRoot, "External-A.d2s"), externalB = Path.Combine(externalRoot, "External-B.d2s");
        foreach (string path in new[] { discovered, externalA, externalB }) await File.WriteAllBytesAsync(path, fixture);

        var core = new EditorService(Path.Combine(root, "state"), () => false, discoveredRoot);
        var first = NativeEditorBridge.Object(core.AddProfile(new(excelPath, null, "Opening original tables")));
        // A second real, unmodified table snapshot distinguishes the two valid profile IDs.
        string alternateTables = Path.Combine(root, "alternate-excel"); Directory.CreateDirectory(alternateTables);
        foreach (string source in Directory.EnumerateFiles(excelPath, "*.txt")) File.Copy(source, Path.Combine(alternateTables, Path.GetFileName(source)));
        var second = NativeEditorBridge.Object(core.AddProfile(new(alternateTables, first.Text("stringsPath"), "Opening copied tables")));
        string profileId = first.Text("id"), alternateId = second.Text("id");
        Require(profileId != alternateId, "Private profile identities unexpectedly coincide.");
        var dialogs = new OpeningDialogs();
        var vm = new MainViewModel(dataRoot: Path.Combine(evidenceRoot, "fixture-ui"), core: core, fileDialogs: dialogs);
        await vm.InitializeAsync(); Healthy(vm);
        Require(vm.Saves.Count == 1 && SamePath(vm.Saves[0].Path, discovered), "Opening checks escaped the injected discovery directory.");
        await WorkbenchChecks.WelcomeAsync(vm,checks);
        var entryA = new SaveEntry("External A", externalA, profileId);
        var entryB = new SaveEntry("External B", externalB, profileId);
        vm.SelectedProfile = vm.Profiles.Single(profile => profile.Id == alternateId);
        await vm.OpenSaveAsync(entryA); Healthy(vm);
        Require(SamePath(vm.SourcePath, externalA) && SamePath(vm.SelectedSave?.Path, externalA) && vm.Saves.Contains(vm.SelectedSave!) && vm.SelectedProfile?.Id == profileId && vm.Snapshot.Text("profileId") == profileId, "Opening an entry failed to synchronize the accepted file and its own profile.");
        checks.Add("native-open-entry-profile-overrides-other-valid-selected-profile");

        var acceptedA = vm.SelectedSave;
        bool sawBusy = false, rejectedSelectionRetained = false, commandDisabled = false;
        Task? rejectedOpen = null;
        PropertyChangedEventHandler onBusy = (_, change) =>
        {
            if (change.PropertyName != nameof(MainViewModel.IsBusy) || !vm.IsBusy || sawBusy) return;
            sawBusy = true; vm.SelectedSave = entryB; rejectedOpen = vm.OpenSaveAsync(entryB);
            rejectedSelectionRetained = ReferenceEquals(vm.SelectedSave, acceptedA);
            commandDisabled = !vm.OpenSaveCommand.CanExecute(entryB);
        };
        vm.PropertyChanged += onBusy;
        try { await vm.RefreshLibraryAsync(); }
        finally { vm.PropertyChanged -= onBusy; }
        Healthy(vm);
        Require(sawBusy && rejectedSelectionRetained && commandDisabled && rejectedOpen?.IsCompletedSuccessfully == true && SamePath(vm.SourcePath, externalA), "Busy opening stranded the selector or changed the accepted character.");
        await ((NativeCommand)vm.OpenSaveCommand).ExecuteAsync(entryB); Healthy(vm);
        Require(SamePath(vm.SourcePath, externalB) && SamePath(vm.SelectedSave?.Path, externalB), "The same entry could not be retried after busy opening was rejected.");
        checks.Add("native-open-busy-rejection-retains-selection-and-same-entry-can-retry");

        long gold = vm.Snapshot.Obj("character").Obj("attributes").Long("gold");
        await vm.ApplyDraftAsync(new PreviewRequest { Attributes = new() { ["gold"] = gold + 1 } }); Healthy(vm);
        string pendingSnapshot = vm.Snapshot.ToJsonString(); var acceptedB = vm.SelectedSave;
        dialogs.AllowDiscard = false;
        await vm.OpenSaveAsync(entryA); Healthy(vm);
        Require(dialogs.Confirmations == 1 && vm.IsDirty && vm.Snapshot.ToJsonString() == pendingSnapshot && ReferenceEquals(vm.SelectedSave, acceptedB) && SamePath(vm.SourcePath, externalB), "Cancelling the discard prompt lost the draft or moved the selector.");
        checks.Add("native-open-cancel-preserves-draft-source-and-accepted-selection");

        dialogs.AllowDiscard = true;
        string retryPath = Path.Combine(externalRoot, "Retry.d2s"); var retryEntry = new SaveEntry("Retry", retryPath, profileId);
        await vm.OpenSaveAsync(retryEntry);
        Require(vm.ErrorText.Length > 0 && vm.IsDirty && vm.Snapshot.ToJsonString() == pendingSnapshot && ReferenceEquals(vm.SelectedSave, acceptedB) && SamePath(vm.SourcePath, externalB), "Failed opening replaced the accepted character or its draft.");
        await File.WriteAllBytesAsync(retryPath, fixture);
        await ((NativeCommand)vm.OpenSaveCommand).ExecuteAsync(retryEntry); Healthy(vm);
        Require(!vm.IsDirty && SamePath(vm.SourcePath, retryPath) && SamePath(vm.SelectedSave?.Path, retryPath), "An entry remained unusable after its missing file became available.");
        checks.Add("native-open-error-retains-draft-and-same-entry-can-retry");

        vm.SelectedProfile = vm.Profiles.Single(profile => profile.Id == alternateId);
        await vm.OpenPathAsync(retryPath); Healthy(vm);
        Require(vm.SelectedProfile?.Id == profileId && vm.Snapshot.Text("profileId") == profileId && SamePath(vm.SelectedSave?.Path, retryPath), "Opening a known path ignored its saved entry profile or left selection out of sync.");
        checks.Add("native-open-known-path-restores-its-entry-profile-and-selection");
        await vm.InitializeAsync(); Healthy(vm);
        Require(vm.Saves.Count == 2 && vm.Saves.Any(save => SamePath(save.Path, discovered)) && vm.Saves.Any(save => SamePath(save.Path, retryPath)) && SamePath(vm.SelectedSave?.Path, retryPath) && vm.HasSaves && vm.SaveSelectionText == vm.SelectedSave!.Name, "Refreshing leaked prior external paths or removed the current/discovered character.");
        string originalHash = Convert.ToHexString(SHA256.HashData(fixture));
        foreach (string path in new[] { discovered, externalA, externalB, retryPath })
            Require(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))) == originalHash, "Opening checks wrote a private character: " + Path.GetFileName(path));
        checks.Add("native-open-discovery-preserves-only-current-external-path-without-character-writes");
    }

    sealed class OpeningDialogs : IFileDialogs
    {
        public bool AllowDiscard { get; set; } = true;
        public int Confirmations { get; private set; }
        public bool Confirm(string title, string message) { Confirmations++; return AllowDiscard; }
        public string? OpenFile(string title, string filter) => throw new InvalidOperationException("Unexpected OS file dialog in an opening check.");
        public string? SaveFile(string title, string name, string filter) => throw new InvalidOperationException("Unexpected OS save dialog in an opening check.");
        public string? ChooseFolder(string title) => throw new InvalidOperationException("Unexpected OS folder dialog in an opening check.");
    }
}
