using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Akara.Desktop.ViewModels;
using ForjaHoradrica;

namespace Akara.Desktop;

public partial class WorkbenchView : UserControl
{
    MainViewModel? attached;
    bool objectContext,catalogExpanded=true,skillsQueued,catalogInitialized,catalogQueued;
    double catalogWidth=235,inspectorWidth=300;
    double presentationScale=1;
    double? requestedScale;
    public double AppearanceScale=>presentationScale;
    public WorkbenchView()=>InitializeComponent();
    void OnDataContextChanged(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(attached is not null)
        {
            attached.RequestCreate-=Create;attached.RequestClone-=Clone;attached.RequestMove-=Move;attached.RequestDesign-=Design;attached.RequestCatalogDesign-=DesignCatalog;
            attached.PropertyChanged-=OnViewModelChanged;attached.Skills.CollectionChanged-=OnSkillsChanged;
        }
        attached=e.NewValue as MainViewModel;
        catalogInitialized=false;
        if(attached is not null)
        {
            attached.RequestCreate+=Create;attached.RequestClone+=Clone;attached.RequestMove+=Move;attached.RequestDesign+=Design;attached.RequestCatalogDesign+=DesignCatalog;
            attached.PropertyChanged+=OnViewModelChanged;attached.Skills.CollectionChanged+=OnSkillsChanged;
        }
        UpdateContext();RefreshSkillColumns();
    }
    void OnViewModelChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName is nameof(MainViewModel.SelectedView) or nameof(MainViewModel.HasCharacter))UpdateContext();
        if(e.PropertyName==nameof(MainViewModel.SelectedStorage))UpdateCanvasScale();
        if(e.PropertyName==nameof(MainViewModel.SourceHash))catalogInitialized=false;
        if(e.PropertyName is nameof(MainViewModel.IsBusy) or nameof(MainViewModel.SourceHash))QueueCatalogLoad();
    }
    void OnSkillsChanged(object? sender,NotifyCollectionChangedEventArgs e)
    {
        if(skillsQueued)return;skillsQueued=true;
        Dispatcher.BeginInvoke(DispatcherPriority.DataBind,new Action(()=>{skillsQueued=false;RefreshSkillColumns();}));
    }
    void RefreshSkillColumns()
    {
        if(SkillColumnsControl is null)return;
        SkillColumnsControl.ItemsSource=attached?.Skills.GroupBy(skill=>skill.TabId).OrderBy(group=>group.Key)
            .Select((group,index)=>new SkillColumn(SkillTreeTitle(attached.ClassName,group.Key,index),group.OrderBy(skill=>skill.RequiredLevel).ThenBy(skill=>skill.Row).ThenBy(skill=>skill.Column).ToArray())).ToArray();
    }
    // These skillpage values were checked against the installed skilldesc
    // tables. Unknown classes/pages keep an explicit neutral numbered title.
    static string SkillTreeTitle(string className,int page,int index)=>(className,page) switch
    {
        ("Sorceress",1)=>"Fuego",("Sorceress",2)=>"Rayos",("Sorceress",3)=>"Frío",
        ("Amazon",1)=>"Arco y ballesta",("Amazon",2)=>"Pasivas y magia",("Amazon",3)=>"Jabalina y lanza",
        ("Necromancer",1)=>"Maldiciones",("Necromancer",2)=>"Veneno y hueso",("Necromancer",3)=>"Invocación",
        ("Paladin",1)=>"Combate",("Paladin",2)=>"Auras ofensivas",("Paladin",3)=>"Auras defensivas",
        ("Barbarian",1)=>"Combate",("Barbarian",2)=>"Dominios",("Barbarian",3)=>"Gritos de guerra",
        ("Druid",1)=>"Invocación",("Druid",2)=>"Cambio de forma",("Druid",3)=>"Elementales",
        ("Assassin",1)=>"Trampas",("Assassin",2)=>"Disciplinas sombrías",("Assassin",3)=>"Artes marciales",
        _=>"Árbol "+(index+1)
    };
    sealed record SkillColumn(string Title,IReadOnlyList<SkillViewModel> Skills);
    void UpdateContext()
    {
        if(MainPane is null)return;
        bool next=attached?.HasCharacter==true&&attached.SelectedView is "equipment" or "mercenary" or "workshop";
        if(objectContext&&!next){if(CatalogColumn.Width.Value>0)catalogWidth=CatalogColumn.ActualWidth;inspectorWidth=InspectorColumn.ActualWidth;}
        objectContext=next;
        CatalogPane.Visibility=CatalogSplitter.Visibility=next&&catalogExpanded?Visibility.Visible:Visibility.Collapsed;
        InspectorPane.Visibility=InspectorSplitter.Visibility=CatalogToggle.Visibility=next?Visibility.Visible:Visibility.Collapsed;
        CatalogColumn.MinWidth=next&&catalogExpanded?190:0;CatalogColumn.Width=new GridLength(next&&catalogExpanded?Math.Max(190,catalogWidth):0);
        CatalogSplitterColumn.Width=new GridLength(next&&catalogExpanded?5:0);
        InspectorColumn.MinWidth=next?260:0;InspectorColumn.Width=new GridLength(next?Math.Max(260,inspectorWidth):0);InspectorSplitterColumn.Width=new GridLength(next?5:0);
        Grid.SetColumn(MainPane,next?2:0);Grid.SetColumnSpan(MainPane,next?1:5);
        UpdateCanvasScale();QueueCatalogLoad();
    }
    void QueueCatalogLoad()
    {
        if(!objectContext||catalogInitialized||catalogQueued||attached is null||attached.IsBusy||attached.IsSyntheticDemo)return;
        catalogQueued=true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>
        {
            catalogQueued=false;
            if(!objectContext||attached is null||!attached.SearchCatalogCommand.CanExecute(null))return;
            catalogInitialized=true;attached.SearchCatalogCommand.Execute(null);
        }));
    }
    void UpdateCanvasScale()
    {
        if(EquipmentDoll is null||InventoryCells is null)return;
        double logicalHeight=ActualHeight/presentationScale;
        double center=MainPane.ActualWidth>0?MainPane.ActualWidth:Math.Max(520,ActualWidth/presentationScale-(objectContext?(catalogExpanded?catalogWidth+5:0)+inspectorWidth+5:0));
        bool wide=center>=950,compact=center<700;
        EquipmentDoll.Width=compact?240:wide?330:280;
        EquipmentDoll.Height=Math.Min(compact?290:wide?420:330,Math.Max(240,logicalHeight-580));
        double targetCell=center>=1350?48:wide?44:compact?28:32;
        InventoryCells.CellSize=Math.Min(targetCell,Math.Max(28,Math.Floor((logicalHeight-520)/Math.Max(1,attached?.GridRows??8))));
        ContainerViewport.MaxWidth=Math.Max(280,center-EquipmentDoll.Width-48);
        ContainerViewport.MaxHeight=Math.Max(220,Math.Min(410,logicalHeight-520));
        bool showStash=center>=1450&&attached?.SelectedView=="equipment"&&attached.SelectedStorage!="stash";
        ExtraStashPane.Visibility=showStash?Visibility.Visible:Visibility.Collapsed;
        StashCanvasColumn.Width=showStash?new GridLength(1,GridUnitType.Star):new GridLength(0);
        var inventoryColumn=EquipmentCanvas.ColumnDefinitions[1];
        inventoryColumn.Width=showStash?new GridLength(Math.Max(360,(attached?.GridColumns??10)*InventoryCells.CellSize+12)):new GridLength(1,GridUnitType.Star);
        ExtraStashScroll.MaxHeight=Math.Max(220,Math.Min(650,logicalHeight-520));
    }
    void OnToggleCatalog(object sender,RoutedEventArgs e)
    {
        if(catalogExpanded&&CatalogColumn.ActualWidth>0)catalogWidth=CatalogColumn.ActualWidth;
        catalogExpanded=!catalogExpanded;UpdateContext();
    }
    async void Create(){if(attached is not null)await NativeDialogs.CreateAsync(Window.GetWindow(this),attached);}
    async void Clone(){if(attached is not null)await NativeDialogs.CloneAsync(Window.GetWindow(this),attached);}
    async void Move(ItemViewModel item){if(attached is not null)await NativeDialogs.MoveAsync(Window.GetWindow(this),attached,item);}
    async void Design(ItemViewModel? item){if(attached is not null)await NativeDialogs.DesignAsync(Window.GetWindow(this),attached,item);}
    async void DesignCatalog(CatalogEntry entry){if(attached is not null)await NativeDialogs.DesignAsync(Window.GetWindow(this),attached,null,new ItemDesignRequest{BaseCode="",Quality="normal",RunewordName=entry.Name,Level=85});}
    void OnItemInvoked(object? sender,EventArgs e){if(attached?.SelectedItem is { } item)Design(item);}
    void OnItemTableInvoked(object sender,MouseButtonEventArgs e){if(sender is ItemsControl table&&e.OriginalSource is DependencyObject source&&ItemsControl.ContainerFromElement(table,source) is DataGridRow&&attached?.SelectedItem is { } item)Design(item);}
    async void OnMercenary(object sender,RoutedEventArgs e){if(attached is not null)await NativeDialogs.MercenaryAsync(Window.GetWindow(this),attached);}
    async void OnAddProfile(object sender,RoutedEventArgs e){if(attached is null)return;var dialog=new Microsoft.Win32.OpenFolderDialog{Title="Carpeta de tablas del juego o del mod"};if(dialog.ShowDialog()==true)await attached.RegisterProfileAsync(dialog.FolderName);}
    void OnTitleDrag(object sender,MouseButtonEventArgs e){if(Window.GetWindow(this) is not { } window)return;if(e.ClickCount==2){window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;return;}try{window.DragMove();}catch(InvalidOperationException){}}
    void OnMinimize(object sender,RoutedEventArgs e){if(Window.GetWindow(this) is { } window)SystemCommands.MinimizeWindow(window);}
    void OnMaximize(object sender,RoutedEventArgs e){if(Window.GetWindow(this) is not { } window)return;if(window.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(window);else SystemCommands.MaximizeWindow(window);}
    void OnClose(object sender,RoutedEventArgs e){Window.GetWindow(this)?.Close();}
    void ApplyAppearanceScale()
    {
        if(DesignSurface is null||AppearanceScaleTransform is null)return;
        if(Window.GetWindow(this) is { } window)
        {
            var area=SystemParameters.WorkArea;
            foreach(var choice in AppearanceScalePicker.Items.OfType<ComboBoxItem>())
                if(double.TryParse(choice.Tag?.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double scale))
                    choice.IsEnabled=1160*scale<=area.Width&&800*scale<=area.Height;
            if(requestedScale is { } requested&&(1160*requested>area.Width||800*requested>area.Height))
            { requestedScale=null;AppearanceScalePicker.SelectedIndex=0; }
            double minimumScale=requestedScale??1;
            window.MinWidth=1160*minimumScale;window.MinHeight=800*minimumScale;
            if(requestedScale.HasValue&&window.WindowState==WindowState.Normal)
            {
                window.Width=Math.Min(area.Width,Math.Max(window.ActualWidth,window.MinWidth));
                window.Height=Math.Min(area.Height,Math.Max(window.ActualHeight,window.MinHeight));
                if(!double.IsNaN(window.Left))window.Left=Math.Clamp(window.Left,area.Left,area.Right-window.Width);
                if(!double.IsNaN(window.Top))window.Top=Math.Clamp(window.Top,area.Top,area.Bottom-window.Height);
            }
        }
        double next=requestedScale??(ActualWidth>=2600&&ActualHeight>=1200?1.5:ActualWidth>=2200&&ActualHeight>=1000?1.25:1);
        if(Math.Abs(next-presentationScale)<.001)return;
        presentationScale=next;AppearanceScaleTransform.ScaleX=AppearanceScaleTransform.ScaleY=next;
        UpdateContext();
    }
    void OnAppearanceScaleChanged(object sender,SelectionChangedEventArgs e)
    {
        if(sender is not ComboBox{SelectedItem:ComboBoxItem item})return;
        requestedScale=double.TryParse(item.Tag?.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double value)?value:null;
        ApplyAppearanceScale();
    }
    void OnSizeChanged(object sender,SizeChangedEventArgs e){ApplyAppearanceScale();UpdateCanvasScale();}
    void OnWorkspaceSizeChanged(object sender,SizeChangedEventArgs e)=>UpdateCanvasScale();
}
