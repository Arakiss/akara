using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Akara.Desktop.ViewModels;
using ForjaHoradrica;

namespace Akara.Desktop.Controls;

/// <summary>Shared by the visible object editor and offscreen regression checks.</summary>
public sealed class ItemPropertiesEditor : StackPanel
{
    public ObservableCollection<ItemPropertyRow> Rows{get;}
    public CheckBox UseCatalogProperties{get;}=new(){Content=L.T("Usar propiedades automáticas del catálogo")};
    public DataGrid Table{get;}
    public TextBox ValueEditor{get;}=new(){Width=130,HorizontalAlignment=HorizontalAlignment.Left};
    public TextBox ParameterEditor{get;}=new(){Width=130,HorizontalAlignment=HorizontalAlignment.Left};
    public ComboBox PropertyPicker{get;}
    public Button AddButton{get;}=new(){Content=L.T("Añadir propiedad"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};
    readonly TextBlock message=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
    public sealed record PropertyChoice(CatalogEntry Entry,string Name);
    public ItemPropertiesEditor(IEnumerable<ItemPropertyRow> rows,IReadOnlyList<CatalogEntry> catalog,bool inherit)
    {
        Rows=new(rows);Margin=new Thickness(0,14,0,0);
        var hint=new TextBlock{Text=L.T("Selecciona una propiedad y cambia su valor en el campo de abajo."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};hint.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");Children.Add(hint);
        Table=new DataGrid{ItemsSource=Rows,IsReadOnly=true,Height=235,SelectionMode=DataGridSelectionMode.Single,SelectionUnit=DataGridSelectionUnit.FullRow};
        AutomationProperties.SetName(Table,L.T("Propiedades del objeto"));
        Table.Columns.Add(new DataGridTextColumn{Header=L.T("Propiedad"),Binding=new Binding(nameof(ItemPropertyRow.Label)),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        Table.Columns.Add(new DataGridTextColumn{Header=L.T("Valor"),Binding=new Binding(nameof(ItemPropertyRow.Value)),Width=110});Children.Add(Table);
        var selected=new TextBlock{Margin=new Thickness(0,12,0,5),FontWeight=FontWeights.SemiBold};selected.SetBinding(TextBlock.TextProperty,new Binding("SelectedItem.Label"){Source=Table,TargetNullValue=L.T("Selecciona una propiedad")});Children.Add(selected);
        var editing=new DockPanel();var unit=new TextBlock{Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};unit.SetBinding(TextBlock.TextProperty,new Binding("SelectedItem.Unit"){Source=Table});
        ValueEditor.SetBinding(TextBox.TextProperty,new Binding("SelectedItem.Value"){Source=Table,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});AutomationProperties.SetName(ValueEditor,L.T("Valor de la propiedad seleccionada"));
        DockPanel.SetDock(ValueEditor,Dock.Left);editing.Children.Add(ValueEditor);editing.Children.Add(unit);Children.Add(editing);
        var advancedBody=new StackPanel();var technical=new TextBlock();technical.SetBinding(TextBlock.TextProperty,new Binding("SelectedItem.TechnicalLabel"){Source=Table});technical.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");advancedBody.Children.Add(technical);
        advancedBody.Children.Add(new TextBlock{Text=L.T("Parámetro nativo de habilidad, clase o carga; conserva el actual si sólo cambias el valor."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,5)});
        ParameterEditor.SetBinding(TextBox.TextProperty,new Binding("SelectedItem.Layer"){Source=Table,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged,ValidatesOnExceptions=true});AutomationProperties.SetName(ParameterEditor,L.T("Parámetro avanzado de la propiedad"));advancedBody.Children.Add(ParameterEditor);
        Children.Add(new Expander{Header=L.T("Parámetro avanzado"),Content=advancedBody,Margin=new Thickness(0,5,0,0)});
        var remove=new Button{Content=L.T("Quitar propiedad seleccionada"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,0)};remove.Click+=(_,_)=>{if(Table.SelectedItem is ItemPropertyRow row){Rows.Remove(row);UseCatalogProperties.IsChecked=false;Table.SelectedItem=Rows.FirstOrDefault();}};Children.Add(remove);
        PropertyPicker=new ComboBox{ItemsSource=catalog.Select(entry=>new PropertyChoice(entry,Names.Stat(entry.Name,entry.Name.Replace("item_","").Replace('_',' '),0))).OrderBy(choice=>choice.Name).ToArray(),DisplayMemberPath="Name",IsEditable=true,IsTextSearchEnabled=true};AutomationProperties.SetName(PropertyPicker,L.T("Buscar una propiedad para añadir"));
        var addBody=new StackPanel();addBody.Children.Add(new TextBlock{Text=L.T("Busca por nombre y selecciona una propiedad de la lista."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,7)});addBody.Children.Add(PropertyPicker);addBody.Children.Add(AddButton);
        AddButton.Click+=(_,_)=>{if(PropertyPicker.SelectedItem is not PropertyChoice choice){message.Text=L.T("Selecciona una propiedad de la lista.");return;}var existing=Rows.FirstOrDefault(row=>row.Id==choice.Entry.Data.Int("nativeId")&&row.Layer==0);var added=existing??new ItemPropertyRow(new(choice.Entry.Data.Int("nativeId"),0,Value:0),choice.Entry);if(existing is null)Rows.Add(added);UseCatalogProperties.IsChecked=false;Table.SelectedItem=added;Table.ScrollIntoView(added);message.Text=existing is null?L.T("Propiedad añadida. Escribe su valor."):L.T("Esta propiedad ya existe y está seleccionada.");};
        Children.Add(new Expander{Header=L.T("Añadir una propiedad"),Content=addBody,Margin=new Thickness(0,8,0,0)});
        message.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");Children.Add(message);
        UseCatalogProperties.IsChecked=inherit;UseCatalogProperties.ToolTip=L.T("Activa esta opción para regenerar las propiedades desde el catálogo en lugar de usar la tabla personalizada.");UseCatalogProperties.Checked+=(_,_)=>RefreshMode();UseCatalogProperties.Unchecked+=(_,_)=>RefreshMode();Children.Add(UseCatalogProperties);
        Table.SelectionChanged+=(_,_)=>RefreshMode();Table.SelectedItem=Rows.FirstOrDefault();RefreshMode();
    }
    void RefreshMode(){Table.IsEnabled=UseCatalogProperties.IsChecked!=true;ValueEditor.IsEnabled=Table.IsEnabled&&Table.SelectedItem is ItemPropertyRow;}
    public bool TryGetProperties(out List<ItemStatSpec>? properties,out string? error)
    {
        properties=null;error=null;if(UseCatalogProperties.IsChecked==true)return true;
        if(Validation.GetHasError(ParameterEditor)){error=L.T("Escribe un número entero válido para el parámetro avanzado.");return false;}
        try{properties=Rows.Select(row=>row.ToSpec()).ToList();return true;}
        catch(Exception ex)when(ex is FormatException or OverflowException){error=L.T("Escribe un número válido para cada propiedad modificada.");return false;}
    }
}
