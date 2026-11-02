using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows.Input;

namespace Akara.Desktop.ViewModels;

public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(property); return true;
    }
}

public sealed class NativeCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public async void Execute(object? parameter) => await execute(parameter);
    public Task ExecuteAsync(object? parameter = null) => CanExecute(parameter) ? execute(parameter) : Task.CompletedTask;
}

public sealed record NavEntry(string Key, string Title, string Glyph);
public sealed record ChoiceEntry(string Value, string Label);
public sealed record SaveEntry(string Name, string Path, string? ProfileId)
{
    public string FolderLabel => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path)) ?? L.T("Archivo local");
}
public sealed record ProfileEntry(string Id, string Name, string ExcelPath);
public sealed record PresetEntry(string Id, string Name, string Description, int ClassId, string ClassName, string GuideText = "")
{
    public override string ToString() => Name;
}
public sealed record CatalogEntry(string Id, string Name, string Type, string Code, bool Supported, int LevelRequired, JsonObject Data)
{
    public override string ToString() => Name;
}
public sealed record LibraryEntry(string Id, string Name, string ClassName, int? Level, int ItemCount);
public sealed record BackupViewModel(string Id, string Path, string Hash, string CreatedAt)
{
    public string DisplayDate => DateTimeOffset.TryParse(CreatedAt, out var date) ? date.ToLocalTime().ToString("dd MMM yyyy · HH:mm", L.Culture) : CreatedAt;
}
public sealed record DiffViewModel(string Section, string Label, string Before, string After);
public sealed record StatViewModel(int Id, string Label, int Layer, long RawValue, double DisplayValue, bool Hidden, string NativeName = "")
{
    public string FriendlyLabel => Names.Stat(NativeName, Label, Layer);
    public string Text
    {
        get
        {
            string value = DisplayValue.ToString("0.###", L.Culture);
            if (NativeName == "item_skillongethit")
            {
                var match = System.Text.RegularExpressions.Regex.Match(Label, @"^(?:Al recibir un golpe|When struck): (.+) \((?:nivel|level)\s*(\d+)\)$");
                if (match.Success) return L.F("{0}% de lanzar {1} (nivel {2}) al recibir un golpe", value, Names.Skill(match.Groups[1].Value), match.Groups[2].Value);
            }
            if (NativeName is "item_cannotbefrozen" && DisplayValue != 0) return L.T("No puede ser congelado");
            if (NativeName is "item_indesctructible" or "item_indestructible" && DisplayValue != 0) return L.T("Indestructible");
            string suffix = Names.PercentStat(NativeName) ? "%" : "";
            return FriendlyLabel + "  " + (DisplayValue >= 0 ? "+" : "") + value + suffix;
        }
    }
}

public sealed class SkillViewModel : ObservableModel
{
    int points;
    bool readOnly;
    readonly Action<SkillViewModel, int>? change;
    public int Id { get; }
    public string Name => Names.Skill(NativeName);
    public string NativeName { get; }
    public int MaxPoints { get; }
    public int RequiredLevel { get; }
    public int TabId { get; }
    public int IconCell { get; }
    public string IconClass { get; }
    public int Row { get; }
    public int Column { get; }
    public string Requirement => L.T("Nivel ") + RequiredLevel + L.T(" · máximo ") + MaxPoints;
    public bool IsReadOnly { get => readOnly; set => Set(ref readOnly,value); }
    public int Points { get => points; set { if (IsReadOnly || points == value) return; change?.Invoke(this, value); points = value; Raise(); Raise(nameof(Progress)); } }
    internal void RestorePoints(int value) { points = value; Raise(nameof(Points)); Raise(nameof(Progress)); }
    public double Progress => MaxPoints == 0 ? 0 : 100d * Points / MaxPoints;
    public SkillViewModel(JsonObject node, Action<SkillViewModel, int>? changed)
    {
        Id = node.Int("id"); NativeName = node.Text("name");
        points = node.Int("points"); MaxPoints = node.Int("maxPoints", 25); RequiredLevel = node.Int("requiredLevel");
        IconCell = node.Int("iconCell", -1); IconClass = node.Text("iconClass");
        TabId = node.Int("tabId"); Row = node.Int("row"); Column = node.Int("column"); change = changed;
    }
}

