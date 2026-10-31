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
        double center=MainPane.ActualWidth>0?MainPane.ActualWidth:Math.Max(520,ActualWidth-(objectContext?(catalogExpanded?catalogWidth+5:0)+inspectorWidth+5:0));
        bool wide=center>=950,compact=center<700;
        EquipmentDoll.Width=compact?240:wide?330:280;EquipmentDoll.Height=compact?290:wide?390:330;
        InventoryCells.CellSize=compact?28:center>=1350?40:wide?36:32;
        ContainerViewport.MaxWidth=Math.Max(280,center-EquipmentDoll.Width-48);
        ContainerViewport.MaxHeight=Math.Max(280,Math.Min(410,ActualHeight-390));
        bool showStash=center>=1450&&attached?.SelectedView=="equipment"&&attached.SelectedStorage!="stash";
        ExtraStashPane.Visibility=showStash?Visibility.Visible:Visibility.Collapsed;
        StashCanvasColumn.Width=showStash?new GridLength(1,GridUnitType.Star):new GridLength(0);
        var inventoryColumn=EquipmentCanvas.ColumnDefinitions[1];
        inventoryColumn.Width=showStash?new GridLength(420):new GridLength(1,GridUnitType.Star);
        ExtraStashScroll.MaxHeight=Math.Max(280,Math.Min(650,ActualHeight-430));
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
    void OnSizeChanged(object sender,SizeChangedEventArgs e)=>UpdateCanvasScale();
    void OnWorkspaceSizeChanged(object sender,SizeChangedEventArgs e)=>UpdateCanvasScale();
}
