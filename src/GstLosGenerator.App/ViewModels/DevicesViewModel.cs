using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;

namespace GstLosGenerator.App.ViewModels;

// Devices de un PLC con todas sus direcciones, en el orden del UDT.
public sealed partial class DevicesViewModel : EditableViewModel, ISelectable
{
    public Plc Plc { get; }
    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    [ObservableProperty]
    public partial DeviceViewModel? Selected { get; set; }

    public DevicesViewModel(Session session, Plc plc) : base(session)
    {
        Plc = plc;
        foreach (var device in plc.Devices)
        {
            Devices.Add(new DeviceViewModel(session, plc, device));
        }
        Selected = Devices.FirstOrDefault();
    }

    public bool IsEmpty => Devices.Count == 0;

    public void Select(string subject) => Selected = Devices.FirstOrDefault(d => d.Name == subject) ?? Selected;

    [RelayCommand]
    private void Add()
    {
        var device = new Device { Number = Plc.Devices.Select(d => d.Number).DefaultIfEmpty(0).Max() + 1 };
        Session.Change("add-device", () => Plc.Devices.Add(device));
        var vm = new DeviceViewModel(Session, Plc, device);
        Devices.Add(vm);
        Selected = vm;
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void Remove()
    {
        if (Selected is not { } vm)
        {
            return;
        }
        Session.Change("remove-device", () => Plc.Devices.Remove(vm.Device));
        Devices.Remove(vm);
        Selected = Devices.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }
}

public sealed partial class DeviceViewModel : EditableViewModel
{
    private readonly Plc _plc;

    public Device Device { get; }
    public ObservableCollection<AddressRowViewModel> Addresses { get; } = [];
    public static IReadOnlyList<string> DataTypeChoices { get; } = ["Bool", "Byte", "Int16", "Int32", "Int64", "Float", "String", "String40", "String80", "String120"];

    [ObservableProperty]
    public partial AddressRowViewModel? SelectedAddress { get; set; }

    public DeviceViewModel(Session session, Plc plc, Device device) : base(session)
    {
        _plc = plc;
        Device = device;
        foreach (var address in device.Addresses)
        {
            Addresses.Add(new AddressRowViewModel(session, address));
        }
    }

    public string Name => _plc.DeviceName(Device.Number);
    public string Udt => Naming.DeviceUdt(_plc.Number, Device.Number);
    public string Summary => L.T("{0} direcciones", Device.Addresses.Count);

    public string Number
    {
        get => Device.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, out var n) && n > 0 && _plc.FindDevice(n) is null)
            {
                Edit(Device.Number, n, v => Device.Number = v);
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Udt));
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private void AddAddress()
    {
        var index = SelectedAddress is null ? Device.Addresses.Count : Addresses.IndexOf(SelectedAddress) + 1;
        var type = SelectedAddress?.Address.DataType ?? "Bool";
        var name = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{type}_{_plc.Number:00}{Device.Number}_o(000)");
        var address = new DeviceAddress { Name = name, DataType = type, SheetName = name };
        Session.Change("add-address", () => Device.Addresses.Insert(index, address));
        var row = new AddressRowViewModel(Session, address);
        Addresses.Insert(index, row);
        SelectedAddress = row;
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    private void RemoveAddress()
    {
        if (SelectedAddress is not { } row)
        {
            return;
        }
        var index = Addresses.IndexOf(row);
        Session.Change("remove-address", () => Device.Addresses.Remove(row.Address));
        Addresses.Remove(row);
        SelectedAddress = Addresses.Count == 0 ? null : Addresses[Math.Min(index, Addresses.Count - 1)];
        OnPropertyChanged(nameof(Summary));
    }
}

public sealed partial class AddressRowViewModel(Session session, DeviceAddress address) : EditableViewModel(session)
{
    public DeviceAddress Address { get; } = address;
    public IReadOnlyList<string> DataTypeChoices => DeviceViewModel.DataTypeChoices;

    public string Name
    {
        get => Address.Name;
        set
        {
            var name = value.Trim();
            // Si el nombre de la LoS era el mismo, se mantienen iguales.
            Edit(Address.Name, name, v =>
            {
                if (Address.SheetName == Address.Name)
                {
                    Address.SheetName = v;
                }
                Address.Name = v;
            });
        }
    }

    public string? DataType
    {
        get => Address.DataType;
        set
        {
            if (value is not null && Edit(Address.DataType, value, v => Address.DataType = v))
            {
                OnPropertyChanged(nameof(TiaType));
            }
        }
    }

    public string TiaType => DataTypes.ToTia(Address.DataType);
}
