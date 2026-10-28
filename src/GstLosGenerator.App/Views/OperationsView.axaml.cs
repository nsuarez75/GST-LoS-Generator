using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GstLosGenerator.App.ViewModels;

namespace GstLosGenerator.App.Views;

public partial class OperationsView : UserControl
{
    // Por debajo de este ancho la lista de operaciones se estrecha para dejar sitio a la ficha.
    private const double NarrowWidth = 760;

    private readonly DragStarter _drag;
    private OperationViewModel? _mark;

    public OperationsView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Body.ColumnDefinitions[0].Width = new GridLength(e.NewSize.Width < NarrowWidth ? 200 : 250);

        // Las operaciones se reordenan arrastrándolas dentro de la lista.
        _drag = new DragStarter(List);
        _drag.Attach(List, e => (e.Source as StyledElement)?.DataContext as OperationViewModel);
        _drag.Ended += () => Mark(null, false);
        DragDrop.SetAllowDrop(List, true);
        List.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        List.AddHandler(DragDrop.DragLeaveEvent, (_, _) => Mark(null, false));
        List.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    // Operación sobre la que está el puntero y si caería detrás de ella (mitad de abajo).
    private (OperationViewModel? Target, bool After) Target(DragEventArgs e)
    {
        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        if (item?.DataContext is not OperationViewModel target)
        {
            return (null, false);
        }
        return (target, e.GetPosition(item).Y > item.Bounds.Height / 2);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var (target, after) = Target(e);
        var ok = _drag.Dragged is OperationViewModel dragged && target is not null && target != dragged;
        e.DragEffects = ok ? DragDropEffects.Move : DragDropEffects.None;
        Mark(ok ? target : null, after);
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var (target, after) = Target(e);
        Mark(null, false);
        if (DataContext is not OperationsViewModel vm || _drag.Dragged is not OperationViewModel dragged || target is null || target == dragged)
        {
            return;
        }
        var from = vm.Operations.IndexOf(dragged);
        var to = vm.Operations.IndexOf(target) + (after ? 1 : 0);
        vm.Move(dragged, to > from ? to - 1 : to);
    }

    private void Mark(OperationViewModel? target, bool after)
    {
        if (_mark is not null && _mark != target)
        {
            _mark.DropBefore = _mark.DropAfter = false;
        }
        _mark = target;
        if (target is not null)
        {
            target.DropBefore = !after;
            target.DropAfter = after;
        }
    }
}
