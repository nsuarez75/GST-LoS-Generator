// Arrastrar con el ratón dentro de una vista. El arrastre empieza cuando el puntero se aleja unos píxeles
// del sitio donde se pulsó, para que un clic normal siga seleccionando. Lo arrastrado se queda aquí:
// solo se suelta dentro de la misma vista.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace GstLosGenerator.App.Views;

internal sealed class DragStarter(Control host)
{
    private const double Threshold = 6;
    private static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("gst-los-generator-drag");

    private PointerPressedEventArgs? _pressed;
    private Point _start;

    // Lo que se está arrastrando; null fuera de un arrastre.
    public object? Dragged { get; private set; }

    // Se acaba el arrastre, se haya soltado o no.
    public event Action? Ended;

    public void Press(PointerPressedEventArgs e, object item)
    {
        if (!e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = e;
        _start = e.GetPosition(host);
        Dragged = item;
    }

    public void Release() => _pressed = null;

    public async void Move(PointerEventArgs e)
    {
        if (_pressed is not { } pressed)
        {
            return;
        }
        if (!e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
        {
            _pressed = null;
            return;
        }
        var delta = e.GetPosition(host) - _start;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) < Threshold)
        {
            return;
        }
        _pressed = null;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(Format, "item"));
        try
        {
            await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Move);
        }
        finally
        {
            Dragged = null;
            Ended?.Invoke();
        }
    }

    // Engancha los eventos del puntero de un control; press decide qué se arrastra desde donde se pulsó (null: nada).
    public void Attach(Control control, Func<PointerPressedEventArgs, object?> press)
    {
        control.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (press(e) is { } item)
            {
                Press(e, item);
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        control.AddHandler(InputElement.PointerMovedEvent, (_, e) => Move(e), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        control.AddHandler(InputElement.PointerReleasedEvent, (_, _) => Release(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
    }
}
