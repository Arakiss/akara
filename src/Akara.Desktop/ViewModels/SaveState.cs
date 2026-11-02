namespace Akara.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    int inputErrorCount;
    public bool HasInputErrors=>inputErrorCount>0;
    // The button can prepare a review; committing still requires the reviewed, valid plan.
    public bool CanSave=>CanEdit&&!HasInputErrors&&!GameRunning&&(IsDirty||IsNew);
    public string SaveActionText=>reviewed&&validPlan?"Guardar":"Revisar y guardar…";
    public string DisplayErrorText=>HasInputErrors?"Hay un valor no válido. Escribe un número entero en los puntos de habilidad.":ErrorText;
    public string SaveHint=>!HasCharacter?"Abre un personaje para editarlo.":IsSyntheticDemo?"La demostración no guarda partidas.":IsBusy?"Espera a que termine la operación.":HasInputErrors?DisplayErrorText:GameRunning?"Cierra Diablo II y pulsa Revisar para actualizar la comprobación.":!IsDirty&&!IsNew?"Sin cambios pendientes.":reviewed&&validPlan?"Guardar los cambios revisados con copia de seguridad.":"Cambios detectados. Pulsa para revisar; después podrás confirmar el guardado.";
    internal void SetInputErrorCount(int count)
    {
        if(inputErrorCount==count)return;
        if(count>0){reviewed=validPlan=false;planId="";}
        inputErrorCount=count;
        ValidationMessage=count>0?"Corrige los valores señalados antes de revisar y guardar.":PendingValidationText;
        StatusText=count>0?"Hay campos inválidos sin guardar.":PendingValidationText;
        Raise(nameof(ValidationMessage));Raise(nameof(HasInputErrors));Raise(nameof(HasErrors));Raise(nameof(DisplayErrorText));RaiseSaveState();
    }
    void RaiseSaveState()
    {
        Raise(nameof(CanCommit));Raise(nameof(CanSave));Raise(nameof(SaveActionText));Raise(nameof(SaveHint));Requery();
    }
}
