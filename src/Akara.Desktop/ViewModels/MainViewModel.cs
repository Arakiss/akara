using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using Akara.Desktop.Services;
using ForjaHoradrica;

namespace Akara.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableModel
{
    static string SaveFilter => L.T("Personajes de Diablo II (*.d2s)|*.d2s");
    static string JsonFilter => L.T("Archivos de AKARA (*.json)|*.json");
    readonly NativeEditorBridge bridge;
    readonly IFileDialogs dialogs;
    readonly List<EditorState> undo = [], redo = [];
    readonly HashSet<string> discoveredSavePaths = new(StringComparer.OrdinalIgnoreCase);
    PreviewRequest draft = new();
    bool loading, busy, dirty, reviewed, validPlan, basicTouched, skillsTouched;
    string planId = "", sessionId = "", error = "", status = L.T("Elige un personaje para empezar."), selectedView = "equipment", selectedStorage = "inventory";
    string characterName = "", levelText = "1", experienceText = "0", strengthText = "0", dexterityText = "0", vitalityText = "0", energyText = "0", goldText = "0", stashGoldText = "0";
    int selectedWeaponSet = 1;
    ItemViewModel? selectedItem;
    SaveEntry? selectedSave;
    ProfileEntry? selectedProfile;
    PresetEntry? selectedPreset;
    CatalogEntry? selectedCatalogEntry;
    LibraryEntry? selectedLoadout;
    BackupViewModel? selectedBackup;

    public event Action? RequestCreate;
    public event Action? RequestClone;
    public event Action<ItemViewModel>? RequestMove;
    public event Action<ItemViewModel?>? RequestDesign;
    public event Action<CatalogEntry>? RequestCatalogDesign;
    public event Action<string>? OutputCreated;

    public MainViewModel(string? dataRoot = null, EditorService? core = null, IFileDialogs? fileDialogs = null)
    {
        bridge = new(dataRoot, core); dialogs = fileDialogs ?? new WindowsFileDialogs(); artworkDataRoot = dataRoot;
        OpenCommand = Command(async parameter => { var path = parameter as string ?? dialogs.OpenFile(L.T("Abrir personaje"), SaveFilter); if (path is not null) await OpenPathAsync(path); });
        OpenSaveCommand = Command(parameter => parameter is SaveEntry save ? OpenSaveAsync(save) : SelectedSave is { } selected ? OpenSaveAsync(selected) : Task.CompletedTask, parameter => parameter is SaveEntry || SelectedSave is not null);
        RefreshCommand = Command(_ => InitializeAsync());
        CreateCommand = Command(async parameter => { if (parameter is CreateRequest request) await CreateCharacterAsync(request); else RequestCreate?.Invoke(); });
        CloneCommand = Command(async parameter => { if (parameter is string name && name.Length > 0) await CloneCharacterAsync(name); else RequestClone?.Invoke(); }, _ => HasCharacter);
        SelectViewCommand = Command(parameter => { SelectedView = parameter is NavEntry entry ? entry.Key : parameter?.ToString() ?? "equipment"; return Task.CompletedTask; });
        SelectItemCommand = Command(parameter => { if (parameter is ItemViewModel item) SelectedItem = item; else if (parameter is EquipmentSlotViewModel slot) SelectedItem = slot.Item; return Task.CompletedTask; });
        SelectStorageCommand = Command(parameter => { SelectedStorage = parameter?.ToString() ?? "inventory"; return Task.CompletedTask; });
        SelectWeaponSetCommand = Command(parameter => { SelectedWeaponSet = int.TryParse(parameter?.ToString(), out int set) && set == 2 ? 2 : 1; return Task.CompletedTask; });
        ActivateWeaponSetCommand = Command(parameter => MutateAndPreviewAsync(r => r.ActiveWeaponSet = int.TryParse(parameter?.ToString(), out int n) ? n : SelectedWeaponSet), _ => HasCharacter);
        PreviewCommand = Command(_ => PreviewAsync(true), _ => HasCharacter);
        SaveCommand = Command(parameter => SaveAsync(parameter as string), _ => CanSave);
        ExportCommand = Command(parameter => ExportAsync(parameter as string), _ => HasCharacter && (IsDirty || IsNew));
        UndoCommand = Command(_ => UndoAsync(), _ => undo.Count > 0);
        RedoCommand = Command(_ => RedoAsync(), _ => redo.Count > 0);
        ResetCommand = Command(_ => ResetAsync(), _ => HasCharacter && IsDirty);
        MoveItemCommand = Command(async parameter => { if (parameter is ItemDestination destination && SelectedItem is not null) await MoveItemAsync(SelectedItem, destination); else if (SelectedItem is not null) RequestMove?.Invoke(SelectedItem); }, _ => SelectedItem is not null);
        MoveToStashCommand = Command(_ => SelectedItem is null ? Task.CompletedTask : MoveItemAsync(SelectedItem, new() { Page = "stash" }), _ => SelectedItem is not null);
        CopyItemCommand = Command(_ => SelectedItem is null ? Task.CompletedTask : CopyItemAsync(SelectedItem), _ => SelectedItem is not null);
        DeleteItemCommand = Command(async _ => { if (SelectedItem is { } item && dialogs.Confirm(L.T("Eliminar del borrador"), L.T("¿Eliminar «") + item.Name + L.T("»? Podrás deshacerlo antes de guardar."))) await DeleteItemAsync(item); }, _ => SelectedItem is not null);
        DesignItemCommand = Command(_ => { RequestDesign?.Invoke(SelectedItem); return Task.CompletedTask; }, _ => HasCharacter);
        NewItemCommand = Command(_ => { RequestDesign?.Invoke(null); return Task.CompletedTask; }, _ => HasCharacter);
        ApplyPresetCommand = Command(_ => SelectedPreset is null ? Task.CompletedTask : ApplyPresetAsync(SelectedPreset.Id), _ => HasCharacter && SelectedPreset is not null);
        IncreaseSkillCommand = Command(parameter => { if (parameter is SkillViewModel skill && skill.Points < skill.MaxPoints) skill.Points++; return Task.CompletedTask; }, _ => HasCharacter);
        DecreaseSkillCommand = Command(parameter => { if (parameter is SkillViewModel skill && skill.Points > 0) skill.Points--; return Task.CompletedTask; }, _ => HasCharacter);
        SearchCatalogCommand = Command(_ => SearchCatalogAsync(), _ => HasCharacter);
        AddCatalogItemCommand = Command(_ => SelectedCatalogEntry is null ? Task.CompletedTask : AddCatalogItemAsync(SelectedCatalogEntry), _ => HasCharacter && SelectedCatalogEntry?.Supported == true);
        ExportBlueprintCommand = Command(parameter => ExportBlueprintAsync(parameter as string), _ => SelectedItem is not null);
        ImportBlueprintCommand = Command(parameter => ImportBlueprintAsync(parameter as string), _ => HasCharacter);
        RefreshLibraryCommand = Command(_ => RefreshLibraryAsync());
        SaveLoadoutCommand = Command(_ => SaveLoadoutAsync(LoadoutName), _ => HasCharacter);
        ExportLoadoutCommand = Command(parameter => ExportLoadoutAsync(parameter as string), _ => HasCharacter);
        ImportLoadoutCommand = Command(parameter => ImportLoadoutAsync(parameter as string), _ => HasCharacter);
        ApplyLoadoutCommand = Command(_ => SelectedLoadout is null ? Task.CompletedTask : ApplyLoadoutAsync(SelectedLoadout.Id), _ => HasCharacter && SelectedLoadout is not null);
        DeleteLoadoutCommand = Command(_ => SelectedLoadout is null ? Task.CompletedTask : DeleteLoadoutAsync(SelectedLoadout.Id), _ => SelectedLoadout is not null);
        RefreshBackupsCommand = Command(_ => RefreshBackupsAsync(), _ => HasCharacter && !IsNew);
        RestoreBackupCommand = Command(_ => SelectedBackup is null ? Task.CompletedTask : RestoreBackupAsync(SelectedBackup.Id), _ => HasCharacter && SelectedBackup is not null);
        UnlockWaypointsCommand = Command(_ => MutateAndPreviewAsync(r => r.UnlockWaypoints = true), _ => HasCharacter);
        UnlockHellCommand = Command(_ => MutateAndPreviewAsync(r => r.UnlockHell = true), _ => HasCharacter);
        FillSuppliesCommand = Command(_ => MutateAndPreviewAsync(r => { r.FillTomes = true; r.FillBelt = true; }), _ => HasCharacter);
    }

