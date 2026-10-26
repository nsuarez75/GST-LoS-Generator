using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GstLosGenerator.App.Views;

public partial class CatalogWindow : Window
{
    public CatalogWindow() => InitializeComponent();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
