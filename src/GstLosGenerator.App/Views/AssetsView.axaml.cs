using Avalonia.Controls;
using Avalonia.Interactivity;
using GstLosGenerator.App.ViewModels;

namespace GstLosGenerator.App.Views;

public partial class AssetsView : UserControl
{
    // Por debajo de este ancho las columnas de la derecha se estrechan.
    private const double NarrowWidth = 980;

    public AssetsView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Classes.Set("narrow", e.NewSize.Width < NarrowWidth);
    }

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