    NativeCommand Command(Func<object?, Task> action, Func<object?, bool>? allowed = null) => new(async parameter => { try { await action(parameter); } catch (Exception exception) { ShowError(exception); } }, parameter => !IsBusy && (!IsSyntheticDemo || allowed is null) && (allowed?.Invoke(parameter) ?? true));
    public NativeEditorBridge Bridge => bridge;
    public JsonObject Snapshot { get; private set; } = new();
    public JsonObject SourceSnapshot { get; private set; } = new();
    public PreviewRequest Draft => NativeEditorBridge.Clone(draft);
    public ObservableCollection<NavEntry> NavItems { get; } = Navigation();
    public ObservableCollection<SaveEntry> Saves { get; } = [];
    public ObservableCollection<ProfileEntry> Profiles { get; } = [];
    public ObservableCollection<PresetEntry> Presets { get; } = [];
    public ObservableCollection<PresetEntry> AvailablePresets { get; } = [];
    public ObservableCollection<SkillViewModel> Skills { get; } = [];
    public ObservableCollection<ItemViewModel> Items { get; } = [];
    public ObservableCollection<ItemViewModel> Equipped { get; } = [];
    public ObservableCollection<EquipmentSlotViewModel> EquipmentSlots { get; } = [];
    public ObservableCollection<ItemViewModel> VisibleInventory { get; } = [];
    public ObservableCollection<ItemViewModel> StashItems { get; } = [];
    public ObservableCollection<ItemViewModel> MercenaryItems { get; } = [];
    public ObservableCollection<EquipmentSlotViewModel> MercenarySlots { get; } = [];
    public ObservableCollection<QuestViewModel> WorldQuests { get; } = [];
    public ObservableCollection<WaypointViewModel> WorldWaypoints { get; } = [];
    public ObservableCollection<CatalogEntry> CatalogEntries { get; } = [];
    public ObservableCollection<LibraryEntry> Loadouts { get; } = [];
    public ObservableCollection<BackupViewModel> Backups { get; } = [];
    public ObservableCollection<DiffViewModel> Changes { get; } = [];
    public IReadOnlyList<string> StorageOptions { get; } = ["inventory", "stash", "cube", "belt"];
    public IReadOnlyList<string> QuestStates { get; } = ["notStarted", "started", "completed", "rewarded"];
    public IReadOnlyList<ChoiceEntry> QuestStateChoices => [new("notStarted", L.T("Sin empezar")), new("started", L.T("En curso")), new("completed", L.T("Completada")), new("rewarded", L.T("Recompensa recibida"))];
    public SaveEntry? SelectedSave
    {
        get => selectedSave;
        set
        {
            if (loading) { Set(ref selectedSave, value); return; }
            if (value is null || value == selectedSave) return;
            // Selection represents the character that actually opened. A busy operation,
            // cancelled draft prompt or failed open must not strand the selector on a file
            // that was never accepted, preventing a subsequent attempt with the same entry.
            if (!IsBusy) _ = OpenSaveAsync(value);
            Raise(nameof(SelectedSave));
        }
    }
    public bool HasSaves => Saves.Count > 0;
    public string SavesSummary => Saves.Count == 1 ? L.T("1 personaje local") : Saves.Count + L.T(" personajes locales");
    public string SaveSelectionText => SelectedSave?.Name ?? (HasCharacter ? CharacterName : L.T("Elegir personaje"));
    public ProfileEntry? SelectedProfile { get => selectedProfile; set { Set(ref selectedProfile, value); Raise(nameof(ProfileLabel)); } }
    public PresetEntry? SelectedPreset { get => selectedPreset; set { Set(ref selectedPreset, value); Raise(nameof(SelectedPresetGuide)); Requery(); } }
    public string SelectedPresetGuide => SelectedPreset?.GuideText ?? "";
    public ItemViewModel? SelectedItem { get => selectedItem; set { Set(ref selectedItem, value); Raise(nameof(HasSelectedItem)); Requery(); } }
    public CatalogEntry? SelectedCatalogEntry { get => selectedCatalogEntry; set { Set(ref selectedCatalogEntry, value); Requery(); } }
    public LibraryEntry? SelectedLoadout { get => selectedLoadout; set { Set(ref selectedLoadout, value); Requery(); } }
    public BackupViewModel? SelectedBackup { get => selectedBackup; set { Set(ref selectedBackup, value); Requery(); } }
    public string SelectedView { get => selectedView; set { Set(ref selectedView, value); Raise(nameof(CurrentSection)); } }
    public string CurrentSection { get => SelectedView; set => SelectedView = value; }
    public string SelectedStorage { get => selectedStorage; set { if (Set(ref selectedStorage, value)) RefreshInventory(); foreach (var name in new[] { nameof(StorageLabel), nameof(GridColumns), nameof(GridRows), nameof(StorageColumns), nameof(StorageRows), nameof(GridWidth), nameof(GridHeight) }) Raise(name); } }
    public string StorageLabel => Names.Storage(SelectedStorage);
    public int SelectedWeaponSet { get => selectedWeaponSet; set { if (Set(ref selectedWeaponSet, value is 2 ? 2 : 1)) RefreshInventory(); } }
    public bool IsBusy { get => busy; private set { Set(ref busy, value); UpdateRowLocks(); RaiseSaveState(); Raise(nameof(CanEdit)); Raise(nameof(CanChooseSave)); Raise(nameof(SaveSelectionText)); Raise(nameof(CanChangeLanguage)); Requery(); } }
    public bool CanChooseSave => !IsBusy;
    public bool IsDirty { get => dirty; private set { Set(ref dirty, value); RaiseSaveState(); Raise(nameof(DraftLabel)); Requery(); } }
    public bool IsSyntheticDemo { get; private set; }
    public string DemoLabel => IsSyntheticDemo ? L.T("Vista de diseño · sin partida abierta") : "";
    public bool HasCharacter => IsSyntheticDemo || sessionId.Length > 0;
    public bool CanEdit => HasCharacter && !IsSyntheticDemo && !IsBusy;
    public bool IsCharacterSelected => HasCharacter;
    public bool HasSelectedItem => SelectedItem is not null;
    public bool GameRunning => Snapshot.Bool("gameRunning");
    public bool IsNew => Snapshot.Bool("isNew");
    public bool CanCommit => CanSave && reviewed && validPlan;
    public bool HasErrors => ErrorText.Length > 0 || HasInputErrors;
    public string ErrorText { get => error; private set { Set(ref error, value); Raise(nameof(HasErrors)); Raise(nameof(DisplayErrorText)); } }
    public string ValidationMessage { get; private set; } = L.T("Sin cambios pendientes.");
    public string StatusText { get => status; private set { Set(ref status, value); Raise(nameof(Status)); } }
    public string Status => StatusText;
    public string DraftLabel => IsDirty ? L.T("Borrador sin guardar") : IsNew ? L.T("Personaje nuevo") : L.T("Partida local");
    public string ClassName => Snapshot.Obj("character").Text("className");
    public string ClassLabel => Names.Class(ClassName);
    public string ProfileLabel => SelectedProfile?.Name ?? L.T("Datos locales de Reimagined");
    public string SourcePath => SourceSnapshot.Text("sourcePath");
    public string SourceHash => SourceSnapshot.Text("sourceHash");
    public int ActiveWeaponSet => Snapshot.Int("activeWeaponSet", 1);
    public string BaseLifeText => Number(Snapshot.Obj("character").Obj("vitals").Double("maxLife"));
    public string BaseManaText => Number(Snapshot.Obj("character").Obj("vitals").Double("maxMana"));
    public string SkillPointsText => (Snapshot.Obj("character").Obj("attributes").Long("unspentSkills") + Snapshot.Array("skills").OfType<JsonObject>().Sum(skill => (long)skill.Int("points")) - Skills.Sum(skill => (long)skill.Points)) + L.T(" disponibles");
    public string AttributePointsText => Snapshot.Obj("character").Obj("attributes").Long("unspentStats") + L.T(" disponibles");
    public string MercenaryName => Names.MercenaryType(Snapshot.Obj("mercenary").Text("typeName", L.T("Mercenario")));
    public string MercenaryLevelText => L.T("Nivel ") + Snapshot.Obj("mercenary").Int("level");
    public string MercenaryAura => Names.Aura(Snapshot.Obj("mercenary").Text("aura"));
    public bool HasMercenary => Snapshot.Obj("mercenary").Bool("exists");
    public string CurrentDifficulty => Names.Difficulty(WorldSnapshot.Text("activeDifficulty"));
    public JsonObject WorldSnapshot { get; private set; } = new();
    public int GridColumns => SelectedStorage switch { "stash" => Snapshot.Obj("stashGrid").Int("width", 10), "cube" => Snapshot.Obj("cubeGrid").Int("width", 3), "belt" => Snapshot.Obj("beltGrid").Int("width", 4), _ => Snapshot.Obj("inventoryGrid").Int("width", 10) };
    public int GridRows => SelectedStorage switch { "stash" => Snapshot.Obj("stashGrid").Int("height", 10), "cube" => Snapshot.Obj("cubeGrid").Int("height", 4), "belt" => Snapshot.Obj("beltGrid").Int("height", 4), _ => Snapshot.Obj("inventoryGrid").Int("height", 8) };
    public int StashGridColumns => Snapshot.Obj("stashGrid").Int("width", 10);
    public int StashGridRows => Snapshot.Obj("stashGrid").Int("height", 10);
    public int StorageColumns => GridColumns;
    public int StorageRows => GridRows;
    public double GridWidth => GridColumns * 52;
    public double GridHeight => GridRows * 52;
    public string CharacterName { get => characterName; set => Edit(ref characterName, value); }
    public string LevelText { get => levelText; set => Edit(ref levelText, value); }
    public string ExperienceText { get => experienceText; set => Edit(ref experienceText, value); }
    public string StrengthText { get => strengthText; set => Edit(ref strengthText, value); }
    public string DexterityText { get => dexterityText; set => Edit(ref dexterityText, value); }
    public string VitalityText { get => vitalityText; set => Edit(ref vitalityText, value); }
    public string EnergyText { get => energyText; set => Edit(ref energyText, value); }
    public string GoldText { get => goldText; set => Edit(ref goldText, value); }
    public string StashGoldText { get => stashGoldText; set => Edit(ref stashGoldText, value); }
    public string CatalogType { get; set; } = "unique";
    public string CatalogQuery { get; set; } = "";
    public string LoadoutName { get; set; } = L.T("Mi configuración");
    public bool LoadoutIncludeCharacter { get; set; } = true;
    public bool LoadoutKeepPrevious { get; set; } = true;
    public string LastOutputPath { get; private set; } = "";

