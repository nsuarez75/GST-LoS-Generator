// Puente con TIA Portal V19 para GST LoS Generator. Lo lanza la app y sigue abierto mientras ella lo necesite:
// lee órdenes por la entrada estándar (una por línea, campos separados por tabuladores) y contesta por la salida
// estándar, una línea por mensaje («CLAVE texto»; ver TiaProtocol en el núcleo). Cada orden termina en DONE o ERROR.
//   projects                                                  proyectos abiertos en algún TIA
//   tree <proyecto>                                           PLC, carpetas y bloques del proyecto
//   export <proyecto> <PLC> <carpeta> <bloque> <archivo>      exporta un bloque a XML
//   import <proyecto> <PLC> <carpeta> <fb.xml> <salida.xml> [<udt>...]
//                                                             sube los UDT y el FB, compila y vuelve a exportar el FB
// Las carpetas van con sus nombres separados por «/»; vacía es la raíz de bloques del PLC.
// Al cerrarse la entrada estándar termina y cierra solo los TIA sin ventana que abrió él.
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GstLosGenerator.Tia;

internal static class Program
{
    private static string? _apiPath;

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);
        // Siemens.Engineering se carga de la instalación de TIA V19, antes de tocar nada de Openness.
        _apiPath = ApiPath();
        AppDomain.CurrentDomain.AssemblyResolve += ResolveOpenness;
        if (_apiPath == null)
        {
            Output.Error("No se encuentra TIA Portal V19 con Openness en este equipo.");
            return 2;
        }
        Output.Write("READY", "");
        Run();
        return 0;
    }

    // Aparte de Main para que Siemens.Engineering no se cargue antes de enganchar ResolveOpenness.
    private static void Run()
    {
        using var tia = new Tia();
        string? line;
        while ((line = Console.In.ReadLine()) != null)
        {
            var args = line.Split('\t');
            try
            {
                switch (args[0])
                {
                    case "projects":
                        tia.ListProjects();
                        break;
                    case "tree" when args.Length == 2:
                        tia.Tree(args[1]);
                        break;
                    case "export" when args.Length == 6:
                        tia.Export(args[1], args[2], args[3], args[4], args[5]);
                        break;
                    case "import" when args.Length >= 6:
                        tia.Import(args[1], args[2], args[3], args[4], args[5], args.Skip(6));
                        break;
                    case "quit":
                        return;
                    default:
                        throw new InvalidOperationException("Orden desconocida: " + args[0]);
                }
                Output.Write("DONE", "");
            }
            catch (Exception e)
            {
                Output.Error(e.Message);
            }
        }
    }

    private static Assembly? ResolveOpenness(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        if (_apiPath == null || !name.StartsWith("Siemens.Engineering", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var path = Path.Combine(Path.GetDirectoryName(_apiPath)!, name + ".dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }

    // Siemens.Engineering.dll de la API V19 según el registro de Openness; si no, la de una instalación estándar.
    private static string? ApiPath()
    {
        using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (var key = machine.OpenSubKey(@"SOFTWARE\Siemens\Automation\Openness\19.0\PublicAPI\V19"))
        {
            if (key?.GetValue("Siemens.Engineering") is string path && File.Exists(path))
            {
                return path;
            }
        }
        const string standard = @"C:\Program Files\Siemens\Automation\Portal V19\PublicAPI\V19\Siemens.Engineering.dll";
        return File.Exists(standard) ? standard : null;
    }
}

// Mensajes a la app: una línea por mensaje, sin saltos dentro del texto.
internal static class Output
{
    public static void Write(string key, string text)
    {
        Console.Out.WriteLine(key + " " + text.Replace("\r", " ").Replace("\n", " "));
        Console.Out.Flush();
    }

    public static void Error(string text) => Write("ERROR", text);
}
