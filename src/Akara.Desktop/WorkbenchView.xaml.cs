using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Akara.Desktop.Controls;
using Akara.Desktop.ViewModels;
using ForjaHoradrica;

namespace Akara.Desktop;

public partial class WorkbenchView : UserControl
{
    MainViewModel? attached;
    bool objectContext,catalogExpanded=true,skillsQueued,catalogInitialized,catalogQueued;
    double catalogWidth=240,inspectorWidth=370;
    readonly CatalogBranch[] catalogBranches=[new("unique","Únicos"),new("set","Conjuntos"),new("runeword","Palabras rúnicas"),new("base","Bases")];
    CatalogBranch? activeBranch;
    ICollectionView? welcomeView;
    double presentationScale=1;
    double? requestedScale;
    public double AppearanceScale=>presentationScale;
    public WorkbenchView()
    {
        InitializeComponent();
        CatalogTree.ItemsSource=catalogBranches;
        CatalogTree.AddHandler(TreeViewItem.ExpandedEvent,new RoutedEventHandler(OnCatalogExpanded));
    }
    void OnDataContextChanged(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(attached is not null)
        {
            attached.RequestCreate-=Create;attached.RequestClone-=Clone;attached.RequestMove-=Move;attached.RequestDesign-=Design;attached.RequestCatalogDesign-=DesignCatalog;
            attached.PropertyChanged-=OnViewModelChanged;attached.Skills.CollectionChanged-=OnSkillsChanged;
        }
        attached=e.NewValue as MainViewModel;
        catalogInitialized=false;
        if(welcomeView is not null)welcomeView.Filter=null;
        welcomeView=null;
        ResetCatalogBranches();
        catalogInitialized=attached?.CatalogEntries.Count>0;
        if(attached is not null)
        {
            attached.RequestCreate+=Create;attached.RequestClone+=Clone;attached.RequestMove+=Move;attached.RequestDesign+=Design;attached.RequestCatalogDesign+=DesignCatalog;
            attached.PropertyChanged+=OnViewModelChanged;attached.Skills.CollectionChanged+=OnSkillsChanged;
            welcomeView=CollectionViewSource.GetDefaultView(attached.Saves);
            welcomeView.Filter=WelcomeMatches;
            WelcomeSavesList.SelectedItem=attached.Saves.FirstOrDefault(save=>save.ProfileId is not null)??attached.Saves.FirstOrDefault();
        }
        UpdateContext();RefreshSkillColumns();RefreshInspector();
    }
    void OnViewModelChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName is nameof(MainViewModel.SelectedView) or nameof(MainViewModel.HasCharacter))UpdateContext();
        if(e.PropertyName==nameof(MainViewModel.SelectedStorage))UpdateCanvasScale();
        if(e.PropertyName==nameof(MainViewModel.SelectedItem))RefreshInspector();
        if(e.PropertyName==nameof(MainViewModel.SourceHash)){catalogInitialized=false;ResetCatalogBranches();}
        if(e.PropertyName is nameof(MainViewModel.IsBusy) or nameof(MainViewModel.SourceHash))QueueCatalogLoad();
    }
    bool WelcomeMatches(object value)=>value is SaveEntry save&&(WelcomeSearch.Text.Trim() is not {Length:>0} query||save.Name.Contains(query,StringComparison.CurrentCultureIgnoreCase)||save.FolderLabel.Contains(query,StringComparison.CurrentCultureIgnoreCase));
    void OnWelcomeSearchChanged(object sender,TextChangedEventArgs e)
    {
        welcomeView?.Refresh();
        if(WelcomeSavesList is not null&&WelcomeSavesList.SelectedItem is null)WelcomeSavesList.SelectedIndex=0;
    }
    async void OnWelcomeDoubleClick(object sender,MouseButtonEventArgs e)
    {
        if(attached is null||e.OriginalSource is not DependencyObject source||ItemsControl.ContainerFromElement(WelcomeSavesList,source) is not ListBoxItem||WelcomeSavesList.SelectedItem is not SaveEntry save)return;
        await attached.OpenSaveAsync(save);
    }
    void RefreshInspector()
    {
        if(InspectorCardHost is null)return;
        InspectorCardHost.Content=attached?.SelectedItem is { } item?new ItemTooltipCard(item){Width=double.NaN,MaxHeight=double.PositiveInfinity,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(0),Background=FindResource("InsetBrush") as Brush}:null;
    }
    void ResetCatalogBranches()
    {
        foreach(var branch in catalogBranches){branch.Entries=[];branch.IsExpanded=false;}
        activeBranch=catalogBranches.FirstOrDefault(branch=>branch.Key==attached?.CatalogType)??catalogBranches[0];
        activeBranch.Entries=attached?.CatalogEntries??[];activeBranch.IsExpanded=true;
    }
    async void OnCatalogNodeSelected(object sender,RoutedPropertyChangedEventArgs<object> e)
    {
        if(attached is null)return;
        if(e.NewValue is CatalogEntry entry)attached.SelectedCatalogEntry=entry;
        else if(e.NewValue is CatalogBranch branch)await SelectCatalogBranchAsync(branch);
    }
    async void OnCatalogExpanded(object sender,RoutedEventArgs e)
    {
        if(e.OriginalSource is TreeViewItem{DataContext:CatalogBranch branch})await SelectCatalogBranchAsync(branch);
    }
    async Task SelectCatalogBranchAsync(CatalogBranch branch)
    {
        if(attached is null||attached.IsBusy||branch==activeBranch)return;
        if(activeBranch is not null){activeBranch.Entries=attached.CatalogEntries.ToArray();activeBranch.IsExpanded=false;}
        activeBranch=branch;branch.IsExpanded=true;branch.Entries=attached.CatalogEntries;
        attached.SelectedCatalogEntry=null;catalogInitialized=true;
        await attached.SearchCatalogAsync(branch.Key);
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
        CatalogSplitterColumn.Width=new GridLength(next&&catalogExpanded?4:0);
        InspectorColumn.MinWidth=next?290:0;InspectorColumn.Width=new GridLength(next?Math.Max(290,inspectorWidth):0);InspectorSplitterColumn.Width=new GridLength(next?4:0);
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
        double center=MainPane.ActualWidth>0?MainPane.ActualWidth:Math.Max(470,ActualWidth/presentationScale-(catalogExpanded?catalogWidth+4:0)-inspectorWidth-32);
        double paneHeight=MainPane.ActualHeight>0?MainPane.ActualHeight:ActualHeight/presentationScale-172;
        double dollWidth=Math.Clamp((center-40)*.43,195,386);
        double availableInventory=Math.Max(210,center-dollWidth-42);
        bool wide=center>=1450&&attached?.SelectedStorage!="stash";
        WideStashPane.Visibility=wide?Visibility.Visible:Visibility.Collapsed;
        EquipmentCanvas.ColumnDefinitions[1].Width=wide?new GridLength((attached?.GridColumns??10)*48+18):new GridLength(1,GridUnitType.Star);
        EquipmentCanvas.ColumnDefinitions[2].Width=wide?new GridLength(1,GridUnitType.Star):new GridLength(0);
        if(wide)availableInventory=(attached?.GridColumns??10)*48;
        double maxCanvasHeight=Math.Max(270,Math.Min(440,paneHeight-245-(ObjectListExpander?.IsExpanded==true?170:0)));
        EquipmentDoll.Width=dollWidth;EquipmentDoll.Height=Math.Min(maxCanvasHeight,dollWidth*1.14);
        InventoryCells.CellSize=48;
        ContainerViewport.MaxWidth=availableInventory;
        ContainerViewport.MaxHeight=Math.Max(180,maxCanvasHeight-72);
        WideStashViewport.MaxHeight=ContainerViewport.MaxHeight;
        double inventoryHeight=Math.Min(ContainerViewport.MaxHeight,(attached?.GridRows??8)*48*Math.Min(1,availableInventory/((attached?.GridColumns??10)*48)));
        ArmoryRow.Height=new GridLength(Math.Max(EquipmentDoll.Height+54,inventoryHeight+116));
    }
    void OnObjectListToggled(object sender,RoutedEventArgs e)=>UpdateCanvasScale();
    void OnStashDoubleClick(object sender,MouseButtonEventArgs e)
    {
        if(e.OriginalSource is DependencyObject source&&ItemsControl.ContainerFromElement(StashTray,source) is ListBoxItem&&attached?.SelectedItem is { } item)Design(item);
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
    static void SizeItemTable(DataGrid table)
    {
        if(table.ActualWidth<=0||table.Columns.Count!=3)return;
        double nameWidth=Math.Max(160,table.ActualWidth-table.BorderThickness.Left-table.BorderThickness.Right-220-SystemParameters.VerticalScrollBarWidth);
        table.Columns[0].Width=new DataGridLength(nameWidth,DataGridLengthUnitType.Pixel);
        table.Columns[1].Width=new DataGridLength(130,DataGridLengthUnitType.Pixel);
        table.Columns[2].Width=new DataGridLength(90,DataGridLengthUnitType.Pixel);
    }
    void OnItemTableLoaded(object sender,RoutedEventArgs e)=>SizeItemTable((DataGrid)sender);
    void OnItemTableSizeChanged(object sender,SizeChangedEventArgs e)=>SizeItemTable((DataGrid)sender);
    void OnItemTableVisibleChanged(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(e.NewValue is true)Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>SizeItemTable((DataGrid)sender)));
    }
    async void OnMercenary(object sender,RoutedEventArgs e){if(attached is not null)await NativeDialogs.MercenaryAsync(Window.GetWindow(this),attached);}
    async void OnChooseSave(object sender,RoutedEventArgs e){if(attached is not null)await NativeDialogs.ChooseCharacterAsync(Window.GetWindow(this),attached);}
    async void OnAddProfile(object sender,RoutedEventArgs e){if(attached is null)return;var dialog=new Microsoft.Win32.OpenFolderDialog{Title="Carpeta de tablas del juego o del mod"};if(dialog.ShowDialog()==true)await attached.RegisterProfileAsync(dialog.FolderName);}
    void OnTitleDrag(object sender,MouseButtonEventArgs e){for(DependencyObject? source=e.OriginalSource as DependencyObject;source is not null&&source!=sender;source=source is Visual?VisualTreeHelper.GetParent(source):null)if(source is ButtonBase or MenuItem)return;if(Window.GetWindow(this) is not { } window)return;if(e.ClickCount==2){window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;return;}try{window.DragMove();}catch(InvalidOperationException){}}
    void OnAbout(object sender,RoutedEventArgs e)=>NativeDialogs.About(Window.GetWindow(this));
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
