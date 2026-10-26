using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;

namespace GstLosGenerator.App.ViewModels;

// Ficha de un PLC: nombre, número, resumen y acciones sobre su FB.
public sealed partial class PlcEditorViewModel(MainViewModel host, Plc plc) : EditableViewModel(host.Session)
{
    public MainViewModel Host { get; } = host;
    public Plc Plc { get; } = plc;

    public string Name
    {
        get => Plc.Name;
        set
        {
            var name = value.Trim();
            if (name.Length > 0 && Host.Session.Project.FindPlc(name) is null)
            {
                Edit(Plc.Name, name, v => Plc.Name = v);
            }
        }
    }

    public string Number
    {
        get => Plc.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, out var n) && n >= 0)
            {
                Edit(Plc.Number, n, v => Plc.Number = v);
                OnPropertyChanged(nameof(DeviceNames));
            }
        }
    }

    public string DeviceNames => Plc.Devices.Count == 0 ? "—" : string.Join(", ", Plc.Devices.Select(d => Plc.DeviceName(d.Number)));
    public string Summary => L.T("{0} assets · {1} operaciones · {2} locations", Plc.Assets.Count, Plc.Operations.Count, Plc.Locations.Count);
    public string CurrentFb => Plc.CurrentFbFile is { } f ? f : L.T("Ninguno: se generará desde cero");
    public bool HasCurrentFb => Plc.CurrentFb is not null;

    [RelayCommand]
    private Task ReadFb() => Host.ReadFbFor(Plc);

    [RelayCommand]
    private void ForgetFb()
    {
        Host.Session.Change("forget-fb", () =>
        {
            Plc.CurrentFb = null;
            Plc.CurrentFbFile = null;
        });
        OnPropertyChanged(nameof(CurrentFb));
        OnPropertyChanged(nameof(HasCurrentFb));
    }

    [RelayCommand]
    private Task Generate() => Host.GenerateFor(Plc);

    [RelayCommand]
    private Task Remove() => Host.RemovePlc(Plc);
}
