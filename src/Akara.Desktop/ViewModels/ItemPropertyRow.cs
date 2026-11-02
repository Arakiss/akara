using System.Globalization;
using ForjaHoradrica;

namespace Akara.Desktop.ViewModels;

public sealed class ItemPropertyRow : ObservableModel
{
    readonly ItemStatSpec original;
    readonly string originalValue;
    string value;
    int layer;
    public int Id=>original.Id;
    public string NativeName{get;}
    public string Unit=>Names.PercentStat(NativeName)?"%":"";
    public string Label=>Names.Stat(NativeName,NativeName.Replace("item_","").Replace('_',' '),Layer)+(Unit.Length>0?" (%)":"");
    public string TechnicalLabel=>NativeName+" · ID "+Id;
    public string Value{get=>value;set=>Set(ref this.value,value);}
    public int Layer{get=>layer;set{if(Set(ref layer,value))Raise(nameof(Label));}}
    public ItemPropertyRow(ItemStatSpec source,CatalogEntry? metadata,double? human=null)
    {
        original=source;layer=source.Layer;NativeName=metadata?.Name??"Propiedad "+source.Id;
        value=(human??source.Value??source.RawValue??0).ToString("0.########",CultureInfo.GetCultureInfo("es-ES"));originalValue=value;
    }
    public ItemStatSpec ToSpec()=>Value==originalValue&&Layer==original.Layer?original:new(Id,Layer,Parse(Value));
    static double Parse(string text)=>double.TryParse(text,NumberStyles.Float,CultureInfo.GetCultureInfo("es-ES"),out double value)?value:double.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
}
