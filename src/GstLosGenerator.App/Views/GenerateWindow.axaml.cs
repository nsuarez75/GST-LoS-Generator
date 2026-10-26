using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GstLosGenerator.App.Views;

public partial class GenerateWindow : Window
{
    public GenerateWindow() => InitializeComponent();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
