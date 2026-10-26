using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GstLosGenerator.App.ViewModels;
using GstLosGenerator.Core;

namespace GstLosGenerator.App.Views;

// Ventana principal. También hace de IDialogs: los diálogos se abren sobre ella.
public partial class MainWindow : Window, IDialogs
{
    // Por debajo de este ancho los botones de la barra superior se quedan solo con el icono.
    private const double NarrowWidth = 1180;

    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        ExitItem.Click += (_, _) => Close();
        Closing += OnClosing;
        SizeChanged += (_, e) => SetNarrow(e.NewSize.Width < NarrowWidth);
    }

    // Ventana estrecha: botones de la barra solo con icono y lateral más estrecho. El lateral solo cambia
    // al cruzar el umbral, para no deshacer lo que el usuario haya ajustado con el separador.
    private void SetNarrow(bool narrow)
    {
        if (Classes.Contains("narrow") == narrow)
        {
            return;
        }
        Classes.Set("narrow", narrow);
        Body.ColumnDefinitions[0].Width = new GridLength(narrow ? 230 : 260);
    }

    public MainViewModel? ViewModel => DataContext as MainViewModel;

    public void Attach(MainViewModel main)
    {
        main.Dialogs = this;
        DataContext = main;
    }

    // Al cerrar con cambios sin guardar se pregunta; si se guarda o se descarta, se cierra de verdad.
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || ViewModel is not { } main || !main.Session.Dirty)
        {
            return;
        }
        e.Cancel = true;
        if (await main.ConfirmDiscard())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    // Cierra sin preguntar (al cambiar de idioma se sustituye la ventana).
    public void CloseSilently()
    {
        _closeConfirmed = true;
        Close();
    }

    // ---------------------------------------------------------------- IDialogs

    public Task Message(string title, string text) => DialogWindow.Message(this, title, text);

    public Task About(string title, string text, string link) => DialogWindow.About(this, title, text, link);

    public Task<bool> Confirm(string title, string text, string accept) => DialogWindow.Confirm(this, title, text, accept);

    public Task<SaveChoice> AskSave() => DialogWindow.AskSave(this);

    public Task<(bool Accepted, bool Option)> Changes(string title, IReadOnlyList<string> changes, IReadOnlyList<string> warnings, string? option = null) =>
        DialogWindow.Changes(this, title, changes, warnings, option);

    public Task<string[]?> Prompt(string title, IReadOnlyList<string> labels, IReadOnlyList<string> values) =>
        DialogWindow.Prompt(this, title, labels, values);

    public async Task<string?> OpenFile(string title, FileFilter filter)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [Type(filter), FilePickerFileTypes.All],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> SaveFile(string title, FileFilter filter, string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = filter.Extension.TrimStart('.'),
            FileTypeChoices = [Type(filter)],
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickFolder(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    public Task ShowCatalog(CatalogViewModel catalog) => new CatalogWindow { DataContext = catalog }.ShowDialog(this);

    public Task ShowGenerate(GenerateViewModel generate)
    {
        var window = new GenerateWindow { DataContext = generate };
        generate.Done += window.Close;
        return window.ShowDialog(this);
    }

    private static FilePickerFileType Type(FileFilter filter) =>
        new(filter.Name) { Patterns = ["*" + filter.Extension] };
}
