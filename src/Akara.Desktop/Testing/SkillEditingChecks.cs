using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Testing;

internal static class SkillEditingChecks
{
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    internal static async Task RunAsync(MainViewModel vm,string evidenceRoot,List<string> checks)
    {
        string section=vm.SelectedView;vm.SelectedView="skills";
        var view=new WorkbenchView{DataContext=vm,Width=1600,Height=1000};
        view.Measure(new Size(1600,1000));view.Arrange(new Rect(0,0,1600,1000));view.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);view.UpdateLayout();
        try
        {
            var skill=vm.Skills.First(row=>row.Points>2);int original=skill.Points,id=skill.Id;
            string before=Hash(vm.SourcePath);long available=vm.Snapshot.Obj("character").Obj("attributes").Long("unspentSkills");
            var input=Descendants<TextBox>(view).Single(box=>ReferenceEquals(box.DataContext,skill));
            input.SetCurrentValue(TextBox.TextProperty,(original-1).ToString(CultureInfo.InvariantCulture));
            await Dispatcher.Yield(DispatcherPriority.DataBind);
            Require(skill.Points==original-1&&vm.IsDirty&&vm.SkillPointsText==(available+1)+" disponibles","Typing skill points did not update the draft and available points before leaving the field.");
            Require(vm.SaveCommand.CanExecute(null),"A detected skill edit left the save action unavailable.");
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);view.UpdateLayout();
            var saveButton=Descendants<Button>(view).Single(button=>ReferenceEquals(button.Command,vm.SaveCommand));
            Require(saveButton.IsEnabled,"The actual toolbar save button did not enable after a skill edit.");
            checks.Add("native-skill-typing-detects-draft-and-enables-save-action-without-focus-loss");
            Capture(view,Path.Combine(evidenceRoot,"skill-edit-detected.png"));
            await ((NativeCommand)vm.SaveCommand).ExecuteAsync();view.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            Require(vm.ErrorText.Length==0&&vm.CanCommit&&vm.SelectedView=="changes"&&vm.SaveActionText=="Guardar"&&Hash(vm.SourcePath)==before,"The first save action did not review the skill edit without writing.");
            checks.Add("native-save-action-reviews-unreviewed-skill-edit-before-any-write");

            vm.SelectedView="skills";view.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.ContextIdle);view.UpdateLayout();
            var updated=vm.Skills.Single(row=>row.Id==id);
            input=Descendants<TextBox>(view).Single(box=>ReferenceEquals(box.DataContext,updated));
            input.SetCurrentValue(TextBox.TextProperty,"invalid");await Dispatcher.Yield(DispatcherPriority.DataBind);
            Require(vm.HasInputErrors&&vm.HasErrors&&!vm.CanCommit&&!vm.SaveCommand.CanExecute(null),"An invalid skill field left a stale reviewed value saveable.");
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);view.UpdateLayout();
            Require(!saveButton.IsEnabled&&vm.ValidationMessage.Contains("Corrige",StringComparison.Ordinal),"The actual toolbar or status still offered saving invalid skill text.");
            Capture(view,Path.Combine(evidenceRoot,"skill-edit-invalid.png"));
            await vm.SaveAsync();Require(Hash(vm.SourcePath)==before,"Invalid skill input wrote the previous reviewed value.");
            input.SetCurrentValue(TextBox.TextProperty,"");await Dispatcher.Yield(DispatcherPriority.DataBind);
            Require(vm.HasInputErrors&&!vm.SaveCommand.CanExecute(null),"An empty skill field lost its validation error.");
            checks.Add("native-invalid-or-empty-skill-input-blocks-stale-reviewed-save");

            input.SetCurrentValue(TextBox.TextProperty,(original-2).ToString(CultureInfo.InvariantCulture));await Dispatcher.Yield(DispatcherPriority.DataBind);
            Require(!vm.HasInputErrors&&updated.Points==original-2&&!vm.CanCommit&&vm.SaveCommand.CanExecute(null),"Correcting a skill field did not clear the error and require a fresh review.");
            await ((NativeCommand)vm.SaveCommand).ExecuteAsync();Require(vm.CanCommit&&Hash(vm.SourcePath)==before,"The corrected skill edit was not reviewed without writing.");
            await ((NativeCommand)vm.SaveCommand).ExecuteAsync();
            Require(vm.ErrorText.Length==0&&!vm.IsDirty&&vm.Skills.Single(row=>row.Id==id).Points==original-2&&Hash(vm.SourcePath)!=before,"Confirming the reviewed save failed to persist the exact skill edit.");
            await vm.RefreshBackupsAsync();var backup=vm.Backups.First(row=>Hash(row.Path)==before);
            await vm.RestoreBackupAsync(backup.Id);
            Require(vm.ErrorText.Length==0&&Hash(vm.SourcePath)==before&&vm.Skills.Single(row=>row.Id==id).Points==original,"The skill-flow test failed to restore its isolated private copy.");
            checks.Add("native-corrected-skill-input-reviews-saves-and-restores-private-copy");
        }
        finally{view.DataContext=null;await vm.ResetAsync();vm.SelectedView=section;}
    }
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static void Capture(WorkbenchView view,string path)
    {
        view.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.Width,(int)view.Height,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(view);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file=File.Create(path);encoder.Save(file);
    }
    static IEnumerable<T> Descendants<T>(DependencyObject parent)where T:DependencyObject
    {
        for(int index=0;index<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);index++)
        {
            var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,index);
            if(child is T match)yield return match;
            foreach(var result in Descendants<T>(child))yield return result;
        }
    }
}
