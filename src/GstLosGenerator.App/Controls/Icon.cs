using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace GstLosGenerator.App.Controls;

// Icono de línea (trazos de Lucide, licencia ISC, en una rejilla de 24×24).
// Se dibuja con el color de texto heredado, así sigue al tema y al estado del botón que lo contiene.
public sealed class Icon : Control
{
    public static readonly StyledProperty<string?> KindProperty = AvaloniaProperty.Register<Icon, string?>(nameof(Kind));

    // Color del trazo. Se hereda del texto del contenedor (botón, tarjeta...).
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icon>();

    public static readonly StyledProperty<double> StrokeWidthProperty = AvaloniaProperty.Register<Icon, double>(nameof(StrokeWidth), 2);

    private const string Page = "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6";

    private static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["plus"] = "M5 12h14 M12 5v14",
        ["trash"] = "M3 6h18 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6 M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2",
        ["x"] = "M18 6L6 18 M6 6l12 12",
        ["check"] = "M20 6L9 17l-5-5",
        ["search"] = "M11 3a8 8 0 1 0 0 16a8 8 0 1 0 0-16z M21 21l-4.3-4.3",
        ["sun"] = "M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8z M12 2v2 M12 20v2 M4.9 4.9l1.4 1.4 M17.7 17.7l1.4 1.4 M2 12h2 M20 12h2 M6.3 17.7l-1.4 1.4 M19.1 4.9l-1.4 1.4",
        ["moon"] = "M12 3a6 6 0 0 0 9 9a9 9 0 1 1-9-9z",
        ["monitor"] = "M4 3h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M8 21h8 M12 17v4",
        ["globe"] = "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20z M2 12h20 M12 2a15.3 15.3 0 0 1 4 10a15.3 15.3 0 0 1-4 10a15.3 15.3 0 0 1-4-10a15.3 15.3 0 0 1 4-10z",
        ["info"] = "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20z M12 16v-4 M12 8h.01",
        ["alert"] = "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20z M12 8v4 M12 16h.01",
        ["save"] = "M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z M17 21v-8H7v8 M7 3v5h8",
        ["folder"] = "M6 14l1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.55 6a2 2 0 0 1-1.94 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2",
        ["file-plus"] = Page + " M12 18v-6 M9 15h6",
        ["file-sheet"] = Page + " M8 13h2 M14 13h2 M8 17h2 M14 17h2",
        ["file-code"] = Page + " M10 12l-2 2 2 2 M14 12l2 2-2 2",
        ["book"] = "M4 19.5A2.5 2.5 0 0 1 6.5 17H20 M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z",
        ["zap"] = "M13 2L3 14h9l-1 8 10-12h-9z",
        ["undo"] = "M9 14L4 9l5-5 M4 9h10.5a5.5 5.5 0 0 1 0 11H11",
        ["redo"] = "M15 14l5-5-5-5 M20 9H9.5a5.5 5.5 0 0 0 0 11H13",
        ["more"] = "M12 5h.01 M12 12h.01 M12 19h.01",
        ["box"] = "M21 8l-9-5-9 5v8l9 5 9-5z M3 8l9 5 9-5 M12 13v8",
        ["cpu"] = "M6 4h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M9 9h6v6H9z M9 1v3 M15 1v3 M9 20v3 M15 20v3 M20 9h3 M20 14h3 M1 9h3 M1 14h3",
        ["replace"] = "M16 3l4 4-4 4 M20 7H4 M8 21l-4-4 4-4 M4 17h16",
        ["list-plus"] = "M11 12H3 M16 6H3 M16 18H3 M18 9v6 M21 12h-6",
        ["layers"] = "M12 2l10 5-10 5L2 7z M2 17l10 5 10-5 M2 12l10 5 10-5",
        ["list-checks"] = "M3 17l2 2 4-4 M3 7l2 2 4-4 M13 6h8 M13 12h8 M13 18h8",
        ["map-pin"] = "M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0z M12 7a3 3 0 1 0 0 6a3 3 0 1 0 0-6z",
        ["arrow-up"] = "M12 19V5 M5 12l7-7 7 7",
        ["arrow-down"] = "M12 5v14 M19 12l-7 7-7-7",
        ["expand"] = "M7 15l5 5 5-5 M7 9l5-5 5 5",
        ["collapse"] = "M7 20l5-5 5 5 M7 4l5 5 5-5",
        ["copy"] = "M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H10a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2z M4 16a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2",
        ["download"] = "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3",
        ["upload"] = "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M17 8l-5-5-5 5 M12 3v12",
        ["grip"] = "M9 5h.01 M9 12h.01 M9 19h.01 M15 5h.01 M15 12h.01 M15 19h.01",
        ["list-ordered"] = "M10 12h11 M10 18h11 M10 6h11 M4 10h2 M4 6h1v4 M6 18H4c0-1 2-2 2-3s-1-1.5-2-1",
        ["sparkles"] = "M12 3l1.9 5.1L19 10l-5.1 1.9L12 17l-1.9-5.1L5 10l5.1-1.9z M5 3v4 M3 5h4 M19 17v4 M17 19h4",
    };

    private static readonly Dictionary<string, Geometry> Cache = new(StringComparer.OrdinalIgnoreCase);

    static Icon()
    {
        AffectsRender<Icon>(KindProperty, StrokeWidthProperty, ForegroundProperty);
        AffectsMeasure<Icon>(KindProperty);
    }

    public Icon()
    {
        Width = 16;
        Height = 16;
    }

    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public static bool IsKnown(string kind) => Paths.ContainsKey(kind);

    public override void Render(DrawingContext context)
    {
        if (Kind is not { } kind || !Paths.TryGetValue(kind, out var data))
        {
            return;
        }
        if (!Cache.TryGetValue(kind, out var geometry))
        {
            Cache[kind] = geometry = Geometry.Parse(data);
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24;
        if (scale <= 0)
        {
            return;
        }
        // El grosor va en unidades de la rejilla de 24 (como en Lucide): se escala con el icono.
        var pen = new Pen(Foreground ?? Brushes.Gray, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var offset = new Vector((Bounds.Width - 24 * scale) / 2, (Bounds.Height - 24 * scale) / 2);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }
}
