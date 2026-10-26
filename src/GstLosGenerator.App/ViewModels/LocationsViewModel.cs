using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;

namespace GstLosGenerator.App.ViewModels;

// Locations de un PLC: nombre y número. Se reutilizan en varias operaciones.
public sealed partial class LocationsViewModel : EditableViewModel
{
    public Plc Plc { get; }
    public ObservableCollection<LocationRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial LocationRowViewModel? Selected { get; set; }

    public LocationsViewModel(Session session, Plc plc) : base(session)
    {
        Plc = plc;
        foreach (var location in plc.Locations)
        {
            Rows.Add(new LocationRowViewModel(session, plc, location));
        }
    }

    public bool IsEmpty => Rows.Count == 0;

    [RelayCommand]
    private void Add()
    {
        var number = Plc.Locations.Select(l => l.Number).DefaultIfEmpty(0).Max() + 1;
        var name = L.T("Location{0}", number);
        var location = new Location { Name = name, Number = number };
        Session.Change("add-location", () => Plc.Locations.Add(location));
        var row = new LocationRowViewModel(Session, Plc, location);
        Rows.Add(row);
        Selected = row;
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void Remove(LocationRowViewModel? row)
    {
        row ??= Selected;
        if (row is null)
        {
            return;
        }
        Session.Change("remove-location", () => Plc.Locations.Remove(row.Location));
        Rows.Remove(row);
        OnPropertyChanged(nameof(IsEmpty));
    }
}

public sealed partial class LocationRowViewModel(Session session, Plc plc, Location location) : EditableViewModel(session)
{
    public Location Location { get; } = location;

    // Renombrar una location actualiza las operaciones que la usan.
    public string Name
    {
        get => Location.Name;
        set
        {
            var name = value.Trim();
            if (name.Length == 0 || plc.FindLocation(name) is not null)
            {
                OnPropertyChanged();
                return;
            }
            var old = Location.Name;
            Edit(old, name, v =>
            {
                Location.Name = v;
                foreach (var used in plc.Operations.SelectMany(o => o.Locations).Where(l => l.Location == old))
                {
                    used.Location = v;
                }
            });
        }
    }

    public string Number
    {
        get => Location.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, out var n) && n >= 0)
            {
                Edit(Location.Number, n, v => Location.Number = v);
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    public string UsedBy => string.Join(", ", plc.Operations.Where(o => o.Locations.Any(l => l.Location == Location.Name)).Select(o => o.Id));
}
