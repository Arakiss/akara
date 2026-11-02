using System.Globalization;
using System.Windows.Data;

namespace Akara.Desktop.Localization;

public sealed class LocalizedFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => L.F(parameter switch
    {
        "activeSet" => "Activo: {0}", "gold" => "Oro: {0}", "items" => "{0} objetos", "pieces" => "{0} piezas",
        _ => throw new ArgumentException("Unknown localized format.", nameof(parameter))
    }, value);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
