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
    }
    void OnClosing(object? sender,CancelEventArgs e)
    {
        if(DataContext is not MainViewModel vm)return;
        if(vm.IsBusy){e.Cancel=true;MessageBox.Show(this,"Espera a que termine la operación antes de cerrar AKARA.","Operación en curso",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        if(!vm.IsSyntheticDemo&&(vm.IsDirty||vm.IsNew)&&MessageBox.Show(this,"Hay cambios pendientes en el borrador. ¿Quieres cerrar AKARA y descartarlos?","Cerrar AKARA",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)e.Cancel=true;
    }
}