public sealed class ItemViewModel
{
    public JsonObject Data { get; }
    public string Id => Data.Text("id");
    public string Name => Data.Text("name", Code);
    public string BaseName => Data.Text("baseName", Code);
    public string Code => Data.Text("code");
    public string Quality => Data.Text("quality");
    public string QualityLabel => Names.Quality(Quality);
    public bool HasGameArt => Akara.Desktop.Services.LocalGameArt.TryGetItem(this) is not null;
    public string ArtSourceLabel => HasGameArt ? L.T("Gráfico original del juego") : L.T("Imagen del juego no disponible");
    public string Owner => Data.Text("owner", "character");
    public string Page => Data.Text("page");
    public string Slot => Data.Text("slot");
    public string SlotLabel => Names.Slot(Slot);
    public int? WeaponSet => Data.NullableInt("weaponSet");
    public int X => Data.Int("x");
    public int Y => Data.Int("y");
    public int GridX => Page == "belt" ? X % 4 : X;
    public int GridY => Page == "belt" ? X / 4 : Y;
    public int Width => Math.Max(1, Data.Int("width", 1));
    public int Height => Math.Max(1, Data.Int("height", 1));
    public int Level => Data.Int("level");
    public bool Ethereal => Data.Bool("ethereal");
    public bool TwoHanded => Data.Bool("twoHanded");
    public string PersonalizedName => Data.Text("personalizedName");
    public string RuneWordName => Data.Text("runewordName");
    public string Accent => Quality switch { "unique" => "#D7BC80", "set" => "#84AB89", "magic" => "#89A8D0", "rare" => "#D7C974", "craft" or "crafted" => "#CB9B75", _ => "#C8C2B8" };
    public string Kind => Slot is "weapon" or "shield" or "helm" or "armor" or "amulet" or "ring1" or "ring2" or "boots" or "gloves" or "belt" ? Slot : Data.Text("itemType");
    public string Glyph => Kind switch { "weapon" => "⚔", "shield" => "◈", "helm" => "♜", "armor" => "♜", "ring1" or "ring2" => "◌", "amulet" => "◇", "boots" => "▰", "gloves" => "✦", "belt" => "═", _ => "◈" };
    public double PixelX => X * 52d;
    public double PixelY => Y * 52d;
    public double PixelWidth => Width * 52d - 4;
    public double PixelHeight => Height * 52d - 4;
    public string DetailLine => QualityLabel + L.T(" · nivel de objeto ") + Level + (Ethereal ? L.T(" · etéreo") : "");
    public string LocationLabel => Owner == "mercenary" ? L.T("Mercenario · ") + SlotLabel : Page == "equipped" ? SlotLabel + (WeaponSet.HasValue ? " · " + (WeaponSet == 1 ? "I" : "II") : "") : Names.Storage(Page);
    public IReadOnlyList<StatViewModel> Stats { get; }
    public IReadOnlyList<StatViewModel> VisibleStats => Stats.Where(stat => !stat.Hidden).ToList();
    public IReadOnlyList<ItemViewModel?> Sockets { get; }
    public string StatsText => string.Join(Environment.NewLine, Stats.Where(s => !s.Hidden).Select(s => s.Text));
    public ItemViewModel(JsonObject node)
    {
        Data = node;
        Stats = node.Array("stats").OfType<JsonObject>().Concat(node.Array("runewordStats").OfType<JsonObject>()).Select(s => new StatViewModel(s.Int("id"), s.Text("label", s.Text("name")), s.Int("layer"), s.Long("value"), s.Double("displayValue"), s.Bool("hidden"), s.Text("name"))).ToList();
        Sockets = node.Array("sockets").Select(n => n is JsonObject o ? new ItemViewModel(o) : null).ToList();
    }
}

public sealed record EquipmentSlotViewModel(string Slot, string Label, ItemViewModel? Item)
{
    public bool IsEmpty => Item is null;
    public string Name => Item?.Name ?? Label;
    public string Accent => Item?.Accent ?? "#68645D";
}

public sealed class QuestViewModel : ObservableModel
{
    string state;
    readonly Action<QuestViewModel, string>? changed;
    public string Difficulty { get; }
    public string DifficultyLabel => Names.Difficulty(Difficulty);
    public string Id { get; }
    readonly string nativeName;
    public string Name => Names.Quest(nativeName);
    public int Act { get; }
    public int Flags { get; }
    public bool IsReadOnly { get; set; }
    public string State { get => state; set { if (IsReadOnly || state == value) return; changed?.Invoke(this, value); state = value; Raise(); Raise(nameof(StateLabel)); } }
    public string StateLabel => Names.QuestState(State);
    public QuestViewModel(string difficulty, JsonObject node, Action<QuestViewModel, string>? change)
    { Difficulty = difficulty; Id = node.Text("id"); nativeName = node.Text("name"); Act = node.Int("act"); Flags = node.Int("flags"); state = node.Text("state"); changed = change; }
}