    public ICommand OpenCommand { get; } public ICommand OpenSaveCommand { get; } public ICommand RefreshCommand { get; } public ICommand CreateCommand { get; } public ICommand CloneCommand { get; }
    public ICommand SelectViewCommand { get; } public ICommand SelectItemCommand { get; } public ICommand SelectStorageCommand { get; } public ICommand SelectWeaponSetCommand { get; } public ICommand ActivateWeaponSetCommand { get; }
    public ICommand PreviewCommand { get; } public ICommand SaveCommand { get; } public ICommand ExportCommand { get; } public ICommand UndoCommand { get; } public ICommand RedoCommand { get; } public ICommand ResetCommand { get; }
    public ICommand MoveItemCommand { get; } public ICommand MoveToStashCommand { get; } public ICommand CopyItemCommand { get; } public ICommand DeleteItemCommand { get; } public ICommand DesignItemCommand { get; } public ICommand NewItemCommand { get; }
    public ICommand ApplyPresetCommand { get; } public ICommand IncreaseSkillCommand { get; } public ICommand DecreaseSkillCommand { get; } public ICommand SearchCatalogCommand { get; } public ICommand AddCatalogItemCommand { get; }
    public ICommand ExportBlueprintCommand { get; } public ICommand ImportBlueprintCommand { get; } public ICommand RefreshLibraryCommand { get; } public ICommand SaveLoadoutCommand { get; } public ICommand ExportLoadoutCommand { get; } public ICommand ImportLoadoutCommand { get; } public ICommand ApplyLoadoutCommand { get; } public ICommand DeleteLoadoutCommand { get; }
    public ICommand RefreshBackupsCommand { get; } public ICommand RestoreBackupCommand { get; } public ICommand UnlockWaypointsCommand { get; } public ICommand UnlockHellCommand { get; } public ICommand FillSuppliesCommand { get; }

