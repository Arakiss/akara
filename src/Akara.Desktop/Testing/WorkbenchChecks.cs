using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Akara.Desktop.Controls;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Testing;

/// <summary>Exercises the production controls offscreen, without desktop input or live saves.</summary>
internal static class WorkbenchChecks
{
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Layout(WorkbenchView view,double width=1280,double height=900)
    {
        view.Width=width;view.Height=height;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();
    }
    static async Task Idle(MainViewModel vm)
    {
        for(int attempt=0;attempt<200;attempt++)
        {
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            if(!vm.IsBusy)return;
            await Task.Delay(50);
        }
        throw new InvalidOperationException("The workbench remained busy.");
    }
    internal static async Task WelcomeAsync(MainViewModel vm,List<string> checks)
    {
        var view=new WorkbenchView{DataContext=vm};Layout(view);
        try
        {
            var search=(TextBox)view.FindName("WelcomeSearch");var list=(ListBox)view.FindName("WelcomeSavesList");
            Require(list.Items.Count==vm.Saves.Count&&list.Items.Count>0,"The welcome screen omitted discovered characters.");
            search.Text="NoSuchCharacter-Workbench";await Dispatcher.Yield(DispatcherPriority.DataBind);
            Require(list.Items.Count==0&&vm.Saves.Count>0,"Welcome filtering mutated discovery or failed to filter.");
            search.Text="";await Dispatcher.Yield(DispatcherPriority.DataBind);
            var save=vm.Saves[0];list.SelectedItem=save;
            var open=Descendants<Button>(view).Single(button=>button.Content?.ToString()=="Abrir personaje");
            Require(ReferenceEquals(open.Command,vm.OpenSaveCommand)&&ReferenceEquals(open.CommandParameter,save)&&open.Command.CanExecute(save),"The welcome action was not bound to its selected character.");
            await ((NativeCommand)open.Command).ExecuteAsync(open.CommandParameter);await Idle(vm);
            Require(vm.HasCharacter&&vm.SelectedSave?.Path==save.Path&&vm.ErrorText.Length==0,"Opening a welcome row did not synchronize the accepted character.");
            checks.Add("native-welcome-filters-and-opens-the-selected-discovered-character");
        }
        finally{view.DataContext=null;}
    }
    internal static async Task EquipmentAsync(MainViewModel vm,List<string> checks)
    {
        var selected=vm.SelectedItem;string section=vm.SelectedView;string type=vm.CatalogType;string query=vm.CatalogQuery;
        string before=vm.Snapshot.ToJsonString();bool dirty=vm.IsDirty;
        vm.SelectedView="equipment";
        var view=new WorkbenchView{DataContext=vm};Layout(view);await Idle(vm);Layout(view);
        try
        {
            var tray=(ListBox)view.FindName("StashTray");var inspector=(ContentControl)view.FindName("InspectorCardHost");
            var item=vm.StashItems.FirstOrDefault()??throw new InvalidOperationException("Fixture has no stash piece for the native workbench flow.");
            tray.SelectedItem=item;await Dispatcher.Yield(DispatcherPriority.DataBind);
            Require(ReferenceEquals(vm.SelectedItem,item)&&inspector.Content is ItemTooltipCard card&&ReferenceEquals(card.Item,item),"Selecting a stash piece failed to update the actual inspector.");
            var doll=(PaperDoll)view.FindName("EquipmentDoll");var inventory=(Viewbox)view.FindName("ContainerViewport");
            Require(doll.ActualWidth>=190&&inventory.ActualWidth>200&&doll.TranslatePoint(new Point(doll.ActualWidth,0),view).X<=inventory.TranslatePoint(new Point(0,0),view).X,"The narrow workbench overlaps equipment and inventory.");
            checks.Add("native-stash-selection-updates-full-inspector-without-overlap");

            var tree=(TreeView)view.FindName("CatalogTree");
            var first=(CatalogBranch)tree.Items[0];first.IsExpanded=false;Layout(view);await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            var baseBranch=tree.Items.OfType<CatalogBranch>().Single(branch=>branch.Key=="base");
            var node=(TreeViewItem?)tree.ItemContainerGenerator.ContainerFromItem(baseBranch)??throw new InvalidOperationException("The catalog branch was not realized.");
            node.IsSelected=true;await Idle(vm);Layout(view);
            Require(vm.CatalogType=="base"&&baseBranch.Entries.Any()&&vm.ErrorText.Length==0,"Selecting the base branch failed to query the actual profile.");
            var complete=await vm.GetCatalogAsync("base",allPages:true);
            Require(baseBranch.Entries.Select(entry=>entry.Id).SequenceEqual(complete.Select(entry=>entry.Id)),"The catalog tree omitted entries beyond its first page.");
            Require(vm.Snapshot.ToJsonString()==before&&vm.IsDirty==dirty,"Browsing the redesigned workbench changed the draft.");
            checks.Add("native-catalog-branch-queries-profile-without-changing-draft");
        }
        finally
        {
            view.DataContext=null;vm.SelectedItem=selected;vm.SelectedView=section;vm.CatalogType=type;vm.CatalogQuery=query;
        }
    }
    static IEnumerable<T> Descendants<T>(DependencyObject parent)where T:DependencyObject
    {
        for(int index=0;index<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);index++)
        {
            var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,index);
            if(child is T match)yield return match;
            foreach(var descendant in Descendants<T>(child))yield return descendant;
        }
    }
}