public sealed class WaypointViewModel : ObservableModel
{
    bool active;
    readonly Action<WaypointViewModel, bool>? changed;
    public string Difficulty { get; }
    public string DifficultyLabel => Names.Difficulty(Difficulty);
    public int Id { get; }
    readonly string nativeName;
    public string Name => Names.Waypoint(nativeName);
    public int Act { get; }
    public bool IsReadOnly { get; set; }
    public bool Active { get => active; set { if (IsReadOnly || active == value) return; changed?.Invoke(this, value); active = value; Raise(); } }
    public WaypointViewModel(string difficulty, JsonObject node, Action<WaypointViewModel, bool>? change)
    { Difficulty = difficulty; Id = node.Int("id"); nativeName = node.Text("name"); Act = node.Int("act"); active = node.Bool("active"); changed = change; }
}

public static class NodeValues
{
    public static string Text(this JsonNode? node, string name, string fallback = "") => node?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : fallback;
    public static int Int(this JsonNode? node, string name, int fallback = 0) => node?[name] is JsonValue value && value.TryGetValue<int>(out var n) ? n : fallback;
    public static int? NullableInt(this JsonNode? node, string name) => node?[name] is JsonValue value && value.TryGetValue<int>(out var n) ? n : null;
    public static long Long(this JsonNode? node, string name, long fallback = 0) => node?[name] is JsonValue value && value.TryGetValue<long>(out var n) ? n : fallback;
    public static double Double(this JsonNode? node, string name, double fallback = 0) => node?[name] is JsonValue value && value.TryGetValue<double>(out var n) ? n : fallback;
    public static bool Bool(this JsonNode? node, string name, bool fallback = false) => node?[name] is JsonValue value && value.TryGetValue<bool>(out var n) ? n : fallback;
    public static JsonArray Array(this JsonNode? node, string name) => node?[name] as JsonArray ?? [];
    public static JsonObject Obj(this JsonNode? node, string name) => node?[name] as JsonObject ?? new();
}

