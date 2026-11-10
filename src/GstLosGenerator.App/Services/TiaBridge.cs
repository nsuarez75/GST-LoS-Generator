// Conexión con TIA Portal a través del puente GstLosGenerator.Tia (.NET Framework 4.8), que va dentro de este
// ejecutable: se saca a %LOCALAPPDATA%\GST-LoS-Generator\tia\<versión> la primera vez y se lanza una vez por sesión.
// Se le mandan órdenes por la entrada estándar y se lee lo que contesta línea a línea (ver TiaProtocol).
using System.Diagnostics;
using System.Reflection;
using System.Text;
using GstLosGenerator.App.ViewModels;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Tia;

namespace GstLosGenerator.App.Services;

public sealed class TiaBridge : ITia
{
    private const string ResourceName = "GstLosGenerator.Tia.exe";

    public bool Available => OperatingSystem.IsWindows() && Assembly.GetExecutingAssembly().GetManifestResourceInfo(ResourceName) is not null;

    public ITiaSession Open() => new Session();

    // Saca el puente a disco si no está ya el mismo.
    private static string Extract()
    {
        var version = typeof(TiaBridge).Assembly.GetName().Version?.ToString() ?? "0";
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GST-LoS-Generator", "tia", version);
        var path = Path.Combine(folder, ResourceName);
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new TiaException(L.T("Esta versión de la app no lleva la conexión con TIA."));
        if (File.Exists(path) && new FileInfo(path).Length == resource.Length)
        {
            return path;
        }
        Directory.CreateDirectory(folder);
        var temp = path + ".tmp";
        using (var file = File.Create(temp))
        {
            resource.CopyTo(file);
        }
        File.Move(temp, path, overwrite: true);
        return path;
    }

    private sealed class Session : ITiaSession
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly string _temp = Path.Combine(Path.GetTempPath(), "GST-LoS-Generator", Guid.NewGuid().ToString("N"));
        private Process? _process;

        public async Task<IReadOnlyList<string>> OpenProjects()
        {
            var projects = new List<string>();
            await Send(["projects"], null, (key, text) =>
            {
                if (key == "PROJECT")
                {
                    projects.Add(text);
                }
            });
            return projects;
        }

        public async Task<TiaTree> Tree(string project, IProgress<string> progress)
        {
            var tree = new TiaTree();
            await Send(["tree", project], progress, (key, text) => TiaProtocol.ApplyTree(tree, key, text));
            return tree;
        }

        public async Task<string> Export(TiaLink block, IProgress<string> progress)
        {
            Directory.CreateDirectory(_temp);
            var file = Path.Combine(_temp, "export.xml");
            await Send(["export", block.Project, block.Plc, block.Group, block.Block, file], progress, (_, _) => { });
            return await File.ReadAllTextAsync(file);
        }

        public async Task<TiaImportResult> Import(TiaLink destination, string fbXml, IReadOnlyList<(string File, string Text)> udts,
            IProgress<string> progress)
        {
            var folder = Path.Combine(_temp, "subir");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
            Directory.CreateDirectory(folder);
            // Con BOM, como los archivos que escribe «Generar».
            var fb = Path.Combine(folder, Naming.FbName + ".xml");
            await File.WriteAllTextAsync(fb, fbXml, new UTF8Encoding(true));
            var udtFiles = new List<string>();
            foreach (var (name, text) in udts)
            {
                var path = Path.Combine(folder, name);
                await File.WriteAllTextAsync(path, text, new UTF8Encoding(true));
                udtFiles.Add(path);
            }
            var output = Path.Combine(folder, "actual.xml");
            var result = new TiaImportResult();
            await Send(["import", destination.Project, destination.Plc, destination.Group, fb, output, .. udtFiles], progress,
                (key, text) => TiaProtocol.ApplyImport(result, key, text));
            if (result.ExportedFile is not null && File.Exists(output))
            {
                result.ExportedXml = await File.ReadAllTextAsync(output);
            }
            return result;
        }

        // Manda una orden y lee hasta DONE (bien) o ERROR (lanza TiaException con el texto del puente).
        private async Task Send(string[] fields, IProgress<string>? progress, Action<string, string> onLine)
        {
            await _lock.WaitAsync();
            try
            {
                var process = await Start(progress);
                await process.StandardInput.WriteLineAsync(TiaProtocol.Command(fields));
                await process.StandardInput.FlushAsync();
                while (true)
                {
                    var line = await process.StandardOutput.ReadLineAsync()
                        ?? throw new TiaException(L.T("La conexión con TIA se ha cerrado."));
                    var (key, text) = TiaProtocol.Split(line);
                    switch (key)
                    {
                        case TiaProtocol.Done:
                            return;
                        case TiaProtocol.Error:
                            throw new TiaException(text);
                        case TiaProtocol.Progress:
                            progress?.Report(text);
                            break;
                        default:
                            onLine(key, text);
                            break;
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<Process> Start(IProgress<string>? progress)
        {
            if (_process is { HasExited: false })
            {
                return _process;
            }
            progress?.Report(L.T("Arrancando la conexión con TIA…"));
            string exe;
            try
            {
                exe = Extract();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new TiaException(L.T("No se pudo arrancar la conexión con TIA.") + " " + e.Message);
            }
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardInputEncoding = new UTF8Encoding(false),
            };
            var process = Process.Start(start) ?? throw new TiaException(L.T("No se pudo arrancar la conexión con TIA."));
            _ = process.StandardError.ReadToEndAsync();
            var line = await process.StandardOutput.ReadLineAsync();
            var (key, text) = line is null ? (TiaProtocol.Error, L.T("La conexión con TIA se ha cerrado.")) : TiaProtocol.Split(line);
            if (key != TiaProtocol.Ready)
            {
                throw new TiaException(text);
            }
            _process = process;
            return process;
        }

        // Al cerrar la entrada, el puente termina y cierra solo los TIA sin ventana que abrió él.
        public void Dispose()
        {
            if (_process is { HasExited: false } process)
            {
                try
                {
                    process.StandardInput.Close();
                }
                catch (IOException)
                {
                    // Ya se había cerrado.
                }
            }
            _process?.Dispose();
            _process = null;
            try
            {
                if (Directory.Exists(_temp))
                {
                    Directory.Delete(_temp, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Archivos temporales: si no se pueden borrar ahora, da igual.
            }
            _lock.Dispose();
        }
    }
}
