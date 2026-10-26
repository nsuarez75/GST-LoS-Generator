// Jerarquía de los assets de un PLC, deducida de los nombres:
// celda (LX9_01_CELL01) -> asset de operación (LX9_01_1000FIX01_01_MOVEHR) -> sub-assets (..._UNXLOAD_01_RACK).
namespace GstLosGenerator.Core;

public sealed class AssetTree
{
    private readonly Dictionary<string, Asset?> _parents = [];
    private readonly Dictionary<string, List<Asset>> _children = [];

    public IReadOnlyList<Asset> Roots { get; }

    public AssetTree(IEnumerable<Asset> assets)
    {
        var list = assets.ToList();
        var roots = new List<Asset>();
        foreach (var asset in list)
        {
            var parent = FindParent(asset, list);
            _parents[asset.Element] = parent;
            if (parent is null)
            {
                roots.Add(asset);
            }
            else
            {
                if (!_children.TryGetValue(parent.Element, out var children))
                {
                    _children[parent.Element] = children = [];
                }
                children.Add(asset);
            }
        }
        Roots = roots;
    }

    public Asset? Parent(Asset asset) => _parents.GetValueOrDefault(asset.Element);

    public IReadOnlyList<Asset> Children(Asset asset) =>
        _children.TryGetValue(asset.Element, out var children) ? children : [];

    // Todos los descendientes en el orden del árbol.
    public IEnumerable<Asset> Descendants(Asset asset)
    {
        foreach (var child in Children(asset))
        {
            yield return child;
            foreach (var d in Descendants(child))
            {
                yield return d;
            }
        }
    }

    // Todos los assets en orden de árbol: cada uno seguido de sus descendientes.
    public IEnumerable<Asset> InOrder()
    {
        foreach (var root in Roots)
        {
            yield return root;
            foreach (var d in Descendants(root))
            {
                yield return d;
            }
        }
    }

    private static Asset? FindParent(Asset asset, List<Asset> all)
    {
        if (asset.Kind == AssetKind.Cell)
        {
            return null;
        }
        // El padre más cercano es el asset de nombre más largo que es prefijo de este.
        Asset? best = null;
        foreach (var other in all)
        {
            if (!ReferenceEquals(other, asset)
                && asset.Element.StartsWith(other.Element + "_", StringComparison.Ordinal)
                && (best is null || other.Element.Length > best.Element.Length))
            {
                best = other;
            }
        }
        if (best is not null)
        {
            return best;
        }
        // Si ninguno es prefijo, cuelga de la celda con el mismo prefijo de línea y celda.
        // El asset del PLC (LX9_76_PLC01) no tiene celda y queda en la raíz.
        var prefix = Naming.CellPrefix(asset.Element);
        return all.FirstOrDefault(a => a.Kind == AssetKind.Cell && !ReferenceEquals(a, asset)
            && prefix.Length > 0 && a.Element.StartsWith(prefix, StringComparison.Ordinal));
    }
}
