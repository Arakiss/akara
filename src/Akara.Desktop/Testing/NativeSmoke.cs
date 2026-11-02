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
            Check(resourceNames.All(n => !n.Contains("items-atlas", StringComparison.OrdinalIgnoreCase)), "generated-item-atlas-not-embedded");
            Check(resourceNames.All(n => !n.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".sprite", StringComparison.OrdinalIgnoreCase)), "local-game-fonts-and-sprites-not-embedded");
            Check(RejectsTruncatedSprite(), "local-sprite-decoder-rejects-truncated-header-and-payload");
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name is { } name && (name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || name.Contains("WebView2", StringComparison.Ordinal))), "no-web-host-or-webview-assembly");
            var demo = new MainViewModel(dataRoot: Path.Combine(root, "demo"), fileDialogs: new NoDialogs());
            await demo.InitializeAsync(); Healthy(demo, "initialize");
            Check(!demo.CanCommit && !demo.SaveCommand.CanExecute(null), "save-disabled-before-open-and-review");
            await ((NativeCommand)demo.SelectViewCommand).ExecuteAsync("skills");
            Check(demo.SelectedView == "skills", "native-navigation-command");
            Check(demo.NavItems.Select(n => n.Key).Distinct().Count() == demo.NavItems.Count && demo.NavItems.Count >= 7, "native-navigation-model");
            await demo.CreateDemoAsync(); Healthy(demo, "demo");
            Check(demo.Snapshot["character"] is JsonObject character && character["name"]?.GetValue<string>() is { Length: > 0 }, "native-demo-populates-viewmodel");
            Check(!demo.CanCommit && (!demo.IsSyntheticDemo || !demo.SaveCommand.CanExecute(null)), "demo-never-commits-before-review");

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
                var runningVm = new MainViewModel(dataRoot: Path.Combine(root, "fixture-ui"), core: runningCore, fileDialogs: new NoDialogs());
                await runningVm.InitializeAsync(); Healthy(runningVm, "running-state initialize");
                await runningVm.OpenPathAsync(copy); Healthy(runningVm, "running-state open");
                await runningVm.ApplyDraftAsync(new PreviewRequest { Attributes = new() { ["gold"] = Gold(runningVm.Snapshot) + 1 } }); Healthy(runningVm, "running-state edit");
                await runningVm.PreviewAsync(true); Healthy(runningVm, "running-state review");
                Check(runningVm.GameRunning && !runningVm.CanCommit && !runningVm.SaveCommand.CanExecute(null) && runningVm.ValidationMessage.Contains("Cierra Diablo II", StringComparison.Ordinal), "native-review-explains-open-game-save-guard");
                var vm = new MainViewModel(dataRoot: Path.Combine(root, "fixture-ui"), core: core, fileDialogs: new NoDialogs());
                await vm.InitializeAsync(); Healthy(vm, "fixture initialize");
                vm.SelectedProfile = vm.Profiles.First(p => p.Id == profileId);
                await vm.OpenPathAsync(copy); Healthy(vm, "fixture open");
                Check(vm.HasCharacter && !vm.CanCommit, "private-fixture-open-is-read-only");
                await WorkbenchChecks.EquipmentAsync(vm,checks); Healthy(vm,"production workbench");
                await SkillEditingChecks.RunAsync(vm,root,checks); Healthy(vm,"skill input");
                await ItemEditingChecks.RunAsync(vm,root,checks); Healthy(vm,"item input");
                Check(Hash(await File.ReadAllBytesAsync(copy)) == fixtureHash, "opening-keeps-source-bytes");
                await CheckOriginalGameArtAsync(vm, excelPath, root, checks, omissions);
                await CheckItemHoverAsync(vm, copy, fixtureHash, root, checks);
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
                await SaveOpeningChecks.RunAsync(fixtureBytes, excelPath, root, checks);
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
    static async Task CheckItemHoverAsync(MainViewModel vm, string sourcePath, string sourceHash, string evidenceRoot, List<string> checks)
    {
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        string snapshotBefore = vm.Snapshot.ToJsonString(); bool dirtyBefore = vm.IsDirty;
        var all = vm.Items.Concat(vm.MercenaryItems).ToArray();
        var candidates = new[]
        {
            all.OrderByDescending(item => item.VisibleStats.Count + item.Sockets.Sum(socket => socket?.VisibleStats.Count ?? 0)).First(),
            all.OrderByDescending(item => item.VisibleStats.Select(stat => stat.Text.Length).DefaultIfEmpty(0).Max()).First(),
            all.OrderByDescending(item => item.Name.Length).First(),
            all.OrderByDescending(item => item.Ethereal).ThenByDescending(item => item.Sockets.Count).First()
        }.DistinctBy(item => item.Id).ToArray();
        foreach (var item in candidates)
        {
            var card = new ItemTooltipCard(item); LayoutCard(card, 320);
            var text = string.Join("\n", Descendants<System.Windows.Controls.TextBlock>(card).Select(line => line.Text));
            Require(ReferenceEquals(card.Item, item) && text.Contains(item.Name, StringComparison.Ordinal) && text.Contains(item.BaseName, StringComparison.Ordinal) && text.Contains(item.QualityLabel, StringComparison.Ordinal) && text.Contains("Nivel de objeto " + item.Level, StringComparison.Ordinal), "Hover identity differs from real item: " + item.Code);
            Require(!item.Ethereal || text.Contains("Etéreo", StringComparison.Ordinal), "Hover lost ethereal state: " + item.Code);
            if (item.Sockets.Count > 0)
            {
                string count = "Engarces: " + item.Sockets.Count(socket => socket is not null) + " / " + item.Sockets.Count;
                Require(text.Contains(count, StringComparison.Ordinal) && item.Sockets.OfType<ItemViewModel>().All(socket => text.Contains(socket.Name, StringComparison.Ordinal)), "Hover lost native socket count or content: " + item.Code);
            }
            var expectedStats = item.VisibleStats.Concat(item.Sockets.OfType<ItemViewModel>().SelectMany(socket => socket.VisibleStats)).Concat(item.Data.Array("setBonusLists").OfType<JsonArray>().SelectMany(bonus => bonus.OfType<JsonObject>()).Select(TooltipStat).Where(stat => !stat.Hidden)).ToArray();
            var propertyLines = Descendants<System.Windows.Controls.TextBlock>(card.PropertiesViewport).ToArray();
            foreach (var group in expectedStats.GroupBy(stat => stat.Text))
                Require(propertyLines.Count(line => line.Text == group.Key) >= group.Count(), "Hover truncated a real property: " + item.Code + " / " + group.Key);
            foreach (var line in propertyLines.Where(line => line.Text.Length >= 75))
                Require(line.ActualHeight > line.LineHeight + .5, "Long hover property did not wrap: " + item.Code);
            card.PropertiesViewport.ScrollToEnd(); card.UpdateLayout();
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); card.UpdateLayout();
            var tail = Descendants<System.Windows.Controls.TextBlock>(card.PropertiesViewport).Last(line => line.Text == item.LocationLabel);
            double top = tail.TransformToAncestor(card.PropertiesViewport).Transform(new Point()).Y;
            Require(card.PropertiesViewport.ViewportHeight > 0 && top >= -1 && top + tail.ActualHeight <= card.PropertiesViewport.ViewportHeight + 2, "Last hover content cannot be reached by scrolling: " + item.Code);
        }
        foreach (var (needle, fileName) in new[] { ("Oculus", "hover-oculus.png"), ("Tal Rasha", "hover-tal.png") })
        {
            var item = all.FirstOrDefault(candidate => candidate.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
            if (item is null) continue;
            var card = new ItemTooltipCard(item); LayoutCard(card, 620);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(card.ActualWidth), (int)Math.Ceiling(card.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(card); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(evidenceRoot, fileName)); encoder.Save(stream);
        }
        checks.Add("native-hover-card-preserves-real-metadata-properties-and-scroll-reach");

        var inventory = vm.Items.First(item => item.Page == "inventory");
        var entries = new System.Collections.ObjectModel.ObservableCollection<ItemViewModel>(new[] { inventory });
        var board = new InventoryBoard { ItemsSource = entries, Columns = vm.GridColumns, Rows = vm.GridRows, CellSize = 32 };
        var equipment = new System.Collections.ObjectModel.ObservableCollection<EquipmentSlotViewModel>(vm.EquipmentSlots);
        var doll = new PaperDoll { ItemsSource = equipment, Width = 440, Height = 500 };
        var selectedBefore = vm.SelectedItem;
        BindingOperations.SetBinding(board, InventoryBoard.SelectedItemProperty, new Binding(nameof(MainViewModel.SelectedItem)) { Source = vm, Mode = BindingMode.TwoWay });
        BindingOperations.SetBinding(doll, PaperDoll.SelectedItemProperty, new Binding(nameof(MainViewModel.SelectedItem)) { Source = vm, Mode = BindingMode.TwoWay });
        board.Measure(new Size(board.Columns * 32, board.Rows * 32)); board.Arrange(new Rect(0, 0, board.Columns * 32, board.Rows * 32)); board.UpdateLayout();
        doll.Measure(new Size(440, 500)); doll.Arrange(new Rect(0, 0, 440, 500)); doll.UpdateLayout();
        bool Closed(ItemHover hover) => hover.Card is null && hover.CurrentItem is null && !hover.IsPending && !hover.IsOpen;
        try
        {
            var occupied = new Point((inventory.GridX + .5) * 32, (inventory.GridY + .5) * 32);
            var hit = board.HitItem(occupied); Require(ReferenceEquals(hit, inventory), "Hover hit test missed a real inventory item.");
            board.Hover.Track(hit); board.Hover.CompletePending();
            Require(ReferenceEquals(board.Hover.Card?.Item, inventory) && !board.Hover.IsOpen, "Detached hover did not prepare the real card safely.");
            var equipped = equipment.Select(slot => slot.Item).OfType<ItemViewModel>().First();
            doll.Hover.Track(equipped); doll.Hover.CompletePending();
            Require(ReferenceEquals(doll.Hover.Card?.Item, equipped) && ReferenceEquals(vm.SelectedItem, selectedBefore) && !doll.Hover.IsOpen, "Hover changed the selected item or opened a native window.");
            var empty = Enumerable.Range(0, board.Rows).SelectMany(y => Enumerable.Range(0, board.Columns).Select(x => new Point((x + .5) * 32, (y + .5) * 32))).First(point => board.HitItem(point) is null);
            board.Hover.Track(board.HitItem(empty)); board.Hover.CompletePending();
            doll.Hover.Track(doll.HitItem(new Point(0, 0))); doll.Hover.CompletePending();
            Require(Closed(board.Hover) && Closed(doll.Hover) && ReferenceEquals(vm.SelectedItem, selectedBefore), "Empty cells retained a hover card or changed selection.");
            checks.Add("native-hover-is-passive-and-empty-surfaces-have-no-card");

            board.Hover.Track(inventory); board.Hover.CompletePending();
            entries[0] = new ItemViewModel((JsonObject)inventory.Data.DeepClone());
            Require(Closed(board.Hover), "Item replacement retained a stale hover card.");
            board.Hover.Track(entries[0]); board.ItemsSource = new[] { inventory };
            Require(Closed(board.Hover), "Container replacement retained pending hover.");
            board.Hover.Track(inventory); board.IsEnabled = false;
            Require(Closed(board.Hover), "Disabling an item surface retained pending hover."); board.IsEnabled = true;
            doll.Hover.Track(equipped); doll.Hover.CompletePending(); equipment.Clear();
            Require(Closed(doll.Hover), "Equipment refresh retained a stale hover card.");
            board.Hover.Track(inventory); board.Hover.CompletePending(); board.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Require(Closed(board.Hover), "Unloading a surface retained its hover card.");
            var row = new System.Windows.Controls.Border();
            ItemHover.SetItem(row, inventory);
            var rowHover = ItemHover.GetController(row) ?? throw new InvalidOperationException("Attached hover did not create a controller.");
            try
            {
                rowHover.Track(ItemHover.GetItem(row)); rowHover.CompletePending();
                Require(ReferenceEquals(rowHover.Card?.Item, inventory) && !rowHover.IsOpen, "Attached hover did not prepare the real row item safely.");
                ItemHover.SetItem(row, new ItemViewModel((JsonObject)inventory.Data.DeepClone()));
                Require(Closed(rowHover), "Rebinding a recycled row retained stale hover.");
                var rebound = ItemHover.GetController(row) ?? throw new InvalidOperationException("Rebound row lost its hover controller.");
                rebound.Track(ItemHover.GetItem(row)); rebound.CompletePending(); ItemHover.SetItem(row, null);
                Require(Closed(rebound), "Clearing a recycled row retained hover.");
            }
            finally { ItemHover.GetController(row)?.Detach(); rowHover.Detach(); }
            board.Hover.Detach(); board.Hover.Track(inventory); board.Hover.CompletePending();
            Require(Closed(board.Hover) && ReferenceEquals(vm.SelectedItem, selectedBefore) && vm.IsDirty == dirtyBefore && vm.Snapshot.ToJsonString() == snapshotBefore && Hash(await File.ReadAllBytesAsync(sourcePath)) == sourceHash, "Hover lifecycle changed the draft, wrote a character, or survived detach.");
            checks.Add("native-hover-closes-on-refresh-disable-unload-and-detach-without-writes");
        }
        finally { board.Hover.Detach(); doll.Hover.Detach(); }
    }
    static void LayoutCard(ItemTooltipCard card, double height)
    {
        card.Measure(new Size(370, height)); card.Arrange(new Rect(0, 0, 370, Math.Min(height, card.DesiredSize.Height))); card.UpdateLayout();
    }
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants<T>(System.Windows.Media.VisualTreeHelper.GetChild(root, index))) yield return child;
    }
    static StatViewModel TooltipStat(JsonObject node) => new(node.Int("id"), node.Text("label", node.Text("name")), node.Int("layer"), node.Long("value"), node.Double("displayValue"), node.Bool("hidden"), node.Text("name"));
    static bool RejectsTruncatedSprite()
    {
        var header = new byte[40];
        System.Text.Encoding.ASCII.GetBytes("SpA1").CopyTo(header, 0);
        BitConverter.GetBytes((ushort)31).CopyTo(header, 4);
        BitConverter.GetBytes((ushort)1).CopyTo(header, 6);
        BitConverter.GetBytes(1u).CopyTo(header, 8);
        BitConverter.GetBytes(1u).CopyTo(header, 12);
        BitConverter.GetBytes(1u).CopyTo(header, 20);
        foreach (var bytes in new[] { Array.Empty<byte>(), header[..39], header })
        {
            try { LocalGameArt.DecodeSprite(bytes, 0); return false; }
            catch (InvalidDataException) { }
        }
        return true;
    }
    static async Task CheckOriginalGameArtAsync(MainViewModel vm, string excelPath, string evidenceRoot, List<string> checks, List<string> omissions)
    {
        static bool HasCasc(string directory) => File.Exists(Path.Combine(directory, ".build.info")) && Directory.Exists(Path.Combine(directory, "Data", "data"));
        string? gameRoot = null;
        for (var directory = new DirectoryInfo(excelPath); directory is not null; directory = directory.Parent)
            if (HasCasc(directory.FullName)) { gameRoot = directory.FullName; break; }
        string defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Diablo II Resurrected");
        if (gameRoot is null && HasCasc(defaultRoot)) gameRoot = defaultRoot;
        if (gameRoot is null) { omissions.Add("original-game-art-local-casc-not-available"); return; }
        var items = vm.Items.Concat(vm.MercenaryItems).ToArray();
        var imported = await LocalGameArt.WarmupAsync(gameRoot, excelPath, items.Select(item => item.Data), Path.Combine(evidenceRoot, "fixture-ui"));
        var groups = new[]
        {
            (Key: "oculus", Needle: "Oculus"), (Key: "eschuta", Needle: "Eschuta"),
            (Key: "harlequin-crest", Needle: "Harlequin Crest"), (Key: "tal-rasha", Needle: "Tal Rasha")
        };
        var results = new List<object>();
        foreach (var group in groups)
        {
            var present = items.Where(item => item.Name.Contains(group.Needle, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (present.Length == 0) { omissions.Add("original-game-art-" + group.Key + "-not-present-in-private-fixture"); continue; }
            var missing = new List<string>();
            foreach (var item in present)
            {
                var image = LocalGameArt.TryGetItem(item) as System.Windows.Media.Imaging.BitmapSource;
                bool visible = image is not null && HasVisiblePixels(image);
                results.Add(new { group = group.Key, item.Name, item.Code, item.Quality, loaded = visible, width = image?.PixelWidth, height = image?.PixelHeight });
                if (!visible) missing.Add(item.Name + " [" + item.Code + "]");
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("Local CASC is installed but critical original art is missing: " + string.Join(", ", missing) + ". Import diagnostics: " + string.Join("; ", imported.Missing));
            checks.Add("original-local-casc-art-loaded-" + group.Key);
        }
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "original-art.json"), JsonSerializer.Serialize(new { gameRoot, excelPath, imported.ItemsLoaded, imported.SkillsLoaded, imported.CacheDirectory, imported.Missing, criticalItems = results }, new JsonSerializerOptions { WriteIndented = true }));
    }
    static bool HasVisiblePixels(System.Windows.Media.Imaging.BitmapSource image)
    {
        if (image.PixelWidth <= 0 || image.PixelHeight <= 0 || image.Format.BitsPerPixel != 32) return false;
        int stride = checked(image.PixelWidth * 4); var pixels = new byte[checked(stride * image.PixelHeight)];
        image.CopyPixels(pixels, stride, 0);
        for (int index = 3; index < pixels.Length; index += 4) if (pixels[index] > 0) return true;
        return false;
    }
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