public static class Names
{
    static readonly Dictionary<string, string> StatLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["strength"]="Fuerza", ["dexterity"]="Destreza", ["vitality"]="Vitalidad", ["energy"]="Energía", ["maxhp"]="Vida máxima", ["maxmana"]="Maná máximo", ["maxstamina"]="Resistencia máxima",
        ["item_singleskill"]="Habilidad de clase", ["item_nonclassskill"]="Habilidad adicional", ["item_skillongethit"]="Efecto al recibir un golpe", ["item_skillonhit"]="Efecto al golpear", ["item_skillonattack"]="Efecto al atacar", ["item_skillondeath"]="Efecto al morir", ["item_skillonkill"]="Efecto al matar", ["item_charged_skill"]="Cargas de habilidad",
        ["passive_fire_mastery"]="Daño de habilidades de fuego", ["passive_ltng_mastery"]="Daño de habilidades de rayos", ["passive_cold_mastery"]="Daño de habilidades de frío", ["passive_pois_mastery"]="Daño de habilidades de veneno",
        ["passive_fire_pierce"]="Penetración de resistencia al fuego", ["passive_ltng_pierce"]="Penetración de resistencia a los rayos", ["passive_cold_pierce"]="Penetración de resistencia al frío", ["passive_pois_pierce"]="Penetración de resistencia al veneno",
        ["item_manaafterkill"]="Maná tras cada muerte", ["item_healafterkill"]="Vida tras cada muerte", ["item_replenish_durability"]="Reparación de durabilidad", ["item_replenish_quantity"]="Reponer cantidad",
        ["item_hp_perlevel"]="Vida por nivel", ["item_mana_perlevel"]="Maná por nivel", ["item_hpregen"]="Regeneración de vida", ["hpregen"]="Regeneración de vida", ["manarecovery"]="Regeneración de maná",
        ["item_maxhp_percent"]="Vida máxima", ["item_maxmana_percent"]="Maná máximo", ["item_fastercastrate"]="Velocidad de lanzamiento", ["item_fastergethitrate"]="Recuperación de impacto", ["item_fastermovevelocity"]="Velocidad de carrera", ["item_fasterattackrate"]="Velocidad de ataque", ["item_fasterblockrate"]="Velocidad de bloqueo",
        ["item_magicbonus"]="Hallazgo mágico", ["item_goldbonus"]="Oro encontrado", ["item_allskills"]="Todas las habilidades", ["item_armor_percent"]="Defensa mejorada", ["armorclass"]="Defensa", ["armorclass_vs_missile"]="Defensa contra proyectiles", ["armorclass_vs_hth"]="Defensa cuerpo a cuerpo",
        ["item_maxdamage_percent"]="Daño máximo mejorado", ["item_mindamage_percent"]="Daño mínimo mejorado", ["item_damage_percent"]="Daño mejorado", ["item_normaldamage"]="Daño añadido", ["mindamage"]="Daño mínimo", ["maxdamage"]="Daño máximo", ["secondary_mindamage"]="Daño mínimo a dos manos", ["secondary_maxdamage"]="Daño máximo a dos manos", ["item_throw_mindamage"]="Daño mínimo al lanzar", ["item_throw_maxdamage"]="Daño máximo al lanzar",
        ["fireresist"]="Resistencia al fuego", ["lightresist"]="Resistencia a los rayos", ["coldresist"]="Resistencia al frío", ["poisonresist"]="Resistencia al veneno", ["magicresist"]="Resistencia mágica", ["maxfireresist"]="Resistencia máxima al fuego", ["maxlightresist"]="Resistencia máxima a los rayos", ["maxcoldresist"]="Resistencia máxima al frío", ["maxpoisonresist"]="Resistencia máxima al veneno",
        ["damageresist"]="Reducción de daño físico", ["normal_damage_reduction"]="Daño reducido", ["magic_damage_reduction"]="Daño mágico reducido", ["lifedrainmindam"]="Robo de vida por impacto", ["manadrainmindam"]="Robo de maná por impacto", ["item_lifesteal"]="Robo de vida por impacto", ["item_manasteal"]="Robo de maná por impacto",
        ["tohit"]="Puntuación de ataque", ["item_tohit_percent"]="Puntuación de ataque", ["item_tohit_perlevel"]="Puntuación de ataque por nivel", ["item_tohit_demon"]="Puntuación de ataque contra demonios", ["item_tohit_undead"]="Puntuación de ataque contra no muertos", ["item_demondamage_percent"]="Daño contra demonios", ["item_undeaddamage_percent"]="Daño contra no muertos",
        ["item_crushingblow"]="Golpe triturador", ["item_deadlystrike"]="Golpe mortal", ["item_openwounds"]="Heridas abiertas", ["item_pierce"]="Ataque perforador", ["item_knockback"]="Retroceso", ["item_slow"]="Ralentización del objetivo", ["item_absorbfire_percent"]="Absorción de fuego", ["item_absorblight_percent"]="Absorción de rayos", ["item_absorbcold_percent"]="Absorción de frío",
        ["item_req_percent"]="Requisitos", ["item_light"]="Radio de luz", ["item_howl"]="Huye el objetivo", ["item_stupidity"]="Ciega al objetivo", ["item_preventheal"]="Impide curar al monstruo", ["item_damagetomana"]="Daño recibido convertido en maná", ["item_attackertakesdamage"]="Daño al atacante", ["item_poisonlengthresist"]="Reducción de duración del veneno"
    };
    public static string Stat(string nativeName, string fallback, int layer)
    {
        if (nativeName == "item_addclassskills") return L.F("Habilidades de {0}", Class(layer switch {0=>"Amazon",1=>"Sorceress",2=>"Necromancer",3=>"Paladin",4=>"Barbarian",5=>"Druid",6=>"Assassin",7=>"Warlock",_=>L.T("clase")}));
        if (nativeName is "item_singleskill" or "item_nonclassskill" && (fallback.StartsWith("Habilidad: ",StringComparison.Ordinal)||fallback.StartsWith("Skill: ",StringComparison.Ordinal))) return Skill(fallback[(fallback.IndexOf(": ",StringComparison.Ordinal)+2)..]);
        if (nativeName is "item_addskill_tab" or "item_skilltab") return L.T("Árbol de habilidades");
        return L.T(StatLabels.GetValueOrDefault(nativeName, fallback));
    }
    public static bool PercentStat(string name) => (name.StartsWith("passive_",StringComparison.Ordinal) && (name.EndsWith("_mastery",StringComparison.Ordinal) || name.EndsWith("_pierce",StringComparison.Ordinal))) || name.Contains("resist", StringComparison.Ordinal) || name.EndsWith("_percent",StringComparison.Ordinal) || name is "item_fastercastrate" or "item_fastergethitrate" or "item_fastermovevelocity" or "item_fasterattackrate" or "item_fasterblockrate" or "item_magicbonus" or "item_goldbonus" or "item_crushingblow" or "item_deadlystrike" or "item_openwounds" or "item_pierce" or "item_damagetomana" or "lifedrainmindam" or "manadrainmindam" or "manarecovery" or "item_req_percent";
    public static string Class(string name) => !L.IsSpanish ? name : name switch { "Amazon" => "Amazona", "Sorceress" => "Hechicera", "Necromancer" => "Nigromante", "Paladin" => "Paladín", "Barbarian" => "Bárbaro", "Druid" => "Druida", "Assassin" => "Asesina", "Warlock" => "Brujo", _ => name };
    public static string Slot(string name) => name switch { "weapon" => L.T("Arma"), "shield" => L.T("Mano secundaria"), "helm" => L.T("Yelmo"), "armor" => L.T("Armadura"), "amulet" => L.T("Amuleto"), "ring1" => L.T("Anillo derecho"), "ring2" => L.T("Anillo izquierdo"), "boots" => L.T("Botas"), "gloves" => L.T("Guantes"), "belt" => L.T("Cinturón"), _ => name };
    public static string Storage(string name) => name switch { "inventory" => L.T("Inventario"), "stash" => L.T("Alijo"), "cube" => L.T("Cubo horádrico"), "belt" => L.T("Cinturón"), "equipped" => L.T("Equipado"), _ => name };
    public static string Difficulty(string name) => name switch { "normal" => "Normal", "nightmare" => L.T("Pesadilla"), "hell" => L.T("Infierno"), _ => name };
    public static string MercenaryType(string name) => !L.IsSpanish ? name : name switch { "Desert Mercenary" => L.T("Mercenario del desierto"), "Rogue Scout" => L.T("Exploradora"), "Iron Wolf" => L.T("Lobo de hierro"), "Barbarian" => "Bárbaro", _ => name };
    public static string Aura(string name) => !L.IsSpanish ? name : name switch { "Might" => L.T("Fuerza"), "Defiance" => "Desafío", "Blessed Aim" => "Puntería bendecida", "Prayer" => "Plegaria", "Thorns" => "Espinas", "Holy Freeze" => "Ola de frío", _ => name };
    public static string Quality(string name) => name switch { "unique" => L.T("Único"), "set" => L.T("Conjunto"), "rare" => L.T("Raro"), "magic" => L.T("Mágico"), "craft" or "crafted" => L.T("Artesanal"), "normal" => "Normal", "superior" => "Superior", "inferior" => L.T("Inferior"), _ => name };
    public static string QuestState(string name) => name switch { "notStarted" => L.T("Sin empezar"), "started" => L.T("En curso"), "completed" => L.T("Completada"), "rewarded" => L.T("Recompensa recibida"), _ => name };
    public static string Quest(string name) => !L.IsSpanish ? name : name switch
    {
        "Introduction"=>"Introducción", "Completion"=>"Acto completado", "Den Of Evil"=>"La guarida del mal", "Sisters Burial Grounds"=>"El cementerio de las hermanas", "The Search For Cain"=>"La búsqueda de Caín", "The Forgotten Tower"=>"La torre olvidada", "Tools Of The Trade"=>"Herramientas del oficio", "Sisters To The Slaughter"=>"Hermanas al matadero",
        "Radaments Lair"=>"La guarida de Radament", "The Horadric Staff"=>"El bastón horádrico", "Tainted Sun"=>"El sol mancillado", "Arcane Sanctuary"=>"El santuario arcano", "The Summoner"=>"El invocador", "The Seven Tombs"=>"Las siete tumbas",
        "The Golden Bird"=>"El pájaro dorado", "Blade Of The Old Religion"=>"El filo de la vieja religión", "Khalims Will"=>"La voluntad de Khalim", "Lam Esens Tome"=>"El tomo de Lam Esen", "The Blackened Temple"=>"El templo ennegrecido", "The Guardian"=>"El guardián",
        "The Fallen Angel"=>"El ángel caído", "Terrors End"=>"El fin del terror", "Hellforge"=>"La forja del infierno", "Siege On Harrogath"=>"Asedio a Harrogath", "Rescue On Mount Arreat"=>"Rescate en el monte Arreat", "Prison Of Ice"=>"Prisión de hielo", "Betrayal Of Harrogath"=>"La traición de Harrogath", "Rite Of Passage"=>"Rito de iniciación", "Eve Of Destruction"=>"La víspera de la destrucción", _=>name
    };
    public static string Waypoint(string name)=>name switch
    {
        "Rogue Encampment"=>"Campamento de las arpías", "Cold Plains"=>"Llanuras frías", "Stony Field"=>"Campo pedregoso", "Dark Wood"=>"Bosque oscuro", "Black Marsh"=>"Pantano negro", "Outer Cloister"=>"Claustro exterior", "Jail Level1"=>"Cárcel, nivel 1", "Inner Cloister"=>"Claustro interior", "Catacombs Level2"=>"Catacumbas, nivel 2",
        "Sewers Level2"=>"Cloacas, nivel 2", "Dry Hills"=>"Colinas áridas", "Halls of the Dead Level2"=>"Salas de la muerte, nivel 2", "Far Oasis"=>"Oasis lejano", "Lost City"=>"Ciudad perdida", "Palace Cellar Level1"=>"Sótano del palacio, nivel 1", "Arcane Sanctuary"=>"Santuario arcano", "Canyon of the Magi"=>"Cañón de los magos",
        "Kurast Docks"=>"Muelles de Kurast", "Spider Forest"=>"Bosque de las arañas", "Great Marsh"=>"Gran pantano", "Flayer Jungle"=>"Selva de los desolladores", "Lower Kurast"=>"Bajo Kurast", "Kurast Bazaar"=>"Bazar de Kurast", "Upper Kurast"=>"Alto Kurast", "Durance of Hate Level2"=>"Represión del odio, nivel 2", "Pandemonium Fortress"=>"Fortaleza del Pandemónium", "City of the Damned"=>"Ciudad de los malditos", "River of Flame"=>"Río de llamas",
        "Frigid Highlands"=>"Tierras altas gélidas", "Arreat Plateau"=>"Meseta de Arreat", "Crystalline Passage"=>"Pasaje cristalino", "Halls of Pain"=>"Salas del dolor", "Glacial Trail"=>"Camino glacial", "Frozen Tundra"=>"Tundra helada", "The Ancients' Way"=>"Camino de los antiguos", "Worldstone Keep Level2"=>"Torre de la Piedra del Mundo, nivel 2", _=>name
    };
    public static string Skill(string name) => !L.IsSpanish ? name=="townportal"?"Town Portal":name : name switch
    {
        "Fire Wall" => "Muro de fuego", "Energy Shield" => "Escudo de energía", "Blaze" => "Llamarada",
        "townportal" or "Town Portal" => "Portal de la ciudad", "Frozen Orb" => "Orbe helada", "Cold Mastery" => "Dominio del frío", "Ice Bolt" => "Saeta de hielo", "Frost Nova" => "Nova de escarcha", "Warmth" => "Calor", "Frozen Armor" => "Armadura helada", "Static Field" => "Campo estático", "Telekinesis" => "Telequinesis", "Teleport" => "Teletransporte", "Lightning" => "Relámpago", "Chain Lightning" => "Cadena de relámpagos", "Lightning Mastery" => "Dominio del rayo", "Thunder Storm" => "Tormenta atronadora", "Charged Bolt" => "Saeta cargada", "Nova" => "Nova", "Fire Ball" => "Bola de fuego", "Fire Bolt" => "Saeta de fuego", "Fire Mastery" => "Dominio del fuego", "Blizzard" => "Ventisca", "Ice Blast" => "Ráfaga de hielo", "Glacial Spike" => "Punta glacial", "Shiver Armor" => "Armadura gélida", "Chilling Armor" => "Armadura glacial", "Enchant" => "Encantamiento", "Meteor" => "Meteoro", "Hydra" => "Hidra", "Poison Nova"=>"Nova de veneno", "Decrepify"=>"Decrepificar", "Life Tap"=>"Grifo de la vida", "Battle Orders"=>"Órdenes de batalla", "Battle Command"=>"Comando de batalla", _ => name
    };
}
