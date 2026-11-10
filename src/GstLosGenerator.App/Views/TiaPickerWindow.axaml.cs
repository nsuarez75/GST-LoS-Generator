using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GstLosGenerator.App.ViewModels;

namespace GstLosGenerator.App.Views;

public partial class TiaPickerWindow : Window
{
    public TiaPickerWindow() => InitializeComponent();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    // Doble clic en un bloque: aceptar.
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is TiaPickerViewModel { CanAccept: true, Selected.Kind: TiaNodeKind.Block } picker)
        {
            picker.Accept();
        }
    }
}
