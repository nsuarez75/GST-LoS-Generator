// Conexión con TIA Portal a través de TIA-Connector. Cada sesión arranca su puente (un proceso .NET Framework 4.8 que
// es el único que carga Openness) y lo cierra al terminar; el puente nunca cierra el TIA del usuario.
using System.Text;
using GstLosGenerator.App.ViewModels;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Tia;
using TiaConnector.Client;
using TiaConnector.Contracts;

namespace GstLosGenerator.App.Services;

public sealed class TiaConnection : ITia
{
    public bool Available => OperatingSystem.IsWindows();

    public ITiaSession Open() => new Session();

    private sealed class Session : ITiaSession
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly TempFolder _temp = new();
        private TiaClient? _client;
        private ProjectInfo? _project;

        public Task<IReadOnlyList<string>> OpenProjects() => Run(null, async client =>
        {
            var open = await client.GetOpenProjectsAsync();
            return (IReadOnlyList<string>)open.Select(p => p.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        });

        public Task<TiaTree> Tree(string project, IProgress<string> progress) => Run(progress, async client =>
        {
            var info = await Connect(client, project, write: false, progress);
            var plcs = info.Devices.Where(d => d.Kind == DeviceKind.Plc).Select(d => d.Name);
            var tree = await client.ReadTreeAsync(plcs, [], Progress(progress));
            return TiaTree.From(tree.Plcs);
        });

        public Task<string> Export(TiaLink block, IProgress<string> progress) => Run(progress, async client =>
        {
            await Connect(client, block.Project, write: false, progress);
            return await client.ReadBlockXmlAsync(Reference(block.Plc, block.Group, block.Block));
        });

        public Task<TiaImportResult> Import(TiaLink destination, string fbXml, IReadOnlyList<(string File, string Text)> udts,
            IProgress<string> progress) => Run(progress, async client =>
        {
            await Connect(client, destination.Project, write: true, progress);
            var folder = Path.Combine(_temp.Path, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var result = new TiaImportResult();

            // Los UDT van antes porque el FB los usa: se suben como fuente externa, se generan y se borra la fuente.
            // Con BOM, como los archivos que escribe «Generar».
            var udtFiles = new List<ImportFile>();
            foreach (var (name, text) in udts)
            {
                var path = Path.Combine(folder, name);
                await File.WriteAllTextAsync(path, text, new UTF8Encoding(true));
                udtFiles.Add(new ImportFile { Path = path, Plc = destination.Plc });
            }
            if (udtFiles.Count > 0)
            {
                var types = await client.ImportTypesAsync(udtFiles, Progress(progress));
                ThrowIfFailed(types);
                result.Udts.AddRange(udtFiles.Select(f => Path.GetFileNameWithoutExtension(f.Path)));
            }

            // El FB, encima del que haya en la carpeta de destino.
            var fb = Path.Combine(folder, Naming.FbName + ".xml");
            await File.WriteAllTextAsync(fb, fbXml, new UTF8Encoding(true));
            var blocks = await client.ImportBlocksAsync(new ImportBlocksArgs
            {
                Files = { new ImportFile { Path = fb, Plc = destination.Plc, Folders = Folders(destination.Group) } },
            }, Progress(progress));
            ThrowIfFailed(blocks);
            result.Group = destination.Group;
            result.Block = FileRules.ReadObjectName(fb) ?? Naming.FbName;

            var placed = Reference(destination.Plc, result.Group, result.Block);
            var compiled = await client.CompileAsync(new CompileArgs { Scope = CompileScope.Blocks, Blocks = { placed } }, Progress(progress));
            result.CompileState = compiled.State.ToString();
            result.Errors = compiled.Errors;
            result.Warnings = compiled.Warnings;
            result.Messages.AddRange(compiled.Messages.Select(m => $"{m.State} · {m.Path}: {m.Description}"));

            // El FB tal como queda en TIA, para que la app lo guarde como FB actual. Sin compilar no se puede exportar.
            try
            {
                result.ExportedXml = await client.ReadBlockXmlAsync(placed);
            }
            catch (TiaConnectorException e)
            {
                result.Notes.Add(L.T("No se ha podido volver a exportar el FB: {0}", Text(e)));
            }
            return result;
        });

        // Se engancha al proyecto si no lo estaba ya. Si no está abierto en ningún TIA: para leer se abre en un TIA sin
        // ventana; para subir, en un TIA con ventana que se queda abierto para que el usuario lo revise y guarde (si
        // estaba abierto sin ventana para leer, ese se cierra antes).
        private async Task<ProjectInfo> Connect(TiaClient client, string project, bool write, IProgress<string> progress)
        {
            var path = Path.GetFullPath(project);
            if (_project is { } current && string.Equals(Path.GetFullPath(current.ProjectPath), path, StringComparison.OrdinalIgnoreCase)
                && !(write && current.OpenedByBridge))
            {
                return current;
            }
            _project = null;
            _project = await client.ConnectAsync(path, write ? OpenMode.WithWindow : OpenMode.WithoutWindow, Progress(progress));
            return _project;
        }

        // Las peticiones van de una en una; los errores de TIA-Connector se pasan a TiaException con el texto en el idioma
        // de la app. Si se pierde la conexión, la siguiente petición vuelve a conectar.
        private async Task<T> Run<T>(IProgress<string>? progress, Func<TiaClient, Task<T>> work)
        {
            await _lock.WaitAsync();
            try
            {
                if (_client is null)
                {
                    progress?.Report(L.T("Arrancando la conexión con TIA…"));
                    _client = await TiaClient.StartAsync();
                }
                return await work(_client);
            }
            catch (TiaConnectorException e)
            {
                _project = null;
                if (e.Code == ErrorCode.BridgeExited)
                {
                    _client?.Dispose();
                    _client = null;
                }
                throw new TiaException(Text(e));
            }
            finally
            {
                _lock.Release();
            }
        }

        private static BlockRef Reference(string plc, string group, string name) =>
            new() { Plc = plc, Folders = Folders(group), Name = name };

        private static List<string> Folders(string group) => [.. group.Split('/', StringSplitOptions.RemoveEmptyEntries)];

        private static void ThrowIfFailed(BatchResult result)
        {
            if (result.Stopped)
            {
                throw new TiaException(L.T("El proyecto ya no está abierto en TIA Portal."));
            }
            if (result.Failures.Count > 0)
            {
                throw new TiaException(string.Join("\n", result.Failures.Select(f => $"{f.Item}: {Reason(f)}")));
            }
        }

        private static string Reason(ItemFailure failure) => failure.Code switch
        {
            FailureCode.Protected => L.T("está protegido con contraseña (know-how)"),
            FailureCode.NotCompiled => L.T("sin compilar"),
            FailureCode.DoesNotCompile => L.T("no compila; revisa sus errores en TIA"),
            FailureCode.NotFound => L.T("no existe en el proyecto"),
            FailureCode.PathTooLong => L.T("la ruta es demasiado larga para Windows"),
            _ => failure.Detail.Length > 0 ? failure.Detail : L.T("error de TIA sin detalle"),
        };

        private static string Text(TiaConnectorException e) => e.Code switch
        {
            ErrorCode.NoOpenness => L.T("No se encuentra TIA Portal V19 con Openness en este equipo."),
            ErrorCode.NoPermission => L.T("TIA Portal no ha dejado entrar a la aplicación: acepta su aviso o comprueba que tu usuario está en el grupo «Siemens TIA Openness»."),
            ErrorCode.ProjectNotOpen or ErrorCode.TiaClosed or ErrorCode.NotConnected => L.T("El proyecto ya no está abierto en TIA Portal."),
            ErrorCode.BridgeExited => L.T("La conexión con TIA se ha cerrado."),
            ErrorCode.NotFound => L.T("No existe en el proyecto: {0}", e.Message),
            _ => e.Message,
        };

        // Por dónde va la operación, en el idioma de la app.
        private static IProgress<ProgressInfo>? Progress(IProgress<string>? progress) =>
            progress is null ? null : new Progress<ProgressInfo>(p => progress.Report(Describe(p)));

        private static string Describe(ProgressInfo p) => p.Stage switch
        {
            ProgressStage.Connecting => L.T("Conectando con TIA Portal (acepta el aviso de TIA si lo pide)…"),
            ProgressStage.OpeningProject => L.T("Abriendo el proyecto en TIA Portal (puede tardar unos minutos)…"),
            ProgressStage.ReadingTree => L.T("Leyendo el proyecto de TIA…"),
            ProgressStage.Exporting => L.T("Exportando {0}…", p.Item),
            ProgressStage.Importing => L.T("Subiendo {0}…", p.Item),
            ProgressStage.Compiling => L.T("Compilando…"),
            _ => p.Item,
        };

        // Cierra el puente sin bloquear la ventana: termina lo pendiente y cierra solo el TIA sin ventana que abrió.
        public void Dispose()
        {
            var client = _client;
            _client = null;
            _project = null;
            if (client is not null)
            {
                _ = Task.Run(client.Dispose);
            }
            _temp.Dispose();
            _lock.Dispose();
        }
    }
}
