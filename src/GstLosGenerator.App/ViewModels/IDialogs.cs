// Lo que los modelos de vista piden a la ventana: preguntar, elegir archivos y abrir otras ventanas.
// En los tests se sustituye por una versión que responde sola.
namespace GstLosGenerator.App.ViewModels;

public enum SaveChoice
{
    Save,
    Discard,
    Cancel,
}

public sealed record FileFilter(string Name, string Extension);

public interface IDialogs
{
    Task Message(string title, string text);

    // Texto con un enlace que se abre en el navegador.
    Task About(string title, string text, string link);

    Task<bool> Confirm(string title, string text, string accept);

    // Pregunta con dos respuestas; true si se elige la primera. Cerrar la ventana elige la segunda.
    Task<bool> Choose(string title, string text, string first, string second);

    Task<SaveChoice> AskSave();

    // Lista de cambios para aceptar o cancelar; option es una casilla opcional (marcada de inicio).
    Task<(bool Accepted, bool Option)> Changes(string title, IReadOnlyList<string> changes, IReadOnlyList<string> warnings, string? option = null);

    // Varios campos de texto; null si se cancela.
    Task<string[]?> Prompt(string title, IReadOnlyList<string> labels, IReadOnlyList<string> values);

    Task<string?> OpenFile(string title, FileFilter filter);

    Task<string?> SaveFile(string title, FileFilter filter, string suggestedName);

    Task<string?> PickFolder(string title);

    Task ShowCatalog(CatalogViewModel catalog);

    Task ShowGenerate(GenerateViewModel generate);
}
