using Avalonia.Controls;
using Avalonia.Interactivity;
using GstLosGenerator.App.ViewModels;

namespace GstLosGenerator.App.Views;

public partial class AssetsView : UserControl
{
    public AssetsView() => InitializeComponent();

    // «Eliminar» quita la variable seleccionada o, si es un asset, el asset.
    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AssetsViewModel vm)
        {
            return;
        }
        if (vm.Selected is VariableRowViewModel)
        {
            vm.RemoveVariableCommand.Execute(null);
        }
        else
        {
            vm.RemoveAssetCommand.Execute(null);
        }
    }
}
