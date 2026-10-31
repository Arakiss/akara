using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Akara.Desktop.ViewModels;
using ForjaHoradrica;

namespace Akara.Desktop;

public partial class WorkbenchView : UserControl
{
    MainViewModel? attached;
    public WorkbenchView() => InitializeComponent();
    void OnDataContextChanged(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(attached is not null){attached.RequestCreate-=Create;attached.RequestClone-=Clone;attached.RequestMove-=Move;attached.RequestDesign-=Design;attached.RequestCatalogDesign-=DesignCatalog;}
        attached=e.NewValue as MainViewModel;
        if(attached is not null){attached.RequestCreate+=Create;attached.RequestClone+=Clone;attached.RequestMove+=Move;attached.RequestDesign+=Design;attached.RequestCatalogDesign+=DesignCatalog;}
    }
    async void Create(){if(attached is not null)await NativeDialogs.CreateAsync(Window.GetWindow(this),attached);}
    async void Clone(){if(attached is not null)await NativeDialogs.CloneAsync(Window.GetWindow(this),attached);}
    async void Move(ItemViewModel item){if(attached is not null)await NativeDialogs.MoveAsync(Window.GetWindow(this),attached,item);}
    async void Design(ItemViewModel? item){if(attached is not null)await NativeDialogs.DesignAsync(Window.GetWindow(this),attached,item);}
    async void DesignCatalog(CatalogEntry entry){if(attached is not null)await NativeDialogs.DesignAsync(Window.GetWindow(this),attached,null,new ItemDesignRequest{BaseCode="",Quality="normal",RunewordName=entry.Name,Level=85});}
    void OnItemInvoked(object? sender,EventArgs e){if(attached?.SelectedItem is { } item)Design(item);}
    async void OnMercenary(object sender,RoutedEventArgs e){if(attached is not null)await NativeDialogs.MercenaryAsync(Window.GetWindow(this),attached);}
    async void OnAddProfile(object sender,RoutedEventArgs e){if(attached is null)return;var dialog=new Microsoft.Win32.OpenFolderDialog{Title="Carpeta de tablas del juego o del mod"};if(dialog.ShowDialog()==true)await attached.RegisterProfileAsync(dialog.FolderName);}
    void OnTitleDrag(object sender,MouseButtonEventArgs e){if(Window.GetWindow(this) is not { } window)return;if(e.ClickCount==2){window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;return;}try{window.DragMove();}catch(InvalidOperationException){}}
    void OnMinimize(object sender,RoutedEventArgs e){if(Window.GetWindow(this) is { } window)SystemCommands.MinimizeWindow(window);}
    void OnMaximize(object sender,RoutedEventArgs e){if(Window.GetWindow(this) is not { } window)return;if(window.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(window);else SystemCommands.MaximizeWindow(window);}
    void OnClose(object sender,RoutedEventArgs e){Window.GetWindow(this)?.Close();}
    void OnSizeChanged(object sender,SizeChangedEventArgs e)
    {
        if(EquipmentDoll is null||InventoryCells is null)return;
        EquipmentDoll.Height=500;
        InventoryCells.CellSize=40;
        double inspector=e.NewSize.Width>1804?360:314;
        InspectorColumn.Width=new GridLength(inspector);
        double gutter=Math.Max(0,(e.NewSize.Width-190-inspector-1300)/2);
        LeadingGutter.Width=TrailingGutter.Width=new GridLength(gutter);
    }
}
