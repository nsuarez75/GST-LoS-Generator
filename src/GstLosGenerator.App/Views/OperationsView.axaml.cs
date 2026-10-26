using Avalonia.Controls;

namespace GstLosGenerator.App.Views;

public partial class OperationsView : UserControl
{
    // Por debajo de este ancho la lista de operaciones se estrecha para dejar sitio a la ficha.
    private const double NarrowWidth = 760;

    public OperationsView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Body.ColumnDefinitions[0].Width = new GridLength(e.NewSize.Width < NarrowWidth ? 200 : 250);
    }
}
