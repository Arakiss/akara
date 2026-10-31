using System.IO;
using System.Security.Cryptography;
using System.Resources;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Akara.Desktop.Controls;
using Akara.Desktop.Services;
using Akara.Desktop.ViewModels;
using ForjaHoradrica;

namespace Akara.Desktop.Testing;

/// <summary>Behavior checks run in the application's STA dispatcher, with no visible window or input injection.</summary>
public static class NativeSmoke
{
    public static async Task<int> RunAsync(string reportPath, string? savePath = null, string? excelPath = null, string? dataDir = null)
    {
        var checks = new List<string>(); var failures = new List<string>(); var omissions = new List<string>();
        string root = Path.Combine(Path.GetTempPath(), "akara-native-behavior-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        void Healthy(MainViewModel vm, string context) { if (vm.ErrorText.Length > 0) throw new InvalidOperationException(context + ": " + vm.ErrorText); }
        try
        {
            Check(typeof(Application).Assembly.GetName().Name == "PresentationFramework", "actual-wpf-framework");
            Check(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA, "sta-ui-thread");
            var resourceNames = new List<string>();
            foreach (string resourceName in typeof(App).Assembly.GetManifestResourceNames().Where(n => n.EndsWith(".g.resources", StringComparison.Ordinal)))
            {
                using var stream = typeof(App).Assembly.GetManifestResourceStream(resourceName)!;
                using var reader = new ResourceReader(stream);
                foreach (System.Collections.DictionaryEntry entry in reader) resourceNames.Add(entry.Key.ToString() ?? "");
            }
            Check(resourceNames.All(n => !n.Contains("class-portraits", StringComparison.OrdinalIgnoreCase)), "rejected-portrait-candidate-not-embedded");
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name is { } name && (name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || name.Contains("WebView2", StringComparison.Ordinal))), "no-web-host-or-webview-assembly");
            var demo = new MainViewModel(dataRoot: Path.Combine(root, "demo"), fileDialogs: new NoDialogs());
            await demo.InitializeAsync(); Healthy(demo, "initialize");
            Check(!demo.CanCommit && !demo.SaveCommand.CanExecute(null), "save-disabled-before-open-and-review");
            await ((NativeCommand)demo.SelectViewCommand).ExecuteAsync("skills");
            Check(demo.SelectedView == "skills", "native-navigation-command");
            Check(demo.NavItems.Select(n => n.Key).Distinct().Count() == demo.NavItems.Count && demo.NavItems.Count >= 7, "native-navigation-model");
            await demo.CreateDemoAsync(); Healthy(demo, "demo");
            Check(demo.Snapshot["character"] is JsonObject character && character["name"]?.GetValue<string>() is { Length: > 0 }, "native-demo-populates-viewmodel");
            Check(!demo.CanCommit && !demo.SaveCommand.CanExecute(null), "demo-never-auto-saves");

            if (savePath is null && excelPath is null) omissions.Add("private-fixture-flows-not-requested");
            else
            {
                if (savePath is null || excelPath is null) throw new ArgumentException("Provide both --open and --excel for private-file behavior checks.");
                savePath = Path.GetFullPath(savePath); excelPath = Path.GetFullPath(excelPath);
                byte[] fixtureBytes = await File.ReadAllBytesAsync(savePath); string fixtureHash = Hash(fixtureBytes);
                string saves = Path.Combine(root, "saves"); Directory.CreateDirectory(saves);
                var core = new EditorService(Path.Combine(root, "state"), () => false, saves);
                var profile = NativeEditorBridge.Object(core.AddProfile(new(excelPath, null, "Private native QA")));
                string profileId = profile["id"]!.GetValue<string>();
                var inspected = NativeEditorBridge.Object(core.Open(new(savePath, profileId)));
                var detectorFalsePreview = NativeEditorBridge.Object(core.Preview(new PreviewRequest { SessionId = inspected["sessionId"]!.GetValue<string>() }));
                Check(!inspected["gameRunning"]!.GetValue<bool>() && detectorFalsePreview["canSave"]!.GetValue<bool>() && !HasRunningWarning(detectorFalsePreview), "injected-closed-game-state-is-consistent-in-summary-and-preview");
                var runningCore = new EditorService(Path.Combine(root, "state-running"), () => true, saves);
                var runningProfile = NativeEditorBridge.Object(runningCore.AddProfile(new(excelPath, null, "Private running-state QA")));
                var runningSummary = NativeEditorBridge.Object(runningCore.Open(new(savePath, runningProfile["id"]!.GetValue<string>())));
                var detectorTruePreview = NativeEditorBridge.Object(runningCore.Preview(new PreviewRequest { SessionId = runningSummary["sessionId"]!.GetValue<string>() }));
                Check(runningSummary["gameRunning"]!.GetValue<bool>() && detectorTruePreview["canSave"]!.GetValue<bool>() && HasRunningWarning(detectorTruePreview), "injected-open-game-state-is-consistent-in-summary-and-preview");
                string name = inspected["character"]!["name"]!.GetValue<string>();
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..") throw new InvalidDataException("Invalid fixture character name.");
                string copy = Path.Combine(saves, name + ".d2s"); await File.WriteAllBytesAsync(copy, fixtureBytes);
                var runningVm = new MainViewModel(core: runningCore, fileDialogs: new NoDialogs());
                await runningVm.InitializeAsync(); Healthy(runningVm, "running-state initialize");
                await runningVm.OpenPathAsync(copy); Healthy(runningVm, "running-state open");
                await runningVm.ApplyDraftAsync(new PreviewRequest { Attributes = new() { ["gold"] = Gold(runningVm.Snapshot) + 1 } }); Healthy(runningVm, "running-state edit");
                await runningVm.PreviewAsync(true); Healthy(runningVm, "running-state review");
                Check(runningVm.GameRunning && !runningVm.CanCommit && !runningVm.SaveCommand.CanExecute(null) && runningVm.ValidationMessage.Contains("Cierra Diablo II", StringComparison.Ordinal), "native-review-explains-open-game-save-guard");
                var vm = new MainViewModel(core: core, fileDialogs: new NoDialogs());
                await vm.InitializeAsync(); Healthy(vm, "fixture initialize");
                vm.SelectedProfile = vm.Profiles.First(p => p.Id == profileId);
                await vm.OpenPathAsync(copy); Healthy(vm, "fixture open");
                Check(vm.HasCharacter && !vm.CanCommit, "private-fixture-open-is-read-only");
                Check(Hash(await File.ReadAllBytesAsync(copy)) == fixtureHash, "opening-keeps-source-bytes");
                long gold = Gold(vm.Snapshot), xp = Experience(vm.Snapshot);
                await vm.ApplyDraftAsync(new PreviewRequest { Attributes = new() { ["gold"] = gold + 1 } }); Healthy(vm, "edit gold");
                Check(vm.IsDirty && !vm.CanCommit && Gold(vm.Snapshot) == gold + 1, "draft-is-valid-but-unreviewed");
                Check(Hash(await File.ReadAllBytesAsync(copy)) == fixtureHash, "editing-keeps-source-bytes");
                await vm.PreviewAsync(true); Healthy(vm, "review");
                Check(vm.CanCommit && vm.SaveCommand.CanExecute(null), "review-enables-native-save-command");
                await vm.UndoAsync(); Healthy(vm, "undo");
                Check(Gold(vm.Snapshot) == gold && !vm.CanCommit, "undo-restores-draft-and-invalidates-review");
                await vm.RedoAsync(); Healthy(vm, "redo");
                Check(Gold(vm.Snapshot) == gold + 1 && !vm.CanCommit, "redo-requires-new-review");
                await vm.PreviewAsync(true); Healthy(vm, "rereview");
                string exportDir = Path.Combine(root, "export"); Directory.CreateDirectory(exportDir);
                string exported = Path.Combine(exportDir, name + ".d2s");
                await vm.ExportAsync(exported); Healthy(vm, "export");
                Check(File.Exists(exported) && Hash(await File.ReadAllBytesAsync(copy)) == fixtureHash, "native-export-creates-copy-only");
                await vm.SaveAsync(); Healthy(vm, "commit");
                Check(Hash(await File.ReadAllBytesAsync(copy)) != fixtureHash && !vm.CanCommit && !vm.IsDirty, "native-save-updates-private-copy-and-clears-draft");
                Check(Experience(vm.Snapshot) == xp, "native-save-preserves-intermediate-xp");
                await vm.RefreshBackupsAsync(); Healthy(vm, "backups");
                var backup = vm.Backups.FirstOrDefault() ?? throw new InvalidOperationException("Native save did not list a backup.");
                Check(Hash(await File.ReadAllBytesAsync(backup.Path)) == fixtureHash, "native-backup-matches-original");
                await vm.RestoreBackupAsync(backup.Id); Healthy(vm, "restore");
                Check(Hash(await File.ReadAllBytesAsync(copy)) == fixtureHash, "native-restore-is-byte-exact");
                vm.GoldText = (gold + 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await vm.CloneCharacterAsync("NativeClone"); Healthy(vm, "clone");
                Check(vm.IsNew && vm.CharacterName == "NativeClone" && !vm.CanCommit, "native-clone-is-independent-unreviewed-draft");
                Check(Gold(vm.Snapshot) == gold + 2 && Hash(await File.ReadAllBytesAsync(copy)) == fixtureHash, "native-clone-includes-raw-unreviewed-edits-without-source-write");
                await vm.CreateCharacterAsync(new CreateRequest { Name = "NativeBuild", ClassName = "Sorceress", ProfileId = profileId, Preset = "squad-sorceress" }); Healthy(vm, "new preset");
                Check(vm.IsNew && vm.IsDirty && vm.ValidationMessage.Contains("nuevo", StringComparison.Ordinal) && vm.ValidationMessage.Contains("revisión", StringComparison.Ordinal), "native-new-character-message-requires-review-and-save");
                Check(vm.Snapshot["character"]!["level"]!.GetValue<int>() == 85 && vm.Skills.Sum(s => s.Points) == 96, "native-create-applies-class-build");
                Check(vm.Items.Count(i => i.Page == "equipped") == 12 && vm.MercenaryItems.Count == 9, "native-both-sets-and-full-mercenary");
                var oculus = vm.Items.Single(i => i.Name.Contains("Oculus", StringComparison.OrdinalIgnoreCase));
                Check(HasSameArtInStorage(oculus, "weapon", "orbStaff"), "native-item-art-oculus-stays-orb-when-stashed");
                string artSession = vm.Snapshot["sessionId"]!.GetValue<string>();
                var eschutaUnique = CatalogRows(core, artSession, "unique", "Eschuta").Single(row => row["name"]!.GetValue<string>().Contains("Eschuta", StringComparison.OrdinalIgnoreCase));
                string eschutaCode = eschutaUnique["code"]!.GetValue<string>();
                var eschutaBase = (JsonObject)CatalogRows(core, artSession, "base", eschutaCode).Single(row => row["code"]!.GetValue<string>() == eschutaCode).DeepClone();
                eschutaBase["name"] = eschutaUnique["name"]!.GetValue<string>();
                Check(HasSameArtInStorage(new ItemViewModel(eschutaBase), "weapon", "orbStaff"), "native-item-art-eschuta-real-base-stays-orb-when-stashed");
                var realArtCases = new[]
                {
                    (Item: vm.Items.Single(i => i.Page == "equipped" && i.Slot == "armor"), Slot: "armor", Art: "purpleArmor"),
                    (Item: vm.Items.Single(i => i.Page == "equipped" && i.Slot == "helm"), Slot: "helm", Art: "hood"),
                    (Item: vm.Items.Single(i => i.Page == "equipped" && i.Slot == "weapon" && i.Code == "crs"), Slot: "weapon", Art: "sword"),
                    (Item: vm.Items.Single(i => i.Page == "equipped" && i.Slot == "belt"), Slot: "belt", Art: "belt")
                };
                Check(realArtCases.All(test => HasSameArtInStorage(test.Item, test.Slot, test.Art)), "native-item-art-real-equipment-keeps-category-in-storage");
                var ringMail = new ItemViewModel((JsonObject)CatalogRows(core, artSession, "base", "rng").Single(row => row["code"]!.GetValue<string>() == "rng").DeepClone());
                Check(HasSameArtInStorage(ringMail, "armor", "steelArmor") && ItemArt.Category("rng", "", "Ring Mail", []) == "unknown" && ItemArt.Category("zzz", "", "Unknown", []) == "unknown" && ItemArt.Get("unknown") is System.Windows.Media.DrawingImage, "native-item-art-ring-mail-and-unknown-are-not-runes-or-charms");
                var selectedEquipment = vm.Items.First(i => i.Page == "equipped" && i.Slot == "armor");
                var doll = new PaperDoll { ItemsSource = vm.EquipmentSlots };
                BindingOperations.SetBinding(doll, PaperDoll.SelectedItemProperty, new Binding(nameof(MainViewModel.SelectedItem)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                doll.SelectItem(selectedEquipment);
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Check(vm.SelectedItem?.Id == selectedEquipment.Id && BindingOperations.IsDataBound(doll, PaperDoll.SelectedItemProperty), "native-equipment-selection-updates-inspector-and-preserves-binding");
                var selectedInventory = vm.Items.First(i => i.Page == "inventory");
                var board = new InventoryBoard { ItemsSource = vm.Items.Where(i => i.Page == "inventory") };
                BindingOperations.SetBinding(board, InventoryBoard.SelectedItemProperty, new Binding(nameof(MainViewModel.SelectedItem)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                board.SelectItem(selectedInventory);
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Check(vm.SelectedItem?.Id == selectedInventory.Id && doll.SelectedItem?.Id == selectedInventory.Id && BindingOperations.IsDataBound(board, InventoryBoard.SelectedItemProperty), "native-inventory-selection-updates-inspector-and-preserves-binding");
                doll.SelectItem(selectedEquipment);
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Check(vm.SelectedItem?.Id == selectedEquipment.Id && board.SelectedItem?.Id == selectedEquipment.Id, "native-item-selection-remains-two-way-after-repeated-changes");
                var skill = vm.Skills.First(s => s.Points > 1); int skillId = skill.Id, skillPoints = skill.Points;
                await ((NativeCommand)vm.DecreaseSkillCommand).ExecuteAsync(skill);
                await vm.PreviewAsync(false); Healthy(vm, "decrease skill");
                Check(SkillPoints(vm.Snapshot, skillId) == skillPoints - 1, "native-skill-command-updates-validated-snapshot");
                await ((NativeCommand)vm.IncreaseSkillCommand).ExecuteAsync(vm.Skills.Single(s => s.Id == skillId));
                await vm.PreviewAsync(false); Healthy(vm, "increase skill");
                Check(SkillPoints(vm.Snapshot, skillId) == skillPoints, "native-skill-increment-updates-validated-snapshot");
                await vm.UndoAsync(); Healthy(vm, "undo skill");
                Check(vm.Skills.Single(s => s.Id == skillId).Points == skillPoints - 1 && SkillPoints(vm.Snapshot, skillId) == skillPoints - 1, "native-skill-undo-restores-controls-and-snapshot");
                await vm.RedoAsync(); Healthy(vm, "redo skill");
                Check(vm.Skills.Single(s => s.Id == skillId).Points == skillPoints && SkillPoints(vm.Snapshot, skillId) == skillPoints, "native-skill-redo-restores-controls-and-snapshot");
                await vm.ResetAsync(); Healthy(vm, "reset skill draft");
                vm.SelectedWeaponSet = 2;
                Check(vm.Equipped.Any(i => i.Slot == "weapon" && i.Code == "crs") && vm.ActiveWeaponSet == 1, "viewing-alternate-set-does-not-activate-it");
                int itemCount = vm.Items.Count; var weapon = vm.Items.First(i => i.Page == "equipped" && i.Slot == "weapon");
                await vm.CopyItemAsync(weapon); Healthy(vm, "copy item");
                Check(vm.Items.Count == itemCount + 1 && vm.Items.Any(i => i.Id.StartsWith("desktop-", StringComparison.Ordinal)), "native-item-copy-has-stable-client-id");
                var copyItem = vm.Items.Single(i => i.Id.StartsWith("desktop-", StringComparison.Ordinal));
                await vm.DeleteItemAsync(copyItem); Healthy(vm, "delete copy");
                Check(vm.Items.Count == itemCount && vm.Items.Any(i => i.Id == weapon.Id), "native-delete-keeps-original-item");
                await vm.UndoAsync(); Healthy(vm, "undo delete");
                Check(vm.Items.Any(i => i.Id == copyItem.Id), "native-item-undo-retains-client-id");
                await vm.ResetAsync(); Healthy(vm, "reset copied items");
                var design = new ItemDesignRequest { BaseCode = "amu", Quality = "crafted", Level = 85, RarePrefixId = 1, RareSuffixId = 1, PersonalizedName = "NativeQA", Stats = [new(127, Value: 2), new(105, Value: 20), new(39, Value: 30), new(41, Value: 30), new(43, Value: 30), new(45, Value: 30)] };
                await vm.ApplyDesignAsync(design); Healthy(vm, "item designer");
                var crafted = vm.Items.Single(i => i.PersonalizedName == "NativeQA");
                Check(crafted.Stats.Count == 6 && crafted.Page == "stash", "native-workshop-six-properties-and-signature");
                vm.SelectedItem = crafted; string blueprint = Path.Combine(root, "native.akara-item.json");
                await vm.ExportBlueprintAsync(blueprint); Healthy(vm, "blueprint export");
                Check(File.Exists(blueprint), "native-blueprint-export");
                await vm.ImportBlueprintAsync(blueprint); Healthy(vm, "blueprint import");
                Check(vm.Items.Count(i => i.PersonalizedName == "NativeQA") == 2, "native-blueprint-import-preserves-properties");
                await vm.ResetAsync(); Healthy(vm, "reset blueprint draft");
                await vm.SetWorldAsync(new WorldChange { Waypoints = [new("hell", 1, false)] }); Healthy(vm, "world waypoint");
                Check(vm.WorldWaypoints.Any(w => w.Difficulty == "hell" && w.Id == 1 && !w.Active), "native-world-waypoint-change");
                await vm.ResetAsync(); Healthy(vm, "reset world");
                await vm.SaveLoadoutAsync("Native QA"); Healthy(vm, "save loadout");
                var library = vm.Loadouts.Single(l => l.Name == "Native QA");
                Check(library.ItemCount > 20, "native-library-captures-full-build");
                vm.LoadoutKeepPrevious = false;
                await vm.ApplyLoadoutAsync(library.Id); Healthy(vm, "apply loadout");
                Check(vm.Items.Count(i => i.Page == "equipped") == 12 && vm.MercenaryItems.Count == 9 && !vm.CanCommit, "native-library-prepares-reviewed-save-flow");
                await vm.DeleteLoadoutAsync(library.Id); Healthy(vm, "delete test library");
                Check(vm.Loadouts.All(l => l.Id != library.Id), "native-library-removes-only-test-document");
                Check(Hash(await File.ReadAllBytesAsync(savePath)) == fixtureHash, "original-fixture-never-written");
            }
        }
        catch (Exception exception) { failures.Add(exception.ToString()); }
        string fullReport = Path.GetFullPath(reportPath); Directory.CreateDirectory(Path.GetDirectoryName(fullReport)!);
        var report = new { schemaVersion = 1, framework = typeof(Application).Assembly.GetName().Name, native = true, browser = false, visibleWindowCreated = false, passed = checks.Count, failed = failures.Count, skipped = omissions.Count, checks, failures, omissions, evidenceDirectory = root };
        await File.WriteAllTextAsync(fullReport, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return failures.Count == 0 ? 0 : 1;
    }
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static long Gold(JsonObject snapshot) => snapshot["character"]!["attributes"]!["gold"]!.GetValue<long>();
    static long Experience(JsonObject snapshot) => snapshot["character"]!["experience"]!.GetValue<long>();
    static int SkillPoints(JsonObject snapshot, int id) => snapshot["skills"]!.AsArray().OfType<JsonObject>().Single(skill => skill["id"]!.GetValue<int>() == id)["points"]!.GetValue<int>();
    static bool HasRunningWarning(JsonObject preview) => preview["validation"]!["warnings"]!.AsArray().Any(w => w?.GetValue<string>().Contains("Diablo II está abierto", StringComparison.Ordinal) == true);
    static IEnumerable<JsonObject> CatalogRows(EditorService core, string sessionId, string type, string query) => NativeEditorBridge.Object(core.Catalog(sessionId, type, query, 0, 250))["entries"]!.AsArray().OfType<JsonObject>();
    static bool HasSameArtInStorage(ItemViewModel source, string slot, string expected)
    {
        ItemViewModel At(string page, string occupiedSlot)
        {
            var data = (JsonObject)source.Data.DeepClone(); data["page"] = page; data["slot"] = occupiedSlot;
            return new ItemViewModel(data);
        }
        var equipped = At("equipped", slot); var stored = At("stash", "");
        return ItemArt.Category(equipped) == expected && ItemArt.Category(stored) == expected && ReferenceEquals(ItemArt.Get(equipped), ItemArt.Get(stored));
    }
    sealed class NoDialogs : IFileDialogs
    {
        public string? OpenFile(string title, string filter) => throw new InvalidOperationException("Unexpected OS file dialog in a native test.");
        public string? SaveFile(string title, string fileName, string filter) => throw new InvalidOperationException("Unexpected OS save dialog in a native test.");
        public string? ChooseFolder(string title) => throw new InvalidOperationException("Unexpected OS folder dialog in a native test.");
        public bool Confirm(string title, string message) => true;
    }
}
