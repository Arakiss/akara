using System.Windows.Markup;

namespace Akara.Desktop.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string source) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(source);
}