    public Task InitializeAsync() => RunAsync(async () => { await RefreshBootstrapCoreAsync(); await RefreshLibraryCoreAsync(); StatusText = L.T("Personajes locales disponibles."); });
    async Task RefreshBootstrapCoreAsync()
    {
        var boot = await bridge.CallAsync(c => c.Bootstrap());
        loading = true;
        try
        {
            Replace(Profiles, boot.Array("profiles").OfType<JsonObject>().Select(p => new ProfileEntry(p.Text("id"), p.Text("name"), p.Text("excelPath"))));
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == (HasCharacter ? Snapshot.Text("profileId") : boot.Text("defaultProfileId"))) ?? Profiles.FirstOrDefault();
            Replace(Saves, boot.Array("saves").OfType<JsonObject>().Select(s => new SaveEntry(s.Text("name"), s.Text("path"), s.Text("profileId") is { Length: > 0 } id ? id : null)));
            discoveredSavePaths.Clear(); foreach (var save in Saves) discoveredSavePaths.Add(Path.GetFullPath(save.Path));
            SynchronizeSaveSelection();
            if (SelectedProfile is not null) await RefreshPresetsCoreAsync();
        }
        finally { loading = false; }
    }
    async Task RefreshPresetsCoreAsync()
    {
        var result = await bridge.CallAsync(c => c.Presets(SelectedProfile?.Id));
        Replace(Presets, result.Array("entries").OfType<JsonObject>().Select(p => new PresetEntry(p.Text("id"), p.Text("name"), p.Text("description"), p.Int("classId"), p.Text("className"), FormatGuide(p.Obj("guide")))));
        Replace(AvailablePresets, Presets.Where(p => p.ClassName == ClassName));
        SelectedPreset = Presets.FirstOrDefault(p => p.Id == "squad-sorceress" && p.ClassName == ClassName) ?? Presets.FirstOrDefault(p => p.ClassName == ClassName);
    }
    public Task OpenSaveAsync(SaveEntry save) => OpenPathAsync(save.Path, save.ProfileId);
    public Task OpenPathAsync(string path) => OpenPathAsync(path, null);
    Task OpenPathAsync(string path, string? profileId) => RunAsync(async () =>
    {
        if ((IsDirty || HasInputErrors) && !dialogs.Confirm(L.T("Cambiar de personaje"), L.T("Hay un borrador sin guardar. ¿Quieres abrir otra partida y descartarlo?"))) return;
        string fullPath = Path.GetFullPath(path);
        var knownSave = Saves.FirstOrDefault(s => string.Equals(Path.GetFullPath(s.Path), fullPath, StringComparison.OrdinalIgnoreCase));
        string? selectedProfileId = profileId ?? knownSave?.ProfileId ?? SelectedProfile?.Id;
        JsonObject opened;
        try { opened = await bridge.CallAsync(c => c.Open(new(fullPath, selectedProfileId))); }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            string profileName = Profiles.FirstOrDefault(p => p.Id == selectedProfileId)?.Name ?? L.T("seleccionado");
            throw new ApiProblem("save_profile_read", L.T("No se pudo leer «") + Path.GetFileName(fullPath) + L.T("» con el perfil «") + profileName + L.T("». El archivo puede estar incompleto o pertenecer a otra versión del juego o del mod. Para abrirlo se necesitan sus tablas compatibles."), details: new { profileId = selectedProfileId, technicalError = exception.Message });
        }
        await AcceptOpenedAsync(opened); StatusText = L.T("Partida abierta. Los cambios se guardan al revisar y confirmar.");
    });
    public Task RegisterProfileAsync(string excelPath, string? name = null) => RunAsync(async () =>
    { await bridge.CallAsync(c => c.AddProfile(new(excelPath, null, name))); await RefreshBootstrapCoreAsync(); StatusText = L.T("Perfil de datos añadido."); });
    public Task CreateCharacterAsync(CreateRequest request) => RunAsync(async () =>
    {
        if ((IsDirty || HasInputErrors) && !IsSyntheticDemo && !dialogs.Confirm(L.T("Crear otro personaje"), L.T("Hay un borrador sin guardar. ¿Quieres descartarlo para crear otro personaje?"))) return;
        request.ProfileId ??= SelectedProfile?.Id;
        var opened = await bridge.CallAsync(c => c.Create(request)); await AcceptOpenedAsync(opened); IsDirty = true;
        StatusText = L.T("Personaje creado en memoria. Revisa los cambios para guardarlo.");
    });
    public Task CreateDemoAsync() => Profiles.Count > 0 ? CreateCharacterAsync(new() { Name = "Akara-Demo", ClassName = "Sorceress", Preset = "squad-sorceress", ProfileId = SelectedProfile?.Id }) : RunAsync(() => { LoadSyntheticDemo(); return Task.CompletedTask; });
    public Task CreateSyntheticDemoAsync() => RunAsync(() => { LoadSyntheticDemo(); return Task.CompletedTask; });
    public Task CloneCharacterAsync(string name) => RunAsync(async () =>
    {
        if (!HasCharacter) return;
        if (IsDirty) { await PreviewCoreAsync(false); if (!validPlan) return; }
        var opened = await bridge.CallAsync(c => c.Clone(new(sessionId, name, validPlan ? planId : null))); await AcceptOpenedAsync(opened); IsDirty = true;
        StatusText = L.T("Copia preparada en memoria. El personaje original sigue guardado.");
    });
    async Task AcceptOpenedAsync(JsonObject opened)
    {
        IsSyntheticDemo = false; Raise(nameof(IsSyntheticDemo)); Raise(nameof(DemoLabel)); Raise(nameof(CanEdit));
        sessionId = opened.Text("sessionId"); SourceSnapshot = (JsonObject)opened.DeepClone(); draft = new() { SessionId = sessionId };
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == opened.Text("profileId")) ?? SelectedProfile;
        undo.Clear(); redo.Clear(); basicTouched = skillsTouched = reviewed = validPlan = false; planId = ""; ErrorText = ""; IsDirty = opened.Bool("isNew");
        await PrepareArtworkAsync(opened); ApplySnapshot(opened, true); Changes.Clear(); await RefreshPresetsCoreAsync(); await RefreshWorldCoreAsync(); await RefreshBackupsCoreAsync();
        ValidationMessage = PendingValidationText; Raise(nameof(ValidationMessage));
        Raise(nameof(HasCharacter)); Raise(nameof(IsCharacterSelected)); Raise(nameof(SourcePath)); Raise(nameof(SourceHash)); Requery();
        SynchronizeSaveSelection();
    }
    void SynchronizeSaveSelection()
    {
        string path = SourceSnapshot.Text("sourcePath");
        string? fullPath = path.Length > 0 ? Path.GetFullPath(path) : null;
        foreach (var previous in Saves.Where(s => !discoveredSavePaths.Contains(Path.GetFullPath(s.Path)) && !string.Equals(Path.GetFullPath(s.Path), fullPath, StringComparison.OrdinalIgnoreCase)).ToArray()) Saves.Remove(previous);
        var entry = fullPath is null ? null : Saves.FirstOrDefault(s => string.Equals(Path.GetFullPath(s.Path), fullPath, StringComparison.OrdinalIgnoreCase));
        if (entry is null && fullPath is not null)
        {
            entry = new SaveEntry(SourceSnapshot.Obj("character").Text("name", Path.GetFileNameWithoutExtension(fullPath)), fullPath, SourceSnapshot.Text("profileId"));
            Saves.Add(entry);
        }
        selectedSave = entry;
        Raise(nameof(SelectedSave)); Raise(nameof(SaveSelectionText)); Raise(nameof(HasSaves)); Raise(nameof(SavesSummary));
    }
    public Task ApplyDraftAsync(PreviewRequest request) => RunAsync(async () => { Remember(); draft = NativeEditorBridge.Clone(request); draft.SessionId = sessionId; basicTouched = skillsTouched = false; MarkDirty(); await PreviewCoreAsync(false); });
    public Task ApplyPresetAsync(string preset) => MutateAndPreviewAsync(request => { request.Preset = preset; basicTouched = skillsTouched = false; });
    public Task PreviewAsync(bool review = true) => RunAsync(() => PreviewCoreAsync(review));
    async Task PreviewCoreAsync(bool review)
    {
        if (!HasCharacter) return;
        if (HasInputErrors) { StatusText = L.T("Corrige los campos señalados antes de revisar o guardar."); return; }
        var request = BuildRequest();
        var preview = await bridge.CallAsync(c => c.Preview(request));
        if (!preview.Bool("canSave"))
        {
            validPlan = reviewed = false; planId = "";
            ErrorText = string.Join(Environment.NewLine, preview.Obj("validation").Array("errors").Select(n => n?.GetValue<string>()));
            ValidationMessage = L.T("Revisa los valores del borrador."); Raise(nameof(ValidationMessage)); RaiseSaveState(); return;
        }
        validPlan = true; reviewed = review; planId = preview.Text("planId");
        await PrepareArtworkAsync(preview.Obj("summary")); ApplySnapshot(preview.Obj("summary"), true);
        Replace(Changes, preview.Array("diff").OfType<JsonObject>().Select(d => new DiffViewModel(d.Text("section"), d.Text("label"), Describe(d["before"]), Describe(d["after"]))));
        ValidationMessage = review && GameRunning
            ? L.T("Validación correcta. Cierra Diablo II para usar «Guardar con copia». Puedes exportar una copia fuera de Saved Games.")
            : !IsDirty && !IsNew
                ? L.T("Sin cambios pendientes.")
                : review
                    ? L.T("Validación correcta. Puedes guardar con copia de seguridad.")
                    : IsNew
                        ? L.T("Personaje nuevo válido. Revisa los cambios antes de guardarlo.")
                        : L.T("Borrador válido. Revisa los cambios antes de guardar.");
        Raise(nameof(ValidationMessage)); RaiseSaveState(); ErrorText = "";
        await RefreshWorldCoreAsync();
        if (review) { SelectedView = "changes"; StatusText = L.T("Cambios revisados · ") + Changes.Count + L.T(" entradas."); }
    }
    public Task SaveAsync(string? destination = null) => RunAsync(async () =>
    {
        if (HasInputErrors) { StatusText = L.T("Corrige los campos señalados antes de guardar."); return; }
        if (!HasCharacter || IsSyntheticDemo || (!IsDirty && !IsNew)) return;
        if (!reviewed || !validPlan) { await PreviewCoreAsync(true); return; }
        if (GameRunning) { ErrorText = L.T("Cierra Diablo II y revisa los cambios antes de guardar."); return; }
        if (IsNew) { destination ??= dialogs.SaveFile(L.T("Guardar personaje nuevo"), CharacterName + ".d2s", SaveFilter); if (destination is null) return; }
        var result = await bridge.CallAsync(c => c.Commit(new(sessionId, planId, SourceHash, destination)));
        LastOutputPath = result.Text("outputPath"); Raise(nameof(LastOutputPath)); OutputCreated?.Invoke(LastOutputPath);
        await AcceptOpenedAsync(result.Obj("summary")); StatusText = L.T("Partida guardada y verificada") + (result.Text("backupPath").Length > 0 ? L.T(" · copia de seguridad creada.") : ".");
        await RefreshBootstrapCoreAsync();
    });
    public Task ExportAsync(string? path = null) => RunAsync(async () =>
    {
        await PreviewCoreAsync(true); if (!validPlan) return;
        path ??= dialogs.SaveFile(L.T("Exportar una copia"), CharacterName + ".d2s", SaveFilter); if (path is null) return;
        var result = await bridge.CallAsync(c => c.Export(new(sessionId, planId, Path.GetFullPath(path))));
        LastOutputPath = result.Text("outputPath"); Raise(nameof(LastOutputPath)); OutputCreated?.Invoke(LastOutputPath); StatusText = L.T("Copia exportada: ") + LastOutputPath;
        await RefreshBootstrapCoreAsync();
    });
    public Task ResetAsync() => RunAsync(async () => { draft = new() { SessionId = sessionId }; undo.Clear(); redo.Clear(); basicTouched = skillsTouched = reviewed = validPlan = false; IsDirty = IsNew; ApplySnapshot((JsonObject)SourceSnapshot.DeepClone(), true); await RefreshWorldCoreAsync(); ValidationMessage = PendingValidationText; Raise(nameof(ValidationMessage)); StatusText = L.T("Borrador restaurado a la partida abierta."); });
    public Task UndoAsync() => RunAsync(async () => { if (undo.Count == 0) return; redo.Add(Capture()); var state = undo[^1]; undo.RemoveAt(undo.Count - 1); Restore(state); await PreviewCoreAsync(false); StatusText = L.T("Cambio deshecho."); });
    public Task RedoAsync() => RunAsync(async () => { if (redo.Count == 0) return; undo.Add(Capture()); var state = redo[^1]; redo.RemoveAt(redo.Count - 1); Restore(state); await PreviewCoreAsync(false); StatusText = L.T("Cambio rehecho."); });
    public Task MoveItemAsync(ItemViewModel item, ItemDestination destination) => MutateAndPreviewAsync(r => (r.ItemOperations ??= []).Add(new() { Operation = "move", ItemId = item.Id, Destination = destination }));
    public Task CopyItemAsync(ItemViewModel item) => MutateAndPreviewAsync(r => (r.ItemOperations ??= []).Add(new() { Operation = "copy", ItemId = item.Id, ClientId = "desktop-" + Guid.NewGuid().ToString("N"), Destination = new() { Page = "stash" } }));
    public Task DeleteItemAsync(ItemViewModel item) => MutateAndPreviewAsync(r => (r.ItemOperations ??= []).Add(new() { Operation = "delete", ItemId = item.Id }));
    public Task ApplyDesignAsync(ItemDesignRequest design, ItemViewModel? item = null, ItemDestination? destination = null) => MutateAndPreviewAsync(r => (r.ItemOperations ??= []).Add(new() { Operation = item is null ? "create" : "edit", ItemId = item?.Id ?? "", ClientId = item is null ? "desktop-" + Guid.NewGuid().ToString("N") : null, Design = design, Destination = destination }));
    public Task SetMercenaryAsync(int typeId, int level, bool resurrect = false) => MutateAndPreviewAsync(r => r.Mercenary = new(typeId, level, resurrect));
    public Task SetWorldAsync(WorldChange world) => MutateAndPreviewAsync(r => r.World = world);
    public Task SearchCatalogAsync(string? type = null, string? query = null) => RunAsync(async () =>
    {
        CatalogType = type ?? CatalogType; CatalogQuery = query ?? CatalogQuery;
        var entries = await GetCatalogAsync(CatalogType, CatalogQuery, allPages: true);
        Replace(CatalogEntries, entries);
        StatusText = CatalogEntries.Count + L.T(" objetos en el catálogo local.");
    });
    public async Task AddCatalogItemAsync(CatalogEntry item, ItemDestination? destination = null)
    {
        if (item.Type == "runeword")
        {
            if (!HasCharacter || IsBusy || IsSyntheticDemo) return;
            RequestCatalogDesign?.Invoke(item); return;
        }
        await MutateAndPreviewAsync(r =>
        {
        if (!item.Supported) throw new ApiProblem("catalog_unsupported", L.T("Las tablas locales no permiten generar este objeto."));
        (r.ItemOperations ??= []).Add(new() { Operation = "create", ClientId = "desktop-" + Guid.NewGuid().ToString("N"), Gear = new() { Type = item.Type, Name = item.Name, BaseCode = item.Code }, Destination = destination ?? new() { Page = "stash" } });
        });
    }
    public Task ExportBlueprintAsync(string? path = null) => RunAsync(async () =>
    {
        if (SelectedItem is null) return; var id = SelectedItem.Id; await PreviewCoreAsync(false); if (!validPlan) return;
        var document = await bridge.CallAsync(c => c.ExportBlueprint(sessionId, id, planId));
        path ??= dialogs.SaveFile(L.T("Exportar objeto"), "objeto.akara-item.json", JsonFilter); if (path is null) return;
        WriteNew(path, document); StatusText = L.T("Objeto exportado."); LastOutputPath = path; Raise(nameof(LastOutputPath));
    });
    public Task ImportBlueprintAsync(string? path = null) => RunAsync(async () =>
    {
        path ??= dialogs.OpenFile(L.T("Importar objeto"), JsonFilter); if (path is null) return;
        var document = ReadDocument<ItemBlueprintDocument>(path); Remember(); (draft.ItemOperations ??= []).Add(new() { Operation = "create", ClientId = "desktop-" + Guid.NewGuid().ToString("N"), Blueprint = document, Destination = new() { Page = "stash" } }); MarkDirty(); await PreviewCoreAsync(false);
    });
    public Task RefreshLibraryAsync() => RunAsync(RefreshLibraryCoreAsync);
    async Task RefreshLibraryCoreAsync()
    {
        var result = await bridge.CallAsync(c => c.Loadouts());
        Replace(Loadouts, result.Array("entries").OfType<JsonObject>().Select(n => new LibraryEntry(n.Text("id"), n.Text("name"), n.Text("className"), n.NullableInt("level"), n.Int("itemCount"))));
    }
    public Task SaveLoadoutAsync(string name) => RunAsync(async () => { await PreviewCoreAsync(false); if (!validPlan) return; await bridge.CallAsync(c => c.SaveLoadout(new(sessionId, name, planId, LoadoutIncludeCharacter))); await RefreshLibraryCoreAsync(); StatusText = L.T("Configuración guardada en tu biblioteca."); });
    public Task ExportLoadoutAsync(string? path = null) => RunAsync(async () =>
    {
        await PreviewCoreAsync(false); if (!validPlan) return;
        var document = await bridge.CallAsync(c => c.ExportLoadout(sessionId, LoadoutName, planId, LoadoutIncludeCharacter));
        path ??= dialogs.SaveFile(L.T("Exportar configuración"), "build.akara-loadout.json", JsonFilter); if (path is null) return;
        WriteNew(path, document); StatusText = L.T("Configuración exportada."); LastOutputPath = path; Raise(nameof(LastOutputPath));
    });
    public Task ImportLoadoutAsync(string? path = null) => RunAsync(async () => { path ??= dialogs.OpenFile(L.T("Importar configuración"), JsonFilter); if (path is null) return; var document = ReadDocument<LoadoutDocument>(path); await bridge.CallAsync(c => c.ImportLoadout(new(sessionId, document))); await RefreshLibraryCoreAsync(); StatusText = L.T("Configuración añadida a la biblioteca."); });
    public Task ApplyLoadoutAsync(string id) => RunAsync(async () => { var document = await bridge.CallAsync(c => c.GetLoadout(id)); Remember(); draft.Loadout = document; draft.LoadoutIncludeCharacter = LoadoutIncludeCharacter; draft.LoadoutKeepPrevious = LoadoutKeepPrevious; basicTouched = skillsTouched = false; MarkDirty(); await PreviewCoreAsync(false); });
    public Task DeleteLoadoutAsync(string id) => RunAsync(async () => { if (!dialogs.Confirm(L.T("Eliminar configuración"), L.T("¿Eliminar esta configuración de la biblioteca local? Las partidas guardadas seguirán disponibles."))) return; await bridge.CallAsync(c => c.DeleteLoadout(id)); await RefreshLibraryCoreAsync(); StatusText = L.T("Configuración eliminada de la biblioteca."); });
    public Task RefreshBackupsAsync() => RunAsync(RefreshBackupsCoreAsync);
    async Task RefreshBackupsCoreAsync()
    {
        if (!HasCharacter || IsNew) { Backups.Clear(); return; }
        var result = await bridge.CallAsync(c => c.GetBackups(sessionId)); Replace(Backups, result.Array("entries").OfType<JsonObject>().Select(n => new BackupViewModel(n.Text("id"), n.Text("path"), n.Text("hash"), n.Text("createdAt"))));
    }
    public Task RestoreBackupAsync(string id) => RunAsync(async () => { if (!dialogs.Confirm(L.T("Restaurar copia"), L.T("Se restaurará la copia elegida y se conservará otra del estado actual. ¿Continuar?"))) return; var result = await bridge.CallAsync(c => c.Restore(new(sessionId, id, SourceHash))); await AcceptOpenedAsync(result.Obj("summary")); StatusText = L.T("Copia restaurada y verificada."); });

    async Task MutateAndPreviewAsync(Action<PreviewRequest> action) => await RunAsync(async () => { if (!HasCharacter) return; Remember(); action(draft); MarkDirty(); await PreviewCoreAsync(false); });
    async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; ErrorText = "";
        try { await action(); }
        catch (Exception exception) { ShowError(exception); }
        finally { IsBusy = false; Requery(); }
    }
    void ShowError(Exception exception) { ErrorText = exception is ApiProblem problem ? problem.Message : L.T("No se pudo completar la operación: ") + exception.Message; StatusText = L.T("La operación no se ha completado."); reviewed = false; RaiseSaveState(); }
    PreviewRequest BuildRequest()
    {
        if (HasInputErrors) throw new ApiProblem("input_invalid", DisplayErrorText);
        var request = NativeEditorBridge.Clone(draft); request.SessionId = sessionId;
        if (basicTouched)
        {
            request.Name = CharacterName;
            request.Level = checked((int)Parse(LevelText, L.T("nivel")));
            long experience = Parse(ExperienceText, L.T("experiencia"));
            // Level is the primary control. Supplying the unchanged XP would otherwise override a requested level.
            request.Experience = experience != SourceSnapshot.Obj("character").Long("experience") ? experience : null;
            request.Attributes = new() { ["strength"] = Parse(StrengthText, L.T("fuerza")), ["dexterity"] = Parse(DexterityText, L.T("destreza")), ["vitality"] = Parse(VitalityText, L.T("vitalidad")), ["energy"] = Parse(EnergyText, L.T("energía")), ["gold"] = Parse(GoldText, L.T("oro")), ["stashGold"] = Parse(StashGoldText, L.T("oro del alijo")) };
        }
        if (skillsTouched) request.Skills = Skills.Select(s => new SkillChange(s.Id, s.Points)).ToList();
        return request;
    }
    void Edit(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if ((IsSyntheticDemo || IsBusy) && !loading) return;
        if (field == value) return; if (!loading && HasCharacter) Remember(); field = value; Raise(property);
        if (!loading && HasCharacter) { basicTouched = true; MarkDirty(); }
    }
    string PendingValidationText => IsNew ? L.T("Personaje nuevo pendiente de revisión y guardado.") : IsDirty ? L.T("Cambios pendientes de revisar.") : L.T("Sin cambios pendientes.");
    void MarkDirty() { IsDirty = true; reviewed = validPlan = false; planId = ""; ValidationMessage = PendingValidationText; Raise(nameof(ValidationMessage)); RaiseSaveState(); }
    void ApplySnapshot(JsonObject snapshot, bool fields)
    {
        loading = true;
        try
        {
            string? selectedId = SelectedItem?.Id; Snapshot = snapshot;
            var character = snapshot.Obj("character"); var attributes = character.Obj("attributes");
            if (fields)
            {
                CharacterName = character.Text("name"); LevelText = character.Int("level", 1).ToString(); ExperienceText = character.Long("experience").ToString();
                StrengthText = attributes.Long("strength").ToString(); DexterityText = attributes.Long("dexterity").ToString(); VitalityText = attributes.Long("vitality").ToString(); EnergyText = attributes.Long("energy").ToString(); GoldText = attributes.Long("gold").ToString(); StashGoldText = attributes.Long("stashGold").ToString();
            }
            foreach (var skill in Skills) skill.PropertyChanged -= OnSkillRowChanged;
            Replace(Skills, snapshot.Array("skills").OfType<JsonObject>().Select(n => new SkillViewModel(n, ChangeSkill)));
            foreach (var skill in Skills) skill.PropertyChanged += OnSkillRowChanged;
            UpdateRowLocks();
            Replace(Items, snapshot.Array("items").OfType<JsonObject>().Select(n => new ItemViewModel(n)));
            Replace(MercenaryItems, snapshot.Obj("mercenary").Array("items").OfType<JsonObject>().Select(n => new ItemViewModel(n)));
            SelectedItem = Items.Concat(MercenaryItems).FirstOrDefault(i => i.Id == selectedId) ?? Items.FirstOrDefault(i => i.Page == "equipped" && i.Slot == "weapon" && i.WeaponSet == SelectedWeaponSet);
            RefreshInventory();
            foreach (var property in new[] { nameof(Snapshot), nameof(ClassName), nameof(ClassLabel), nameof(BaseLifeText), nameof(BaseManaText), nameof(SkillPointsText), nameof(AttributePointsText), nameof(IsNew), nameof(MercenaryName), nameof(MercenaryLevelText), nameof(MercenaryAura), nameof(HasMercenary), nameof(ActiveWeaponSet), nameof(GridColumns), nameof(GridRows), nameof(GridWidth), nameof(GridHeight), nameof(StashGridColumns), nameof(StashGridRows) }) Raise(property);
        }
        finally { loading = false; }
    }
    void RefreshInventory()
    {
        Replace(VisibleInventory, Items.Where(i => i.Page == SelectedStorage));
        Replace(StashItems, Items.Where(i => i.Page == "stash"));
        Replace(Equipped, Items.Where(i => i.Page == "equipped" && (!i.WeaponSet.HasValue || i.WeaponSet == SelectedWeaponSet)));
        string[] slots = ["helm", "amulet", "weapon", "shield", "armor", "gloves", "belt", "boots", "ring1", "ring2"];
        Replace(EquipmentSlots, slots.Select(s => new EquipmentSlotViewModel(s, Names.Slot(s), Equipped.FirstOrDefault(i => i.Slot == s))));
        Replace(MercenarySlots, slots.Select(s => new EquipmentSlotViewModel(s, Names.Slot(s), MercenaryItems.FirstOrDefault(i => i.Slot == s))));
    }
    void ChangeSkill(SkillViewModel skill, int value)
    {
        if (loading) return; Remember(); skillsTouched = true; draft.Skills = Skills.Select(s => new SkillChange(s.Id, s.Id == skill.Id ? value : s.Points)).ToList(); MarkDirty();
    }
    void OnSkillRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkillViewModel.Points)) Raise(nameof(SkillPointsText));
    }
    async Task RefreshWorldCoreAsync()
    {
        if (!HasCharacter) return;
        WorldSnapshot = await bridge.CallAsync(c => c.World(sessionId, validPlan ? planId : null));
        loading = true;
        try
        {
            WorldQuests.Clear(); WorldWaypoints.Clear();
            foreach (var difficulty in WorldSnapshot.Array("difficulties").OfType<JsonObject>())
            {
                string id = difficulty.Text("id");
                foreach (var quest in difficulty.Array("quests").OfType<JsonObject>()) WorldQuests.Add(new(id, quest, ChangeQuest));
                foreach (var waypoint in difficulty.Array("waypoints").OfType<JsonObject>()) WorldWaypoints.Add(new(id, waypoint, ChangeWaypoint));
            }
        }
        finally { loading = false; }
        UpdateRowLocks();
        Raise(nameof(WorldSnapshot)); Raise(nameof(CurrentDifficulty));
    }
    void ChangeQuest(QuestViewModel row, string state)
    {
        if (loading) return; Remember(); var world = draft.World ??= new(); world.Quests.RemoveAll(q => q.Difficulty == row.Difficulty && q.QuestId == row.Id); world.Quests.Add(new(row.Difficulty, row.Id, state)); MarkDirty();
    }
    void ChangeWaypoint(WaypointViewModel row, bool active)
    {
        if (loading) return; Remember(); var world = draft.World ??= new(); world.Waypoints.RemoveAll(q => q.Difficulty == row.Difficulty && q.Id == row.Id); world.Waypoints.Add(new(row.Difficulty, row.Id, active)); MarkDirty();
    }
    sealed record EditorState(PreviewRequest Draft, JsonObject Snapshot, string[] Fields, bool BasicTouched, bool SkillsTouched, bool Dirty, Dictionary<int, int> SkillPoints);
    EditorState Capture() => new(NativeEditorBridge.Clone(draft), (JsonObject)Snapshot.DeepClone(), [CharacterName, LevelText, ExperienceText, StrengthText, DexterityText, VitalityText, EnergyText, GoldText, StashGoldText], basicTouched, skillsTouched, IsDirty, Skills.ToDictionary(s => s.Id, s => s.Points));
    void Remember() { if (loading) return; undo.Add(Capture()); if (undo.Count > 100) undo.RemoveAt(0); redo.Clear(); Requery(); }
    void Restore(EditorState state)
    {
        draft = NativeEditorBridge.Clone(state.Draft); ApplySnapshot((JsonObject)state.Snapshot.DeepClone(), true);
        loading = true;
        try
        {
            CharacterName = state.Fields[0]; LevelText = state.Fields[1]; ExperienceText = state.Fields[2]; StrengthText = state.Fields[3]; DexterityText = state.Fields[4]; VitalityText = state.Fields[5]; EnergyText = state.Fields[6]; GoldText = state.Fields[7]; StashGoldText = state.Fields[8];
            foreach (var skill in Skills) if (state.SkillPoints.TryGetValue(skill.Id, out var points)) skill.RestorePoints(points);
        }
        finally { loading = false; }
        basicTouched = state.BasicTouched; skillsTouched = state.SkillsTouched; IsDirty = state.Dirty; reviewed = validPlan = false;
    }
    static long Parse(string value, string label) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : throw new ApiProblem("number_invalid", L.T("Introduce un número entero válido para ") + label + ".");
    static string Number(double value) => value.ToString("N1", L.Culture).TrimEnd('0').TrimEnd(L.Culture.NumberFormat.NumberDecimalSeparator[0]);
    static string Describe(JsonNode? value) => value switch { null => "—", JsonValue v when v.TryGetValue<string>(out var s) => s, JsonValue v => v.ToJsonString(), JsonArray array => array.Count + L.T(" elementos"), _ => L.T("Configuración") };
    static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values) { var copy = values.ToArray(); collection.Clear(); foreach (var value in copy) collection.Add(value); }
    static void WriteNew(string path, object value) { using var stream = new FileStream(Path.GetFullPath(path), FileMode.CreateNew, FileAccess.Write, FileShare.None); JsonSerializer.Serialize(stream, value, NativeEditorBridge.Json); stream.Flush(true); }
    static T ReadDocument<T>(string path) { if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new ApiProblem("document_size", L.T("El archivo supera el tamaño admitido.")); return JsonSerializer.Deserialize<T>(File.ReadAllText(path), NativeEditorBridge.Json) ?? throw new ApiProblem("document_invalid", L.T("El archivo está vacío o no es válido.")); }
    static void Requery() => CommandManager.InvalidateRequerySuggested();
    void UpdateRowLocks()
    {
        bool locked = IsBusy || IsSyntheticDemo;
        foreach (var skill in Skills) skill.IsReadOnly = locked;
        foreach (var quest in WorldQuests) quest.IsReadOnly = locked;
        foreach (var waypoint in WorldWaypoints) waypoint.IsReadOnly = locked;
    }
}
