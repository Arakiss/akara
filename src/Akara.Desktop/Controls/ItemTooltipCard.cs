using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Controls;

/// <summary>A read-only view of the hovered item; never changes the editor selection.</summary>
public sealed class ItemTooltipCard : Border
{
    public ItemViewModel Item { get; }
    public ScrollViewer PropertiesViewport { get; }
    static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(13, 13, 15));
    static readonly Brush Ivory = new SolidColorBrush(Color.FromRgb(230, 218, 194));
    static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(166, 157, 142));
    static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(145, 175, 219));

    public ItemTooltipCard(ItemViewModel item)
    {
        Item = item;
        Width = 370; MaxHeight = 620; Padding = new Thickness(16);
        Background = Ink; BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.Accent));
        BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(2);
        TextElement.SetFontFamily(this, new FontFamily("Segoe UI"));
        TextElement.SetForeground(this, Ivory); TextElement.SetFontSize(this, 13);
        AutomationProperties.SetName(this, "Propiedades de " + item.Name);
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Child = layout;

        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var image = new Image { Source = ItemArt.GetPresentation(item), Width = 54, Height = 78, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(image, item.ArtSourceLabel);
        header.Children.Add(image);
        var identity = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(identity, 1); header.Children.Add(identity);
        var name = Line(item.Name, BorderBrush, 21); name.SetResourceReference(TextBlock.FontFamilyProperty, "DisplayFont");
        identity.Children.Add(name);
        identity.Children.Add(Line(item.BaseName, Ivory, 13));
        identity.Children.Add(Line(item.QualityLabel + (item.RuneWordName.Length > 0 ? " · Palabra rúnica" : ""), Muted, 12));
        identity.Children.Add(Line("Nivel de objeto " + item.Level + (item.Ethereal ? " · Etéreo" : ""), Muted, 12));
        layout.Children.Add(header);

        var divider = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(87, 69, 47)), Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(divider, 1); layout.Children.Add(divider);
        var body = new StackPanel();
        PropertiesViewport = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, Focusable = false };
        Grid.SetRow(PropertiesViewport, 2); layout.Children.Add(PropertiesViewport);

        if (item.PersonalizedName.Length > 0) body.Children.Add(Line("Personalizado: " + item.PersonalizedName, Ivory));
        if (item.Data["identified"] is JsonValue identified && identified.TryGetValue<bool>(out bool known) && !known)
            body.Children.Add(Line("Sin identificar", Muted));
        if (item.TwoHanded) body.Children.Add(Line("Arma de dos manos", Ivory));
        // The save stores base defense, not the game's final defense after modifiers.
        if (item.Data.NullableInt("defense") is { } defense) body.Children.Add(Line("Defensa base: " + defense, Ivory));
        if (item.Data.NullableInt("maxDurability") is > 0 and var maximum)
            body.Children.Add(Line("Durabilidad: " + item.Data.Int("durability") + " / " + maximum, Ivory));
        if (item.Data.NullableInt("quantity") is > 1 and var quantity) body.Children.Add(Line("Cantidad: " + quantity, Ivory));

        var requirements = item.Data.Obj("requirements");
        var required = new List<string>();
        foreach (var (key, label) in new[] { ("level", "Nivel"), ("strength", "Fuerza"), ("dexterity", "Destreza") })
            if (requirements.Int(key) > 0) required.Add(label + " " + requirements.Int(key));
        if (required.Count > 0)
            body.Children.Add(Line((requirements.Bool("estimated") ? "Requisitos estimados: " : "Requisitos: ") + string.Join(" · ", required), Muted, 12));

        if (item.VisibleStats.Count > 0)
        {
            foreach (var stat in item.VisibleStats) body.Children.Add(Property(stat.Text));
        }
        else body.Children.Add(Line("Sin propiedades adicionales", Muted));

        foreach (var bonus in item.Data.Array("setBonusLists").OfType<JsonArray>())
        {
            var stats = bonus.OfType<JsonObject>().Select(Stat).Where(stat => !stat.Hidden).ToArray();
            if (stats.Length == 0) continue;
            body.Children.Add(Section("Bonificación condicional de conjunto"));
            foreach (var stat in stats) body.Children.Add(Property(stat.Text));
        }
        if (item.Sockets.Count > 0)
        {
            body.Children.Add(Section("Engarces: " + item.Sockets.Count(socket => socket is not null) + " / " + item.Sockets.Count));
            for (int index = 0; index < item.Sockets.Count; index++)
            {
                var socket = item.Sockets[index];
                if (socket is null) { body.Children.Add(Line((index + 1) + " · Vacío", Muted)); continue; }
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
                var icon = new Image { Source = ItemArt.GetPresentation(socket), Width = 20, Height = 24, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 8, 0) };
                row.Children.Add(icon); row.Children.Add(Line((index + 1) + " · " + socket.Name, Ivory)); body.Children.Add(row);
                foreach (var stat in socket.VisibleStats)
                {
                    var text = Property(stat.Text); text.FontSize = 12; text.Margin = new Thickness(28, 1, 0, 2); body.Children.Add(text);
                }
            }
        }
        var location = Line(item.LocationLabel, Muted, 12); location.Margin = new Thickness(0, 12, 0, 0); body.Children.Add(location);
    }

    static TextBlock Line(string text, Brush brush, double size = 13) => new()
    {
        Text = text, Foreground = brush, FontSize = size, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 2), LineHeight = size * 1.5
    };
    static TextBlock Property(string text)
    {
        var line = Line(text, Blue); line.TextAlignment = TextAlignment.Center; line.Margin = new Thickness(0, 4, 0, 2); return line;
    }
    static TextBlock Section(string text)
    {
        var line = Line(text, Muted, 12); line.Margin = new Thickness(0, 12, 0, 4); return line;
    }
    static StatViewModel Stat(JsonObject node) => new(node.Int("id"), node.Text("label", node.Text("name")), node.Int("layer"), node.Long("value"), node.Double("displayValue"), node.Bool("hidden"), node.Text("name"));
}
