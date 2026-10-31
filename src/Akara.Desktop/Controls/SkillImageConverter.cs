using System.Globalization;
using System.Windows.Data;
using Akara.Desktop.Services;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Controls;

public sealed class SkillImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SkillViewModel skill ? LocalGameArt.TryGetSkill(skill.IconClass, skill.IconCell) : null;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
