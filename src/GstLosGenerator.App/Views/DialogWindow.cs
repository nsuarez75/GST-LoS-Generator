// Diálogos sencillos (mensaje, confirmar, guardar cambios, campos de texto y lista de cambios),
// montados en código porque todos tienen la misma forma: título, contenido y botones abajo a la derecha.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using GstLosGenerator.App.ViewModels;
using GstLosGenerator.Core;

namespace GstLosGenerator.App.Views;

public sealed class DialogWindow : Window
{
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

    private DialogWindow(string title, Control body, double width)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 720;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var heading = new TextBlock { Text = title, Classes = { "h2" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(20), RowSpacing = 14 };
        grid.Children.Add(heading);
        Grid.SetRow(body, 1);
        grid.Children.Add(body);
        Grid.SetRow(_buttons, 2);
        grid.Children.Add(_buttons);
        Content = grid;
    }

    private Button AddButton(string text, Action click, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button { Content = text, IsDefault = isDefault, IsCancel = isCancel, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (isDefault)
        {
            button.Classes.Add("accent");
        }
        button.Click += (_, _) => click();
        _buttons.Children.Add(button);
        return button;
    }

    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public static async Task Message(Window owner, string title, string text)
    {
        var dialog = new DialogWindow(title, Text(text), 480);
        dialog.AddButton(L.T("Aceptar"), dialog.Close, isDefault: true, isCancel: true);
        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> Confirm(Window owner, string title, string text, string accept)
    {
        var result = false;
        var dialog = new DialogWindow(title, new ScrollViewer { Content = Text(text), MaxHeight = 480 }, 520);
        dialog.AddButton(accept, () => { result = true; dialog.Close(); }, isDefault: true);
        dialog.AddButton(L.T("Cancelar"), dialog.Close, isCancel: true);
        await dialog.ShowDialog(owner);
        return result;
    }

    public static async Task<SaveChoice> AskSave(Window owner)
    {
        var result = SaveChoice.Cancel;
        var dialog = new DialogWindow(L.T("Cambios sin guardar"), Text(L.T("El proyecto tiene cambios sin guardar. ¿Quieres guardarlos?")), 480);
        dialog.AddButton(L.T("Guardar"), () => { result = SaveChoice.Save; dialog.Close(); }, isDefault: true);
        dialog.AddButton(L.T("No guardar"), () => { result = SaveChoice.Discard; dialog.Close(); });
        dialog.AddButton(L.T("Cancelar"), dialog.Close, isCancel: true);
        await dialog.ShowDialog(owner);
        return result;
    }

    public static async Task<string[]?> Prompt(Window owner, string title, IReadOnlyList<string> labels, IReadOnlyList<string> values)
    {
        string[]? result = null;
        var boxes = new List<TextBox>();
        var panel = new StackPanel { Spacing = 6 };
        for (var i = 0; i < labels.Count; i++)
        {
            panel.Children.Add(new TextBlock { Text = labels[i], Classes = { "label" } });
            var box = new TextBox { Text = i < values.Count ? values[i] : "" };
            boxes.Add(box);
            panel.Children.Add(box);
        }
        var dialog = new DialogWindow(title, panel, 440);
        dialog.AddButton(L.T("Aceptar"), () => { result = boxes.Select(b => b.Text ?? "").ToArray(); dialog.Close(); }, isDefault: true);
        dialog.AddButton(L.T("Cancelar"), dialog.Close, isCancel: true);
        dialog.Opened += (_, _) =>
        {
            boxes.FirstOrDefault()?.Focus(NavigationMethod.Tab);
            boxes.FirstOrDefault()?.SelectAll();
        };
        await dialog.ShowDialog(owner);
        return result;
    }

    public static async Task<(bool, bool)> Changes(Window owner, string title, IReadOnlyList<string> changes, IReadOnlyList<string> warnings, string? option)
    {
        var accepted = false;
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = L.T("{0} cambios", changes.Count), Classes = { "label" } });
        panel.Children.Add(new Border
        {
            Classes = { "card" },
            Padding = new Thickness(0),
            Child = new ListBox { ItemsSource = changes, MaxHeight = 380 },
        });
        if (warnings.Count > 0)
        {
            panel.Children.Add(new TextBlock { Text = L.T("Avisos"), Classes = { "label" } });
            panel.Children.Add(new ListBox { ItemsSource = warnings, MaxHeight = 140 });
        }
        CheckBox? check = null;
        if (option is not null)
        {
            check = new CheckBox { Content = option, IsChecked = true };
            panel.Children.Add(check);
        }
        var dialog = new DialogWindow(title, panel, 760);
        dialog.AddButton(L.T("Aplicar"), () => { accepted = true; dialog.Close(); }, isDefault: true);
        dialog.AddButton(L.T("Cancelar"), dialog.Close, isCancel: true);
        await dialog.ShowDialog(owner);
        return (accepted, check?.IsChecked == true);
    }
}
