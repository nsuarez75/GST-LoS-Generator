using Avalonia.Controls;
using Avalonia.Input;
using GstLosGenerator.App.ViewModels;

namespace GstLosGenerator.App.Views;

public partial class ProjectEditorView : UserControl
{
    public ProjectEditorView() => InitializeComponent();

    // Doble clic en un PLC de la lista lo abre en el árbol.
    private void OnPlcDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: PlcSummary plc } && DataContext is ProjectEditorViewModel vm)
        {
            vm.OpenPlcCommand.Execute(plc);
        }
    }
}
