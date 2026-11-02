using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Akara.Desktop.Controls;
using Akara.Desktop.ViewModels;
using ForjaHoradrica;

namespace Akara.Desktop;

internal static class NativeDialogs
{
    static readonly string[] ClassNames=["Amazon","Sorceress","Necromancer","Paladin","Barbarian","Druid","Assassin","Warlock"];
    internal static void About(Window? owner)
    {
        var form=new Form(owner,"Jamella · Tributo","Editor local de personajes de Diablo II: Resurrected · 0.6.1",600,440);
        form.Body.Children.Add(new TextBlock{Text="Un tributo independiente al editor de Jamella de 2001: árbol de objetos, equipo, inventario y propiedades en una mesa de trabajo. El autor original firmaba como Jamella. Este proyecto no es una continuación oficial ni utiliza su código.",TextWrapping=TextWrapping.Wrap,LineHeight=23});
        form.Body.Children.Add(new TextBlock{Text="Los gráficos y las tablas del juego se leen de tu instalación. El proyecto abierto conserva el nombre técnico AKARA y su carpeta de datos para mantener tus perfiles y copias de seguridad.",TextWrapping=TextWrapping.Wrap,LineHeight=23,Margin=new Thickness(0,16,0,0)});
        var link=new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Código y documentación · Arakiss/akara")){NavigateUri=new Uri("https://github.com/Arakiss/akara")};
        link.RequestNavigate+=(_,e)=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});e.Handled=true;};
        form.Body.Children.Add(new TextBlock(link){Margin=new Thickness(0,18,0,0)});
        form.AcceptButton.Content="Cerrar";form.ShowDialog();
    }
    internal static async Task ChooseCharacterAsync(Window? owner,MainViewModel vm)
    {
        if(!vm.CanChooseSave)return;
        await ((NativeCommand)vm.RefreshCommand).ExecuteAsync(null);
        var choices=vm.Saves.Select(save=>new CharacterChoice(save,
            vm.Profiles.FirstOrDefault(profile=>profile.Id==save.ProfileId)?.Name??"Perfil por elegir",
            Path.GetFileName(Path.GetDirectoryName(save.Path))??"Archivo local")).OrderBy(choice=>choice.Profile).ThenBy(choice=>choice.Save.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
        var form=new Form(owner,"Tus personajes",vm.SavesSummary+". Elige una partida para abrirla; tus cambios se revisan antes de guardar.",760,700);
        form.AcceptButton.Content="Abrir personaje";
        var search=Field(form.Body,"Buscar por nombre o perfil",new TextBox{Margin=new Thickness(0,0,0,12)});
        var list=new ListBox{DisplayMemberPath="Display",FontSize=15,Height=350,ItemsSource=choices,SelectedItem=choices.FirstOrDefault(choice=>choice.Save.Path==vm.SelectedSave?.Path)??choices.FirstOrDefault()};
        form.Body.Children.Add(list);
        var count=new TextBlock{Text=choices.Length+" personajes disponibles",Margin=new Thickness(0,12,0,0)};count.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");form.Body.Children.Add(count);
        search.TextChanged+=(_,_)=>
        {
            var filtered=choices.Where(choice=>choice.Display.Contains(search.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)).ToArray();
            var previous=list.SelectedItem;list.ItemsSource=filtered;list.SelectedItem=filtered.Contains(previous)?previous:filtered.FirstOrDefault();
            count.Text=filtered.Length+" de "+choices.Length+" personajes";
        };
        form.Validate=()=>list.SelectedItem is CharacterChoice?null:"Selecciona un personaje. También puedes abrir un archivo.d2s desde «Abrir».";
        form.Loaded+=(_,_)=>search.Focus();
        if(form.ShowDialog()==true&&list.SelectedItem is CharacterChoice chosen)await vm.OpenSaveAsync(chosen.Save);
    }
    sealed record CharacterChoice(SaveEntry Save,string Profile,string Folder)
    {
        public string Display=>Save.Name+Environment.NewLine+Profile+" · "+Folder;
    }
    internal static async Task CreateAsync(Window? owner,MainViewModel vm)
    {
        var form=new Form(owner,"Un nuevo personaje","Elige su clase y una configuración. La partida se guarda después de revisar los cambios.",650,610);
        var name=Field(form.Body,"Nombre",new TextBox{Text="Nuevo",MaxLength=15});
        var classes=Field(form.Body,"Clase",new ComboBox{ItemsSource=ClassNames.Select(n=>new Choice(n,Names.Class(n))).ToList(),DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue="Sorceress"});
        var presets=Field(form.Body,"Configuración inicial",new ComboBox{DisplayMemberPath="Name"});
        var description=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8)};description.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");form.Body.Children.Add(description);
        void Filter(){string selected=classes.SelectedValue?.ToString()??"Sorceress";var options=vm.Presets.Where(p=>p.ClassName==selected).ToList();presets.ItemsSource=options;presets.SelectedItem=options.FirstOrDefault(p=>p.Id=="squad-sorceress")??options.FirstOrDefault();}
        classes.SelectionChanged+=(_,_)=>Filter();presets.SelectionChanged+=(_,_)=>description.Text=(presets.SelectedItem as PresetEntry)?.Description??"Personaje nuevo sin equipo preparado.";Filter();
        var blank=new CheckBox{Content="Empezar desde nivel 1, sin plantilla"};form.Body.Children.Add(blank);blank.Checked+=(_,_)=>presets.IsEnabled=false;blank.Unchecked+=(_,_)=>presets.IsEnabled=true;
        form.Validate=()=>name.Text.Length<2||name.Text.Any(c=>!(char.IsAsciiLetter(c)||c is '-' or '_'))?"El nombre admite de 2 a 15 letras, guiones y guiones bajos.":null;
        if(form.ShowDialog()==true)await vm.CreateCharacterAsync(new(){Name=name.Text,ClassName=classes.SelectedValue?.ToString()??"Sorceress",ProfileId=vm.SelectedProfile?.Id,Preset=blank.IsChecked==true?null:(presets.SelectedItem as PresetEntry)?.Id});
    }
    internal static async Task CloneAsync(Window? owner,MainViewModel vm)
    {
        var form=new Form(owner,"Clonar personaje","La copia conserva el personaje y su progreso. El original se mantiene guardado.",550,340);var name=Field(form.Body,"Nombre de la copia",new TextBox{Text="Copia",MaxLength=15});form.Validate=()=>name.Text.Length<2?"Escribe un nombre de al menos dos letras.":null;if(form.ShowDialog()==true)await vm.CloneCharacterAsync(name.Text);
    }
    internal static async Task MoveAsync(Window? owner,MainViewModel vm,ItemViewModel item)
    {
        var form=new Form(owner,"Mover objeto",item.Name,570,640);
        var target=Field(form.Body,"Destino",new ComboBox{ItemsSource=new[]{new Choice("character","Personaje"),new Choice("mercenary","Mercenario")},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue="character"});
        var storage=Field(form.Body,"Contenedor",new ComboBox{ItemsSource=new[]{new Choice("inventory","Inventario"),new Choice("stash","Alijo"),new Choice("cube","Cubo horádrico"),new Choice("belt","Cinturón"),new Choice("equipped","Equipo")},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue="stash"});
        string[] legal=item.Data.Array("equipSlots").Select(n=>n!.GetValue<string>()).ToArray();
        var slot=Field(form.Body,"Ranura de equipo",new ComboBox{ItemsSource=legal.Select(s=>new Choice(s,Names.Slot(s))).ToArray(),DisplayMemberPath="Label",SelectedValuePath="Key",SelectedIndex=0});
        var set=Field(form.Body,"Conjunto de armas",new ComboBox{ItemsSource=new[]{"I","II"},SelectedIndex=vm.SelectedWeaponSet-1});
        var automatic=new CheckBox{Content="Buscar espacio automáticamente",IsChecked=true};form.Body.Children.Add(automatic);
        var coordinates=new StackPanel{Orientation=Orientation.Horizontal};var x=new TextBox{Text="0",Width=85};var y=new TextBox{Text="0",Width=85,Margin=new Thickness(12,0,0,0)};coordinates.Children.Add(x);coordinates.Children.Add(y);form.Body.Children.Add(coordinates);coordinates.IsEnabled=false;automatic.Checked+=(_,_)=>coordinates.IsEnabled=false;automatic.Unchecked+=(_,_)=>coordinates.IsEnabled=true;
        target.SelectionChanged+=(_,_)=>{if(target.SelectedValue?.ToString()=="mercenary")storage.SelectedValue="equipped";};
        form.Validate=()=>automatic.IsChecked!=true&&(!int.TryParse(x.Text,out _)||!int.TryParse(y.Text,out _))?"Las coordenadas deben ser números enteros.":null;
        if(form.ShowDialog()==true)await vm.MoveItemAsync(item,new(){Owner=target.SelectedValue?.ToString()??"character",Page=storage.SelectedValue?.ToString()??"stash",Slot=slot.SelectedValue?.ToString(),WeaponSet=set.SelectedIndex+1,X=automatic.IsChecked==true?null:int.Parse(x.Text),Y=automatic.IsChecked==true?null:int.Parse(y.Text)});
    }
    internal static async Task MercenaryAsync(Window? owner,MainViewModel vm)
    {
        try
        {
            var entries=await vm.GetCatalogAsync("hireling");var form=new Form(owner,"Tu mercenario","La identidad y el equipo se conservan al ajustar su nivel.",650,430);
            var current=vm.Snapshot.Obj("mercenary");var type=Field(form.Body,"Especialidad y aura",new ComboBox{ItemsSource=entries,DisplayMemberPath="Name",SelectedItem=entries.FirstOrDefault(e=>e.Data.Int("nativeId")==current.Int("typeId"))??entries.FirstOrDefault()});var level=Field(form.Body,"Nivel",new TextBox{Text=(vm.HasMercenary?current.Int("level"):int.Parse(vm.LevelText)).ToString()});var revive=new CheckBox{Content="Resucitar",IsChecked=true};form.Body.Children.Add(revive);
            form.Validate=()=>!int.TryParse(level.Text,out int n)||n<1?"Escribe un nivel válido.":null;
            if(form.ShowDialog()==true&&type.SelectedItem is CatalogEntry selected)await vm.SetMercenaryAsync(selected.Data.Int("nativeId"),int.Parse(level.Text),revive.IsChecked==true);
        }
        catch(Exception ex){MessageBox.Show(owner,ex.Message,"Mercenario",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    internal static async Task DesignAsync(Window? owner,MainViewModel vm,ItemViewModel? item,ItemDesignRequest? initialDesign=null)
    {
        try
        {
            var design=initialDesign??(item is null?new ItemDesignRequest{BaseCode="amu",Quality="normal",Level=85}:await vm.CreateDesignFromItemAsync(item));
            var bases=await vm.GetCatalogAsync("base",allPages:true);var stats=await vm.GetCatalogAsync("stat",allPages:true);
            var form=new Form(owner,item is null?"Crear una pieza":"Editar pieza",item?.Name??(initialDesign?.RunewordName is not null?"Elige una base compatible. Las runas de la palabra rúnica se colocan automáticamente.":"Bases y propiedades de tu instalación. Los cambios se preparan en el borrador."),930,800);
            var tabs=new TabControl();form.Body.Children.Add(tabs);
            var identity=new StackPanel{Margin=new Thickness(0,14,0,0)};tabs.Items.Add(new TabItem{Header="La pieza",Content=identity});
            var basePicker=Field(identity,"Base",new ComboBox{ItemsSource=bases,DisplayMemberPath="Name",IsEditable=true,IsTextSearchEnabled=true});basePicker.SelectedItem=bases.FirstOrDefault(b=>b.Code==design.BaseCode);
            var quality=Field(identity,"Calidad",new ComboBox{ItemsSource=new[]{new Choice("normal","Normal"),new Choice("inferior","Inferior"),new Choice("superior","Superior"),new Choice("magic","Mágico"),new Choice("rare","Raro"),new Choice("crafted","Artesanal"),new Choice("unique","Único"),new Choice("set","Conjunto"),new Choice("runeword","Palabra rúnica")},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue=design.RunewordName is not null?"runeword":design.Quality});
            var catalogLabel=new TextBlock();var catalog=new ComboBox{DisplayMemberPath="Name",IsEditable=true,IsTextSearchEnabled=true};identity.Children.Add(catalogLabel);identity.Children.Add(catalog);
            async Task ChangeQuality(){string q=quality.SelectedValue?.ToString()??"normal";bool selected=q is "unique" or "set" or "runeword";catalog.Visibility=catalogLabel.Visibility=selected?Visibility.Visible:Visibility.Collapsed;if(selected){catalogLabel.Text=q=="runeword"?"Palabra rúnica":"Objeto del catálogo";catalogLabel.Margin=new Thickness(0,14,0,6);var choices=await vm.GetCatalogAsync(q,allPages:true);catalog.ItemsSource=choices;string? previous=q=="unique"?design.UniqueName:q=="set"?design.SetName:design.RunewordName;catalog.SelectedItem=choices.FirstOrDefault(c=>c.Name==previous);}}
            quality.SelectionChanged+=async(_,_)=>await ChangeQuality();await ChangeQuality();
            catalog.SelectionChanged+=(_,_)=>{if(catalog.SelectedItem is CatalogEntry choice&&quality.SelectedValue?.ToString() is "unique" or "set")basePicker.SelectedItem=bases.FirstOrDefault(b=>b.Code==choice.Code);};
            var level=Field(identity,"Nivel del objeto",new TextBox{Text=design.Level.ToString()});var signature=Field(identity,"Firma del artesano",new TextBox{Text=design.PersonalizedName??"",MaxLength=15});
            var ethereal=new CheckBox{Content="Etéreo",IsChecked=design.Ethereal};var identified=new CheckBox{Content="Identificado",IsChecked=design.Identified};identity.Children.Add(ethereal);identity.Children.Add(identified);
            var affixes=new StackPanel{Margin=new Thickness(0,14,0,0)};tabs.Items.Add(new TabItem{Header="Afijos",Content=affixes});
            var prefixes=await vm.GetCatalogAsync("prefix",allPages:true);var suffixes=await vm.GetCatalogAsync("suffix",allPages:true);
            var prefix=Field(affixes,"Prefijo mágico",new ComboBox{ItemsSource=prefixes,DisplayMemberPath="Name",IsEditable=true,IsTextSearchEnabled=true});prefix.SelectedItem=prefixes.FirstOrDefault(p=>p.Name==design.Prefix);
            var suffix=Field(affixes,"Sufijo mágico",new ComboBox{ItemsSource=suffixes,DisplayMemberPath="Name",IsEditable=true,IsTextSearchEnabled=true});suffix.SelectedItem=suffixes.FirstOrDefault(p=>p.Name==design.Suffix);
            var rarePrefixes=Field(affixes,"Prefijos del objeto raro (IDs separados por coma)",new TextBox{Text=string.Join(", ",design.PrefixIds)});var rareSuffixes=Field(affixes,"Sufijos del objeto raro (IDs separados por coma)",new TextBox{Text=string.Join(", ",design.SuffixIds)});
            var physical=new StackPanel{Margin=new Thickness(0,14,0,0)};tabs.Items.Add(new TabItem{Header="Condición",Content=physical});
            var defense=Field(physical,"Defensa",new TextBox{Text=design.Defense?.ToString()??""});var durability=Field(physical,"Durabilidad actual",new TextBox{Text=design.Durability?.ToString()??""});var maximum=Field(physical,"Durabilidad máxima",new TextBox{Text=design.MaxDurability?.ToString()??""});var quantity=Field(physical,"Cantidad",new TextBox{Text=design.Quantity?.ToString()??""});
            var properties=new StackPanel{Margin=new Thickness(0,14,0,0)};tabs.Items.Add(new TabItem{Header="Propiedades",Content=properties});
            var inheritProperties=new CheckBox{Content="Usar las propiedades originales del catálogo",IsChecked=item is null};properties.Children.Add(inheritProperties);
            var rows=new ObservableCollection<PropertyRow>((design.Stats??[]).Select(s=>new PropertyRow(s,stats.FirstOrDefault(r=>r.Data.Int("nativeId")==s.Id),item?.Stats.FirstOrDefault(r=>r.Id==s.Id&&r.Layer==s.Layer)?.DisplayValue)));
            var propertyPicker=new ComboBox{ItemsSource=stats,DisplayMemberPath="Name",IsEditable=true,IsTextSearchEnabled=true};properties.Children.Add(propertyPicker);
            var add=new Button{Content="Añadir propiedad",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,12)};add.Click+=(_,_)=>{if(propertyPicker.SelectedItem is CatalogEntry entry){inheritProperties.IsChecked=false;rows.Add(new(new(entry.Data.Int("nativeId"),0,0),entry,0));}};properties.Children.Add(add);
            var grid=new DataGrid{ItemsSource=rows,Height=310,CanUserDeleteRows=true};grid.Columns.Add(new DataGridTextColumn{Header="Propiedad",Binding=new Binding("Label"),IsReadOnly=true,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});grid.Columns.Add(new DataGridTextColumn{Header="Valor",Binding=new Binding("Value"){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus},Width=100});grid.Columns.Add(new DataGridTextColumn{Header="Parámetro",Binding=new Binding("Layer"),Width=100});properties.Children.Add(grid);
            grid.IsEnabled=inheritProperties.IsChecked!=true;inheritProperties.Checked+=(_,_)=>grid.IsEnabled=false;inheritProperties.Unchecked+=(_,_)=>grid.IsEnabled=true;
            var remove=new Button{Content="Quitar propiedad elegida",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,0)};remove.Click+=(_,_)=>{if(grid.SelectedItem is PropertyRow row){inheritProperties.IsChecked=false;rows.Remove(row);}};properties.Children.Add(remove);
            quality.SelectionChanged+=(_,_)=>inheritProperties.IsChecked=true;prefix.SelectionChanged+=(_,_)=>inheritProperties.IsChecked=true;suffix.SelectionChanged+=(_,_)=>inheritProperties.IsChecked=true;rarePrefixes.TextChanged+=(_,_)=>inheritProperties.IsChecked=true;rareSuffixes.TextChanged+=(_,_)=>inheritProperties.IsChecked=true;
            catalog.SelectionChanged+=(_,_)=>{if(catalog.SelectedItem is CatalogEntry entry&&entry.Name!=(design.UniqueName??design.SetName??design.RunewordName))inheritProperties.IsChecked=true;};
            var sockets=new StackPanel{Margin=new Thickness(0,14,0,0)};tabs.Items.Add(new TabItem{Header="Engarces",Content=sockets});var count=Field(sockets,"Número de engarces",new TextBox{Text=(design.SocketCount??item?.Sockets.Count??0).ToString()});
            var children=new ObservableCollection<SocketRow>((design.SocketChildren??[]).Select(s=>new SocketRow(s)));var socketGrid=new DataGrid{ItemsSource=children,Height=250};socketGrid.Columns.Add(new DataGridTextColumn{Header="Código de runa, gema o joya",Binding=new Binding("Code"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});sockets.Children.Add(socketGrid);var socketAdd=new Button{Content="Añadir engarce",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,0)};socketAdd.Click+=(_,_)=>children.Add(new(new ItemDesignRequest{BaseCode="r01",Level=1}));sockets.Children.Add(socketAdd);
            void SocketMode(){bool automatic=quality.SelectedValue?.ToString()=="runeword";count.IsEnabled=socketGrid.IsEnabled=socketAdd.IsEnabled=!automatic;if(automatic)count.Text="Automático";else if(!int.TryParse(count.Text,out _))count.Text="0";}quality.SelectionChanged+=(_,_)=>SocketMode();SocketMode();
            var note=new TextBlock{Text="Los valores, capas y engarces se validan antes de entrar en la partida.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,0)};note.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");form.Body.Children.Add(note);
            form.Validate=()=>{try{if(basePicker.SelectedItem is not CatalogEntry&&basePicker.Text.Length==0)return "Elige una base.";if(!int.TryParse(level.Text,out int itemLevel)||itemLevel is <1 or >99)return "El nivel del objeto debe estar entre 1 y 99.";if(quality.SelectedValue?.ToString()!="runeword"&&(!int.TryParse(count.Text,out int socketAmount)||socketAmount<0))return "Escribe un número de engarces válido.";_ =Ids(rarePrefixes.Text);_ =Ids(rareSuffixes.Text);_ =Optional(defense.Text);_ =Optional(durability.Text);_ =Optional(maximum.Text);_ =Optional(quantity.Text);if(inheritProperties.IsChecked!=true)foreach(var row in rows)_=row.ToSpec();return null;}catch(Exception ex)when(ex is FormatException or OverflowException){return "Revisa los valores numéricos.";}};
            if(form.ShowDialog()!=true)return;
            design.BaseCode=(basePicker.SelectedItem as CatalogEntry)?.Code??basePicker.Text.Trim();design.Level=int.Parse(level.Text);string q=quality.SelectedValue?.ToString()??"normal";design.Quality=q=="runeword"?"normal":q;design.UniqueName=q=="unique"?(catalog.SelectedItem as CatalogEntry)?.Name:null;design.SetName=q=="set"?(catalog.SelectedItem as CatalogEntry)?.Name:null;design.RunewordName=q=="runeword"?(catalog.SelectedItem as CatalogEntry)?.Name:null;design.Prefix=(prefix.SelectedItem as CatalogEntry)?.Name;design.Suffix=(suffix.SelectedItem as CatalogEntry)?.Name;design.PrefixIds=q=="magic"?NativeAffixId(prefix):Ids(rarePrefixes.Text);design.SuffixIds=q=="magic"?NativeAffixId(suffix):Ids(rareSuffixes.Text);design.PersonalizedName=signature.Text;design.Ethereal=ethereal.IsChecked==true;design.Identified=identified.IsChecked==true;design.Defense=Optional(defense.Text);design.Durability=Optional(durability.Text);design.MaxDurability=Optional(maximum.Text);design.Quantity=Optional(quantity.Text);design.Stats=inheritProperties.IsChecked==true?null:rows.Select(r=>r.ToSpec()).ToList();
            if(q!="runeword"||inheritProperties.IsChecked==true)design.RunewordStats=null;if(q!="set"||inheritProperties.IsChecked==true){design.SetBonusMask=null;design.SetBonusLists=null;}
            if(design.RunewordName is null){int socketCount=int.Parse(count.Text);design.SocketCount=socketCount;design.SocketChildren=children.Take(socketCount).Select(r=>r.ToDesign()).ToList();}else{design.SocketCount=null;design.SocketChildren=[];}
            await vm.ApplyDesignAsync(design,item,item is null?new ItemDestination{Page="stash"}:null);
        }
        catch(Exception ex){MessageBox.Show(owner,ex.Message,"El taller",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    static int? Optional(string value)=>string.IsNullOrWhiteSpace(value)?null:int.Parse(value);
    static int[] NativeAffixId(ComboBox picker)=>picker.SelectedItem is CatalogEntry entry&&entry.Data.Int("nativeId") is >0 and var id?[id]:[];
    static int[] Ids(string text)=>text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
    static T Field<T>(Panel panel,string label,T control)where T:Control{var text=new TextBlock{Text=label,Margin=new Thickness(0,13,0,6)};text.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");panel.Children.Add(text);panel.Children.Add(control);return control;}
    record Choice(string Key,string Label);
    sealed class Form:Window
    {
        public StackPanel Body{get;}=new();public Func<string?>? Validate{get;set;}
        public Button AcceptButton{get;}=new(){Content="Preparar cambios",MinWidth=150,IsDefault=true};
        public Form(Window? owner,string title,string description,double width,double height)
        {
            Title=title;Width=width;Height=height;MinWidth=480;MinHeight=300;WindowStartupLocation=owner is null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;Owner=owner;ShowInTaskbar=false;ResizeMode=ResizeMode.CanResize;
            FontFamily=new("Segoe UI");FontSize=13;SetResourceReference(ForegroundProperty,"TextBrush");SetResourceReference(BackgroundProperty,"WindowBrush");
            var layout=new Grid{Margin=new Thickness(27)};layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
            var header=new StackPanel{Margin=new Thickness(0,0,0,18)};var heading=new TextBlock{Text=title,FontSize=22};heading.SetResourceReference(TextBlock.FontFamilyProperty,"DisplayFont");header.Children.Add(heading);var subtitle=new TextBlock{Text=description,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,9,0,0)};subtitle.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");header.Children.Add(subtitle);layout.Children.Add(header);
            var scroll=new ScrollViewer{Content=Body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);layout.Children.Add(scroll);
            var footer=new StackPanel{Margin=new Thickness(0,20,0,0)};Grid.SetRow(footer,2);var error=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};error.SetResourceReference(TextBlock.ForegroundProperty,"ErrorBrush");footer.Children.Add(error);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=new Button{Content="Cancelar",IsCancel=true,MinWidth=100,Margin=new Thickness(0,0,9,0)};var apply=AcceptButton;apply.SetResourceReference(StyleProperty,"PrimaryButton");apply.Click+=(_,_)=>{error.Text=Validate?.Invoke()??"";if(error.Text.Length==0)DialogResult=true;};actions.Children.Add(cancel);actions.Children.Add(apply);footer.Children.Add(actions);layout.Children.Add(footer);Content=layout;
        }
    }
    sealed class PropertyRow
    {
        readonly ItemStatSpec original;readonly string originalValue;
        public int Id{get;}public string Label{get;}public string Value{get;set;}public int Layer{get;set;}
        public PropertyRow(ItemStatSpec source,CatalogEntry? metadata,double? human){original=source;Id=source.Id;Layer=source.Layer;Label=metadata?.Name??"Propiedad "+source.Id;double v=human??source.Value??source.RawValue??0;Value=v.ToString("0.########",CultureInfo.GetCultureInfo("es-ES"));originalValue=Value;}
        public ItemStatSpec ToSpec()=>Value==originalValue&&Layer==original.Layer?original:new(Id,Layer,ParseValue(Value));
        static double ParseValue(string text)=>double.TryParse(text,NumberStyles.Float,CultureInfo.GetCultureInfo("es-ES"),out double value)?value:double.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
    }
    sealed class SocketRow(ItemDesignRequest? source)
    {
        public string Code{get;set;}=source?.BaseCode??"";
        public ItemDesignRequest? ToDesign(){if(Code.Length==0)return null;if(source is not null){source.BaseCode=Code;return source;}return new(){BaseCode=Code,Level=1};}
    }
}
