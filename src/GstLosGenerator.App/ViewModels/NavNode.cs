using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GstLosGenerator.Core;

namespace GstLosGenerator.App.ViewModels;

public enum NavKind
{
    Project,
    Plc,
    Devices,
    Assets,
    Operations,
    Locations,
}

// Nodo del árbol del proyecto (panel izquierdo).
public sealed partial class NavNode(NavKind kind, Plc? plc) : ObservableObject
{
    public NavKind Kind { get; } = kind;
    public Plc? Plc { get; } = plc;
    public ObservableCollection<NavNode> Children { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string Detail { get; set; } = "";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    // Icono sencillo de texto por tipo de nodo, sin depender de fuentes de iconos.
    public string Glyph => Kind switch
    {
        NavKind.Project => "▣",
        NavKind.Plc => "▤",
        NavKind.Devices => "⇄",
        NavKind.Assets => "◇",
        NavKind.Operations => "⚙",
        _ => "⌖",
    };

    public bool Matches(NavKind kind, string? plc) => Kind == kind && Plc?.Name == plc;
}
