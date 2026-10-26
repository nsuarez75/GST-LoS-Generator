using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Fb;

namespace GstLosGenerator.App.ViewModels;

// Ventana «Generar»: PLC, FB actual, qué se reescribe y carpeta de salida.
// Lo que falta en el FB actual se añade siempre; de lo que ya está, solo se reescribe lo marcado.
public sealed partial class GenerateViewModel : ObservableObject
{
    private readonly MainViewModel _host;

    public IReadOnlyList<Plc> Plcs { get; }
    public ObservableCollection<GenerateAssetViewModel> Assets { get; } = [];
    public ObservableCollection<string> Notes { get; } = [];
    public ObservableCollection<string> Results { get; } = [];

    [ObservableProperty]
    public partial Plc? Plc { get; set; }

    [ObservableProperty]
    public partial bool Static { get; set; }

    [ObservableProperty]
    public partial bool Operations { get; set; }

    [ObservableProperty]
    public partial bool Calls { get; set; }

    [ObservableProperty]
    public partial bool Struct { get; set; }

    [ObservableProperty]
    public partial bool Swaps { get; set; }

    [ObservableProperty]
    public partial bool Udts { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public GenerateViewModel(MainViewModel host, Plc plc)
    {
        _host = host;
        Plcs = host.Session.Project.Plcs;
        Plc = plc;
    }

    // La ventana la cierra cuando termina bien.
    public event Action? Done;

    public string OutputFolder
    {
        get => _host.Session.Project.OutputFolder ?? "";
        set
        {
            if (value != OutputFolder)
            {
                _host.Session.Change("output-folder", () => _host.Session.Project.OutputFolder = value.Length == 0 ? null : value);
                OnPropertyChanged();
            }
        }
    }

    public bool HasCurrentFb => Plc?.CurrentFb is not null;
    public string CurrentFb => Plc?.CurrentFbFile is { } f ? f : L.T("Ninguno: se genera desde cero con todos los segmentos.");

    partial void OnPlcChanged(Plc? value) => Refresh();

    private void Refresh()
    {
        Assets.Clear();
        Notes.Clear();
        Results.Clear();
        OnPropertyChanged(nameof(HasCurrentFb));
        OnPropertyChanged(nameof(CurrentFb));
        if (Plc is null)
        {
            return;
        }
        var inFb = new HashSet<string>();
        if (Plc.CurrentFb is { } xml)
        {
            try
            {
                foreach (var segment in FbDocument.Parse(xml).Segments.Where(s => s.Key?.StartsWith("call:", StringComparison.Ordinal) == true))
                {
                    inFb.Add(segment.Key!["call:".Length..]);
                }
            }
            catch (FbFormatException e)
            {
                Notes.Add(L.T("El FB actual guardado no se puede leer: {0}", e.Message));
            }
        }
        var tree = new AssetTree(Plc.Assets.Where(a => !a.Missing));
        foreach (var root in tree.Roots)
        {
            AddAsset(root, tree, 0, inFb);
        }
        foreach (var issue in Validation.Check(Plc, _host.Session.Catalog))
        {
            Notes.Add(issue.Text);
        }
    }

    private void AddAsset(Asset asset, AssetTree tree, int depth, HashSet<string> inFb)
    {
        var note = !HasCurrentFb ? "" : inFb.Contains(asset.Element) ? "" : L.T("nuevo: se añade");
        Assets.Add(new GenerateAssetViewModel(asset.Element, depth, note));
        foreach (var child in tree.Children(asset))
        {
            AddAsset(child, tree, depth + 1, inFb);
        }
    }

    [RelayCommand]
    private void MarkAll()
    {
        foreach (var a in Assets)
        {
            a.IsChecked = true;
        }
    }

    [RelayCommand]
    private void MarkNone()
    {
        foreach (var a in Assets)
        {
            a.IsChecked = false;
        }
    }

    [RelayCommand]
    private async Task PickFolder()
    {
        if (await _host.Dialogs.PickFolder(L.T("Carpeta de salida")) is { } folder)
        {
            OutputFolder = folder;
        }
    }

    [RelayCommand]
    private async Task ReadFb()
    {
        if (Plc is not null)
        {
            await _host.ReadFbFor(Plc);
            Refresh();
        }
    }

    private FbParts Parts =>
        (Static ? FbParts.Static : 0) | (Operations ? FbParts.Operations : 0) | (Calls ? FbParts.Calls : 0)
        | (Struct ? FbParts.Struct : 0) | (Swaps ? FbParts.Swaps : 0);

    [RelayCommand]
    private async Task Generate()
    {
        if (Plc is not { } plc)
        {
            return;
        }
        var dialogs = _host.Dialogs;
        var folder = OutputFolder;
        if (folder.Length == 0)
        {
            folder = await dialogs.PickFolder(L.T("Carpeta de salida")) ?? "";
            if (folder.Length == 0)
            {
                return;
            }
            OutputFolder = folder;
        }
        var options = new GenerateOptions
        {
            Parts = Parts,
            Assets = Assets.Where(a => a.IsChecked).Select(a => a.Element).ToHashSet(),
        };
        IsBusy = true;
        GenerateResult result;
        try
        {
            result = await Task.Run(() => FbGenerator.Generate(plc, _host.Session.Catalog, plc.CurrentFb, options));
            if (result.PinLosses.Count > 0)
            {
                var text = string.Join("\n", result.PinLosses.Select(p => L.T("{0} → {1}: pines {2}", p.Element, p.Fb, string.Join(", ", p.Pins))));
                if (await dialogs.Confirm(L.T("Pines cableados a mano"),
                        L.T("Estas llamadas tienen pines conectados a mano que su FB nuevo ya no tiene:\n\n{0}\n\n¿Regenerarlas igualmente? Esas conexiones se perderán.", text),
                        L.T("Regenerar")))
                {
                    options.ConfirmedPinLoss = result.PinLosses.Select(p => p.Element).ToHashSet();
                    result = await Task.Run(() => FbGenerator.Generate(plc, _host.Session.Catalog, plc.CurrentFb, options));
                }
            }
        }
        catch (FbFormatException e)
        {
            await dialogs.Message(L.T("Generar"), L.T("No se pudo usar el FB actual: {0}", e.Message));
            return;
        }
        finally
        {
            IsBusy = false;
        }

        var files = new List<(string Path, string Text, bool Bom)> { (Path.Combine(folder, Naming.FbName + ".xml"), result.Xml, true) };
        if (Udts)
        {
            files.AddRange(plc.Devices.Select(d => (Path.Combine(folder, UdtWriter.FileName(plc, d)), UdtWriter.Write(plc, d), true)));
        }
        var existing = files.Where(f => File.Exists(f.Path)).Select(f => Path.GetFileName(f.Path)).ToList();
        if (existing.Count > 0 && !await dialogs.Confirm(L.T("Sobrescribir"),
                L.T("Ya existen en la carpeta de salida:\n{0}\n\n¿Sobrescribirlos?", string.Join("\n", existing)), L.T("Sobrescribir")))
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var (path, text, bom) in files)
            {
                await File.WriteAllTextAsync(path, text, new UTF8Encoding(bom));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await dialogs.Message(L.T("Generar"), L.T("No se pudo escribir en {0}: {1}", folder, e.Message));
            return;
        }
        Results.Clear();
        foreach (var (path, _, _) in files)
        {
            Results.Add(L.T("Escrito: {0}", Path.GetFileName(path)));
        }
        foreach (var warning in result.Warnings)
        {
            Results.Add("⚠ " + warning);
        }
        await dialogs.Message(L.T("Generado {0}", plc.Name), string.Join('\n', Results));
        Done?.Invoke();
    }
}

public sealed partial class GenerateAssetViewModel(string element, int depth, string note) : ObservableObject
{
    public string Element { get; } = element;
    public Avalonia.Thickness Indent { get; } = new(depth * 18, 0, 0, 0);
    public string Note { get; } = note;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
