// Lo que los modelos de vista piden a TIA Portal. La conexión real está en Services/TiaBridge.cs; en los tests se
// sustituye por una que responde sola.
using GstLosGenerator.Core;
using GstLosGenerator.Core.Tia;

namespace GstLosGenerator.App.ViewModels;

public interface ITia
{
    // false fuera de Windows o si esta versión no lleva el puente: entonces solo se trabaja con archivos.
    bool Available { get; }

    // Una sesión con TIA: mientras dura, los proyectos abiertos sin ventana siguen abiertos.
    ITiaSession Open();
}

public interface ITiaSession : IDisposable
{
    // Rutas de los proyectos abiertos en algún TIA del usuario.
    Task<IReadOnlyList<string>> OpenProjects();

    Task<TiaTree> Tree(string project, IProgress<string> progress);

    // XML del bloque.
    Task<string> Export(TiaLink block, IProgress<string> progress);

    // Sube los UDT (nombre de archivo y texto) y el FB a la carpeta de destino, compila y vuelve a exportar el FB.
    Task<TiaImportResult> Import(TiaLink destination, string fbXml, IReadOnlyList<(string File, string Text)> udts,
        IProgress<string> progress);
}

// Sin TIA: la app solo lee y escribe archivos.
public sealed class NoTia : ITia
{
    public bool Available => false;

    public ITiaSession Open() => throw new InvalidOperationException("TIA Portal no está disponible.");
}
