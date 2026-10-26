using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;

namespace GstLosGenerator.App.ViewModels;

public sealed record PlcSummary(Plc Plc)
{
    public string Name => Plc.Name;
    public string Number => Plc.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string Devices => Plc.Devices.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string Assets => Plc.Assets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string Operations => Plc.Operations.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string CurrentFb => Plc.CurrentFbFile is { } f ? Path.GetFileName(f) : "—";
}

// Ficha del proyecto: nombre, LoS, carpeta de salida y lista de PLCs.
public sealed partial class ProjectEditorViewModel(MainViewModel host) : EditableViewModel(host.Session)
{
    private Project Project => Session.Project;

    public MainViewModel Host { get; } = host;

    public ObservableCollection<PlcSummary> Plcs { get; } = new(host.Session.Project.Plcs.Select(p => new PlcSummary(p)));

    public string Name
    {
        get => Project.Name;
        set => Edit(Project.Name, value, v => Project.Name = v);
    }

    public string LosFile => Project.LosFile ?? L.T("Ninguna todavía");

    public string OutputFolder
    {
        get => Project.OutputFolder ?? "";
        set => Edit(Project.OutputFolder ?? "", value, v => Project.OutputFolder = v.Length == 0 ? null : v);
    }

    public bool IsEmpty => Plcs.Count == 0;

    [RelayCommand]
    private async Task PickOutputFolder()
    {
        if (await Host.Dialogs.PickFolder(L.T("Carpeta de salida")) is { } folder)
        {
            OutputFolder = folder;
        }
    }

    [RelayCommand]
    private void OpenPlc(PlcSummary? plc)
    {
        if (plc is not null)
        {
            Host.SelectedNode = Host.FindNode(NavKind.Plc, plc.Name);
        }
    }
}
