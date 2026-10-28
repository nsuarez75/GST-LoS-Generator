using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GstLosGenerator.App.ViewModels;

namespace GstLosGenerator.App.Views;

public partial class AssetsView : UserControl
{
    // Por debajo de este ancho las columnas de la derecha se estrechan.
    private const double NarrowWidth = 980;

    private readonly DragStarter _drag;
    private AssetNodeViewModel? _mark;

    public AssetsView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Classes.Set("narrow", e.NewSize.Width < NarrowWidth);

        // Las variables se arrastran por su asa a otro asset; si la pulsada está seleccionada, van todas las seleccionadas.
        _drag = new DragStarter(Tree);
        _drag.Attach(Tree, PressedVariables);
        _drag.Ended += () => Mark(null);
        DragDrop.SetAllowDrop(Tree, true);
        Tree.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Tree.AddHandler(DragDrop.DragLeaveEvent, (_, _) => Mark(null));
        Tree.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private List<VariableRowViewModel> SelectedVariables => Tree.SelectedItems?.OfType<VariableRowViewModel>().ToList() ?? [];

    private List<VariableRowViewModel>? PressedVariables(PointerPressedEventArgs e)
    {
        var grip = (e.Source as Visual)?.FindAncestorOfType<Border>(includeSelf: true);
        while (grip is not null && !grip.Classes.Contains("grip"))
        {
            grip = grip.FindAncestorOfType<Border>();
        }
        if (grip?.DataContext is not VariableRowViewModel row)
        {
            return null;
        }
        var selected = SelectedVariables;
        return selected.Contains(row) ? selected : [row];
    }

    // Asset sobre el que está el puntero (o el dueño de la variable sobre la que está).
    private static AssetNodeViewModel? Target(DragEventArgs e) => (e.Source as StyledElement)?.DataContext switch
    {
        AssetNodeViewModel node => node,
        VariableRowViewModel row => row.Owner,
        _ => null,
    };

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var target = Target(e);
        var ok = _drag.Dragged is List<VariableRowViewModel> rows && target is not null && rows.Any(r => r.Owner != target);
        e.DragEffects = ok ? DragDropEffects.Move : DragDropEffects.None;
        Mark(ok ? target : null);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var target = Target(e);
        Mark(null);
        if (DataContext is AssetsViewModel vm && _drag.Dragged is List<VariableRowViewModel> rows && target is not null)
        {
            await vm.MoveVariables(rows, target);
        }
    }

    private void Mark(AssetNodeViewModel? target)
    {
        if (_mark is not null && _mark != target)
        {
            _mark.IsDropTarget = false;
        }
        _mark = target;
        if (target is not null)
        {
            target.IsDropTarget = true;
        }
    }

    // «Eliminar» quita las variables seleccionadas o, si no hay ninguna, el asset.
    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AssetsViewModel vm)
        {
            return;
        }
        if (SelectedVariables is { Count: > 0 } rows)
        {
            vm.RemoveVariables(rows);
        }
        else
        {
            vm.RemoveAssetCommand.Execute(null);
        }
    }
}
