using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Akara.Desktop.Localization;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Testing;

internal static class LanguageChecks
{
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    internal static async Task RunAsync(string root,List<string> checks)
    {
        string original=L.Language, state=Path.Combine(root,"language-preferences");
        try
        {
            LanguagePreferences.Apply("es");
            Require(LanguagePreferences.Load(state)=="en","A fresh installation inherited Spanish from the OS or current UI culture.");
            LanguagePreferences.Apply(LanguagePreferences.Load(state));
            var vm=new MainViewModel(state);
            var english=new WorkbenchView{DataContext=vm};
            Require(((MenuItem)english.FindName("FileMenu")).Header.ToString()=="_File"&&((MenuItem)english.FindName("LanguageMenu")).Header.ToString()=="Language"&&vm.NavItems[1].Title=="Skills"&&Names.Skill("Frozen Orb")=="Frozen Orb"&&Names.Stat("item_fastercastrate","",0)=="Faster Cast Rate","The native UI did not start in English.");
            checks.Add("native-fresh-install-defaults-to-english-independent-of-os-culture");
            english.DataContext=null;
            await vm.ChangeLanguageAsync("es");
            var spanish=new WorkbenchView{DataContext=vm};
            Require(((MenuItem)spanish.FindName("FileMenu")).Header.ToString()==L.T("_Archivo")&&vm.NavItems[1].Title==L.T("Habilidades")&&Names.Skill("Frozen Orb")=="Orbe helada"&&LanguagePreferences.Load(state)=="es","Spanish selection did not update native UI and persisted preference.");
            checks.Add("native-spanish-is-optional-and-persists-in-isolated-preferences");
            spanish.DataContext=null;
            await vm.ChangeLanguageAsync("en");
            Require(vm.NavItems[1].Title=="Skills"&&LanguagePreferences.Load(state)=="en","Switching back to English was not persisted.");
            File.WriteAllText(LanguagePreferences.PathFor(state),"invalid json");
            Require(LanguagePreferences.Load(state)=="en","Invalid preference data did not fall back to English.");
            checks.Add("native-language-preference-roundtrip-and-invalid-file-fallback");
            LanguagePreferences.Apply("en");
            var englishRow=new ItemPropertyRow(new(0,Value:1.5),null);
            Require(englishRow.Value=="1.5","English property values did not use a decimal point.");
            englishRow.Value="1.75";Require(englishRow.ToSpec().Value==1.75,"English decimal input changed its numeric value.");
            LanguagePreferences.Apply("es");
            var spanishRow=new ItemPropertyRow(new(0,Value:1.5),null);
            Require(spanishRow.Value=="1,5","Spanish property values did not preserve decimal-comma input.");
            spanishRow.Value="1,75";Require(spanishRow.ToSpec().Value==1.75,"Spanish decimal input changed its numeric value.");
            checks.Add("native-property-decimals-follow-selected-language-without-changing-values");
        }
        finally{LanguagePreferences.Apply(original);}
    }
    internal static async Task DraftAsync(MainViewModel vm,List<string> checks)
    {
        string original=L.Language,path=vm.SourcePath,hash=Hash(path),name=vm.CharacterName,view=vm.SelectedView;
        long gold=long.Parse(vm.GoldText);var skill=vm.Skills.First(s=>s.NativeName=="Frozen Orb");int points=skill.Points;
        try
        {
            vm.GoldText=(gold+1).ToString();skill.Points=points-1;
            await vm.ChangeLanguageAsync(original=="en"?"es":"en");
            Require(vm.ErrorText.Length==0&&vm.IsDirty&&!vm.CanCommit&&vm.CharacterName==name&&vm.GoldText==(gold+1).ToString()&&vm.Skills.Single(s=>s.Id==skill.Id).Points==points-1&&Hash(path)==hash,"Language switching lost a pending edit, promoted review, or wrote the source.");
            await vm.PreviewAsync(true);Require(vm.CanCommit&&Hash(path)==hash,"The preserved draft could not be reviewed.");
            await vm.ChangeLanguageAsync(original);
            Require(vm.CanCommit&&vm.GoldText==(gold+1).ToString()&&vm.Skills.Single(s=>s.Id==skill.Id).Points==points-1&&Hash(path)==hash,"A reviewed draft changed while switching language back.");
            checks.Add("native-language-switch-preserves-pending-and-reviewed-drafts-without-source-writes");
            vm.SetInputErrorCount(1);await vm.ChangeLanguageAsync(original=="en"?"es":"en");
            Require(L.Language==original&&!vm.CanChangeLanguage&&Hash(path)==hash,"Language switching discarded invalid uncommitted input.");
            vm.SetInputErrorCount(0);
            checks.Add("native-language-switch-keeps-invalid-input-in-place");
        }
        finally
        {
            LanguagePreferences.Apply(original);vm.SetInputErrorCount(0);await vm.ResetAsync();vm.SelectedView=view;
            Require(Hash(path)==hash,"Localization checks changed the original fixture copy.");
        }
    }
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
