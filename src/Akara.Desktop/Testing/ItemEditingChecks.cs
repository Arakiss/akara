using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Akara.Desktop.Controls;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Testing;

internal static class ItemEditingChecks
{
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static async Task RunAsync(MainViewModel vm,string evidenceRoot,List<string> checks)
    {
        string before=Hash(vm.SourcePath),snapshot=vm.Snapshot.ToJsonString();
        var item=vm.Items.First(piece=>piece.Name.Contains("Oculus",StringComparison.OrdinalIgnoreCase));
        var others=vm.Items.Concat(vm.MercenaryItems).Where(piece=>piece.Id!=item.Id).Select(Signature).Order(StringComparer.Ordinal).ToArray();
        var design=await vm.CreateDesignFromItemAsync(item);var catalog=await vm.GetCatalogAsync("stat",allPages:true);
        var editor=new ItemPropertiesEditor(design.Stats!.Select(stat=>new ItemPropertyRow(stat,catalog.Single(entry=>entry.Data.Int("nativeId")==stat.Id),item.Stats.Single(entry=>entry.Id==stat.Id&&entry.Layer==stat.Layer).DisplayValue)),catalog,false);
        var rate=editor.Rows.Single(row=>row.NativeName=="item_fastercastrate");
        double value=item.Stats.Single(stat=>stat.NativeName==rate.NativeName).DisplayValue+10;
        editor.Table.SelectedItem=rate;Layout(editor);await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        editor.ValueEditor.SetCurrentValue(TextBox.TextProperty,value.ToString(CultureInfo.InvariantCulture));await Dispatcher.Yield(DispatcherPriority.DataBind);
        Require(rate.Label.Contains("Velocidad de lanzamiento",StringComparison.Ordinal)&&rate.Value==value.ToString(CultureInfo.InvariantCulture),"The visible property editor did not update the selected readable row immediately.");
        Require(editor.TryGetProperties(out var properties,out _)&&properties!.Single(stat=>stat.Id==rate.Id).Value==value,"The edited value was not collected before focus loss.");
        foreach(var original in design.Stats!.Where(stat=>stat.Id!=rate.Id))Require(properties!.Contains(original),"Editing one property changed an unrelated raw stat.");
        checks.Add("native-item-property-value-editor-updates-immediately-and-preserves-other-raw-stats");

        editor.ValueEditor.SetCurrentValue(TextBox.TextProperty,"not-a-number");await Dispatcher.Yield(DispatcherPriority.DataBind);
        Require(!editor.TryGetProperties(out _,out var error)&&error is {Length:>0},"Invalid item property text was accepted.");
        editor.ValueEditor.SetCurrentValue(TextBox.TextProperty,value.ToString(CultureInfo.InvariantCulture));await Dispatcher.Yield(DispatcherPriority.DataBind);
        editor.ParameterEditor.SetCurrentValue(TextBox.TextProperty,"invalid");await Dispatcher.Yield(DispatcherPriority.DataBind);
        Require(!editor.TryGetProperties(out _,out _),"Invalid item property parameter was accepted.");
        editor.ParameterEditor.SetCurrentValue(TextBox.TextProperty,"0");await Dispatcher.Yield(DispatcherPriority.DataBind);
        checks.Add("native-item-property-and-parameter-errors-are-rejected-before-closing");

        var gold=editor.PropertyPicker.Items.OfType<ItemPropertiesEditor.PropertyChoice>().Single(choice=>choice.Entry.Name=="item_goldbonus");
        editor.PropertyPicker.SelectedItem=gold;editor.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Dispatcher.Yield(DispatcherPriority.DataBind);
        editor.ValueEditor.SetCurrentValue(TextBox.TextProperty,"20");await Dispatcher.Yield(DispatcherPriority.DataBind);
        Require(editor.TryGetProperties(out properties,out error)&&editor.Rows.Any(row=>row.NativeName=="item_goldbonus"&&row.Value=="20"),"Adding a catalog property failed to collect its value.");
        design.Stats=properties;
        error=await vm.CheckItemDesignAsync(design,item);
        Require(error is null,"The native object design failed validation: "+error);
        Require(!vm.IsDirty&&vm.Snapshot.ToJsonString()==snapshot&&Hash(vm.SourcePath)==before,"Checking the dialog design changed the draft or source.");
        checks.Add("native-item-add-property-and-validate-design-without-draft-or-source-writes");

        await vm.ApplyDesignAsync(design,item);Require(vm.ErrorText.Length==0,"The object edit failed: "+vm.ErrorText);
        await ((NativeCommand)vm.SaveCommand).ExecuteAsync();Require(vm.CanCommit&&Hash(vm.SourcePath)==before,"The item edit wrote before review.");
        await ((NativeCommand)vm.SaveCommand).ExecuteAsync();Require(vm.ErrorText.Length==0&&!vm.IsDirty&&Hash(vm.SourcePath)!=before,"The reviewed item edit failed to save.");
        await vm.OpenPathAsync(vm.SourcePath);
        var reopened=vm.Items.Single(piece=>piece.Name==item.Name&&piece.Page==item.Page&&piece.Slot==item.Slot);
        Require(reopened.Stats.Single(stat=>stat.NativeName=="item_fastercastrate").DisplayValue==value&&reopened.Stats.Single(stat=>stat.NativeName=="item_goldbonus").DisplayValue==20,"The saved item did not retain its edited and added properties after reopening.");
        var afterOthers=vm.Items.Concat(vm.MercenaryItems).Where(piece=>!ReferenceEquals(piece,reopened)).Select(Signature).Order(StringComparer.Ordinal).ToArray();
        Require(others.SequenceEqual(afterOthers),"Editing the object changed an unrelated piece's payload.");
        checks.Add("native-item-edit-and-added-property-persist-after-reviewed-save-and-reopen");
        await vm.RefreshBackupsAsync();await vm.RestoreBackupAsync(vm.Backups.First(backup=>Hash(backup.Path)==before).Id);
        Require(vm.ErrorText.Length==0&&Hash(vm.SourcePath)==before,"The item-flow test did not restore its isolated private copy.");
    }
    // Session-local UI ids are reassigned when a save is reopened; native item/stat data must match.
    static string Signature(ItemViewModel item)
    {
        var data=(JsonObject)item.Data.DeepClone();
        void Walk(JsonNode? node)
        {
            if(node is JsonObject obj){if(obj["id"] is JsonValue value&&value.TryGetValue<string>(out _))obj.Remove("id");foreach(var entry in obj)Walk(entry.Value);}
            else if(node is JsonArray array)foreach(var entry in array)Walk(entry);
        }
        Walk(data);return data.ToJsonString();
    }
    static void Layout(FrameworkElement view){view.Width=820;view.Measure(new Size(820,900));view.Arrange(new Rect(0,0,820,Math.Max(700,view.DesiredSize.Height)));view.UpdateLayout();}
}
