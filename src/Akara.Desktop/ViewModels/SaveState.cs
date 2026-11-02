namespace Akara.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    int inputErrorCount;
    public bool HasInputErrors=>inputErrorCount>0;
    // The button can prepare a review; committing still requires the reviewed, valid plan.
    public bool CanSave=>CanEdit&&!HasInputErrors&&!GameRunning&&(IsDirty||IsNew);
    public string SaveActionText=>reviewed&&validPlan?L.T("Guardar"):L.T("Revisar y guardar…");
    public string DisplayErrorText=>HasInputErrors?L.T("Hay un valor no válido. Escribe un número entero en los puntos de habilidad."):ErrorText;
    public string SaveHint=>!HasCharacter?L.T("Abre un personaje para editarlo."):IsSyntheticDemo?L.T("La demostración no guarda partidas."):IsBusy?L.T("Espera a que termine la operación."):HasInputErrors?DisplayErrorText:GameRunning?L.T("Cierra Diablo II y pulsa Revisar para actualizar la comprobación."):!IsDirty&&!IsNew?L.T("Sin cambios pendientes."):reviewed&&validPlan?L.T("Guardar los cambios revisados con copia de seguridad."):L.T("Cambios detectados. Pulsa para revisar; después podrás confirmar el guardado.");
    internal void SetInputErrorCount(int count)
    {
        if(inputErrorCount==count)return;
        if(count>0){reviewed=validPlan=false;planId="";}
        inputErrorCount=count;
        ValidationMessage=count>0?L.T("Corrige los valores señalados antes de revisar y guardar."):PendingValidationText;
        StatusText=count>0?L.T("Hay campos inválidos sin guardar."):PendingValidationText;
        Raise(nameof(ValidationMessage));Raise(nameof(CanChangeLanguage));Raise(nameof(HasInputErrors));Raise(nameof(HasErrors));Raise(nameof(DisplayErrorText));RaiseSaveState();
    }
    void RaiseSaveState()
    {
        Raise(nameof(CanCommit));Raise(nameof(CanSave));Raise(nameof(SaveActionText));Raise(nameof(SaveHint));Requery();
    }
}
