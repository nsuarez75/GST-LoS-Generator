using GstLosGenerator.App.ViewModels;

namespace GstLosGenerator.App.Tests;

// Diálogos que responden solos: aceptan todo y devuelven las rutas preparadas por el test.
internal sealed class FakeDialogs : IDialogs
{
    public List<string> Messages { get; } = [];
    public List<IReadOnlyList<string>> ChangeLists { get; } = [];
    public Queue<string?> Files { get; } = new();
    public Queue<string[]?> Prompts { get; } = new();
    public string? Folder { get; set; }
    public bool ConfirmAnswer { get; set; } = true;

    public Task Message(string title, string text)
    {
        Messages.Add(title + ": " + text);
        return Task.CompletedTask;
    }

    public Task About(string title, string text, string link) => Message(title, text + " " + link);

    public Task<bool> Confirm(string title, string text, string accept) => Task.FromResult(ConfirmAnswer);

    // Respuestas a Choose, en orden; sin ninguna preparada, la primera opción.
    public Queue<bool> Choices { get; } = new();

    public Task<bool> Choose(string title, string text, string first, string second) =>
        Task.FromResult(Choices.Count == 0 || Choices.Dequeue());

    // Opción que se elige en Pick; null: la que viene marcada.
    public int? PickAnswer { get; set; }
    public List<IReadOnlyList<string>> PickLists { get; } = [];

    public Task<int?> Pick(string title, string text, IReadOnlyList<string> options, int selected = 0)
    {
        PickLists.Add(options);
        return Task.FromResult<int?>(PickAnswer ?? selected);
    }

    public Task<SaveChoice> AskSave() => Task.FromResult(SaveChoice.Discard);

    public Task<(bool Accepted, bool Option)> Changes(string title, IReadOnlyList<string> changes, IReadOnlyList<string> warnings, string? option = null)
    {
        ChangeLists.Add(changes);
        return Task.FromResult((true, true));
    }

    public Task<string[]?> Prompt(string title, IReadOnlyList<string> labels, IReadOnlyList<string> values) =>
        Task.FromResult(Prompts.Count > 0 ? Prompts.Dequeue() : values.ToArray());

    public Task<string?> OpenFile(string title, FileFilter filter) => Task.FromResult(Files.Count > 0 ? Files.Dequeue() : null);

    public Task<string?> SaveFile(string title, FileFilter filter, string suggestedName) => Task.FromResult(Files.Count > 0 ? Files.Dequeue() : null);

    public Task<string?> PickFolder(string title) => Task.FromResult(Folder);

    public Task ShowCatalog(CatalogViewModel catalog) => Task.CompletedTask;

    public Task ShowGenerate(GenerateViewModel generate) => Task.CompletedTask;
}
