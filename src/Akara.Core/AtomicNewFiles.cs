namespace ForjaHoradrica;

public sealed partial class EditorService
{
    private bool IsSavedGames(string path)
    {
        string full = Path.GetFullPath(path), root = Path.GetFullPath(saveRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
    private void WriteNewAtomic(string path, byte[] bytes)
    {
        path = Path.GetFullPath(path);
        bool gamePath = IsSavedGames(path);
        if (gamePath && gameState()) throw new ApiProblem("game_running", L.T("Cierra Diablo II antes de crear un personaje dentro de Saved Games."), 409);
        if (File.Exists(path)) throw new ApiProblem("destination_exists", L.T("El archivo de destino ya existe. La creación nunca sobrescribe partidas."), 409);
        string dir = Path.GetDirectoryName(path)!; Directory.CreateDirectory(dir);
        string temporary = Path.Combine(dir, ".akara-new-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            if (Hash(File.ReadAllBytes(temporary)) != Hash(bytes)) throw new ApiProblem("export_verification", L.T("La copia temporal no coincide con el candidato."));
            if (gamePath && gameState()) throw new ApiProblem("game_running", L.T("El juego se abrió antes de crear el personaje. No se ha guardado la partida."), 409);
            File.Move(temporary, path, false);
            if (Hash(File.ReadAllBytes(path)) != Hash(bytes)) throw new ApiProblem("export_verification", L.T("No se pudo verificar la copia creada."));
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
