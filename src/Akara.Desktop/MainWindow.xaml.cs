using System.Windows;
using System.ComponentModel;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing+=OnClosing;
        StateChanged+=OnWindowStateChanged;
    }
    void OnWindowStateChanged(object? sender,EventArgs e)
    {
        bool maximized=WindowState==WindowState.Maximized;
        MaxHeight=maximized?SystemParameters.WorkArea.Height:double.PositiveInfinity;
        MaxWidth=maximized?SystemParameters.WorkArea.Width:double.PositiveInfinity;
    }
    internal void RefreshLanguage()
    {
        Workbench.DataContext = null;
        Workbench = new WorkbenchView();
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
        Content = Workbench;
    }
    void OnClosing(object? sender,CancelEventArgs e)
    {
        if(DataContext is not MainViewModel vm)return;
        if(vm.IsBusy){e.Cancel=true;MessageBox.Show(this,L.T("Espera a que termine la operación antes de cerrar Jamella."),L.T("Operación en curso"),MessageBoxButton.OK,MessageBoxImage.Information);return;}
        if(!vm.IsSyntheticDemo&&(vm.IsDirty||vm.IsNew||vm.HasInputErrors)&&MessageBox.Show(this,L.T("Hay cambios pendientes en el borrador. ¿Quieres cerrar Jamella y descartarlos?"),L.T("Cerrar Jamella"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)e.Cancel=true;
    }
}
