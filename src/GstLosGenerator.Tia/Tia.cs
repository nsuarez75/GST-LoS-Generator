// Trabajo con Openness: proyectos abiertos, árbol de bloques, exportar un bloque y subir el FB y los UDT.
// Nunca se hace Dispose de un TiaPortalProcess ni de un TiaPortal enganchado del usuario: cerraría su TIA.
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace GstLosGenerator.Tia;

internal sealed class Tia : IDisposable
{
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromMinutes(10);

    // Proyectos ya abiertos en esta sesión, por ruta.
    private readonly Dictionary<string, Project> _projects = new(StringComparer.OrdinalIgnoreCase);
    // TIA sin ventana abiertos aquí para leer, por ruta del proyecto: son los únicos que se cierran.
    private readonly Dictionary<string, TiaPortal> _own = new(StringComparer.OrdinalIgnoreCase);
    // TIA del usuario enganchados: se guardan para que no se liberen mientras dura la sesión.
    private readonly List<TiaPortal> _attached = [];

    public void ListProjects()
    {
        foreach (var process in TiaPortal.GetProcesses())
        {
            if (process.ProjectPath != null)
            {
                Output.Write("PROJECT", process.ProjectPath.FullName);
            }
        }
    }

    // PLC, carpetas y bloques: «PLC nombre», «GROUP plc|carpeta», «BLOCK plc|carpeta|bloque|tipo».
    public void Tree(string projectPath)
    {
        var project = OpenProject(projectPath, forWrite: false);
        foreach (var plc in Plcs(project))
        {
            Output.Write("PLC", plc.Name);
            WriteGroup(plc.Name, plc.BlockGroup, "");
        }
    }

    private static void WriteGroup(string plc, PlcBlockGroup group, string path)
    {
        foreach (var block in group.Blocks)
        {
            Output.Write("BLOCK", $"{plc}|{path}|{block.Name}|{block.GetType().Name}");
        }
        foreach (PlcBlockUserGroup sub in group.Groups)
        {
            var subPath = path.Length == 0 ? sub.Name : path + "/" + sub.Name;
            Output.Write("GROUP", $"{plc}|{subPath}");
            WriteGroup(plc, sub, subPath);
        }
    }

    public void Export(string projectPath, string plcName, string groupPath, string blockName, string file)
    {
        var plc = FindPlc(OpenProject(projectPath, forWrite: false), plcName);
        var block = FindGroup(plc, groupPath).Blocks.Find(blockName)
            ?? throw new InvalidOperationException($"No existe el bloque {blockName} en {plcName}.");
        ExportBlock(block, file);
    }

    private static void ExportBlock(PlcBlock block, string file)
    {
        if (block.IsKnowHowProtected)
        {
            throw new InvalidOperationException($"El bloque {block.Name} está protegido con contraseña.");
        }
        if (!block.IsConsistent)
        {
            throw new InvalidOperationException($"El bloque {block.Name} no está compilado en TIA. Compílalo y vuelve a intentarlo.");
        }
        if (File.Exists(file))
        {
            File.Delete(file);
        }
        block.Export(new FileInfo(file), ExportOptions.WithDefaults);
    }

    public void Import(string projectPath, string plcName, string groupPath, string fbFile, string outFile, IEnumerable<string> udtFiles)
    {
        var plc = FindPlc(OpenProject(projectPath, forWrite: true), plcName);
        var group = FindGroup(plc, groupPath);

        // Los UDT van antes porque el FB los usa. Se suben como fuente externa, se generan y se borra la fuente.
        foreach (var udt in udtFiles)
        {
            var name = Path.GetFileName(udt);
            Output.Write("PROGRESS", "Subiendo " + Path.GetFileNameWithoutExtension(udt) + "…");
            var sources = plc.ExternalSourceGroup.ExternalSources;
            sources.Find(name)?.Delete();
            var source = sources.CreateFromFile(name, udt);
            try
            {
                source.GenerateBlocksFromSource();
            }
            finally
            {
                source.Delete();
            }
            Output.Write("UDT", Path.GetFileNameWithoutExtension(udt));
        }

        Output.Write("PROGRESS", "Subiendo el FB…");
        var imported = group.Blocks.Import(new FileInfo(fbFile), ImportOptions.Override);
        var block = imported.FirstOrDefault() ?? throw new InvalidOperationException("TIA no ha creado el FB.");
        Output.Write("PLACED", groupPath + "|" + block.Name);

        Output.Write("PROGRESS", "Compilando…");
        var compilable = block.GetService<ICompilable>() ?? plc.BlockGroup.GetService<ICompilable>();
        if (compilable != null)
        {
            var result = compilable.Compile();
            Output.Write("COMPILE", $"{result.State}|{result.ErrorCount}|{result.WarningCount}");
            WriteMessages(result.Messages);
        }

        // El FB tal como queda en TIA, para que la app lo guarde como FB actual. Sin compilar no se puede exportar.
        try
        {
            ExportBlock(block, outFile);
            Output.Write("EXPORTED", outFile);
        }
        catch (Exception e)
        {
            Output.Write("WARN", e.Message);
        }
    }

    private static void WriteMessages(CompilerResultMessageComposition messages)
    {
        foreach (var message in messages)
        {
            if (message.Messages.Count > 0)
            {
                WriteMessages(message.Messages);
            }
            else if (message.State is CompilerResultState.Error or CompilerResultState.Warning)
            {
                Output.Write("MSG", $"{message.State}|{message.Path}|{message.Description}");
            }
        }
    }

    // El proyecto abierto en un TIA del usuario. Si no lo está: para leer, se abre en un TIA propio sin ventana;
    // para escribir, se abre en un TIA normal con ventana que se deja abierto para que el usuario lo revise y guarde.
    private Project OpenProject(string path, bool forWrite)
    {
        path = Path.GetFullPath(path);
        if (_projects.TryGetValue(path, out var cached) && !(forWrite && _own.ContainsKey(path)) && IsAlive(cached))
        {
            return cached;
        }
        _projects.Remove(path);
        if (forWrite && _own.TryGetValue(path, out var own))
        {
            // Abierto aquí sin ventana para leer: se cierra para poder abrirlo en un TIA con ventana.
            _own.Remove(path);
            own.Dispose();
        }

        var project = AttachTo(path);
        if (project == null)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("No existe el proyecto de TIA: " + path);
            }
            project = forWrite ? OpenWithWindow(path) : OpenWithoutWindow(path);
        }
        _projects[path] = project;
        return project;
    }

    private Project? AttachTo(string path)
    {
        var process = TiaPortal.GetProcesses().FirstOrDefault(p => p.ProjectPath != null && Same(p.ProjectPath.FullName, path));
        if (process == null)
        {
            return null;
        }
        Output.Write("PROGRESS", "Conectando con TIA Portal (acepta el aviso de TIA si lo pide)…");
        var portal = process.Attach();
        _attached.Add(portal);
        return portal.Projects.FirstOrDefault(p => Same(p.Path.FullName, path))
            ?? throw new InvalidOperationException("No se pudo acceder al proyecto abierto en TIA Portal.");
    }

    private Project OpenWithoutWindow(string path)
    {
        Output.Write("PROGRESS", "Abriendo el proyecto en TIA Portal sin ventana (puede tardar unos minutos)…");
        var portal = new TiaPortal(TiaPortalMode.WithoutUserInterface);
        try
        {
            var project = portal.Projects.Open(new FileInfo(path));
            _own[path] = portal;
            return project;
        }
        catch
        {
            portal.Dispose();
            throw;
        }
    }

    // Se abre como si el usuario hiciera doble clic en el proyecto: ese TIA no depende de este programa y sigue
    // abierto cuando termina. Se espera a que tenga el proyecto cargado y se engancha.
    private Project OpenWithWindow(string path)
    {
        Output.Write("PROGRESS", "Abriendo el proyecto en TIA Portal (puede tardar unos minutos)…");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        var limit = DateTime.UtcNow + OpenTimeout;
        while (DateTime.UtcNow < limit)
        {
            Thread.Sleep(2000);
            try
            {
                if (AttachTo(path) is { } project)
                {
                    return project;
                }
            }
            catch (Exception e) when (e is InvalidOperationException or EngineeringException)
            {
                // TIA aún está cargando el proyecto.
            }
        }
        throw new TimeoutException("TIA Portal no ha terminado de abrir el proyecto. Ábrelo tú y vuelve a intentarlo.");
    }

    private static bool IsAlive(Project project)
    {
        try
        {
            _ = project.Name;
            return true;
        }
        catch (Exception)
        {
            // El usuario cerró el proyecto o el TIA.
            return false;
        }
    }

    private static PlcSoftware FindPlc(Project project, string name) =>
        Plcs(project).FirstOrDefault(p => p.Name == name)
        ?? throw new InvalidOperationException($"El proyecto no tiene ningún PLC llamado {name}.");

    private static PlcBlockGroup FindGroup(PlcSoftware plc, string path)
    {
        PlcBlockGroup group = plc.BlockGroup;
        foreach (var name in path.Split(['/'], StringSplitOptions.RemoveEmptyEntries))
        {
            group = group.Groups.Find(name) ?? throw new InvalidOperationException($"No existe la carpeta {path} en {plc.Name}.");
        }
        return group;
    }

    private static IEnumerable<PlcSoftware> Plcs(Project project)
    {
        var devices = new List<Device>(project.Devices);
        var pending = new Queue<DeviceUserGroup>(project.DeviceGroups);
        while (pending.Count > 0)
        {
            var group = pending.Dequeue();
            devices.AddRange(group.Devices);
            foreach (DeviceUserGroup sub in group.Groups)
            {
                pending.Enqueue(sub);
            }
        }
        return devices.SelectMany(d => Software(d.DeviceItems));
    }

    private static IEnumerable<PlcSoftware> Software(DeviceItemComposition items)
    {
        foreach (DeviceItem item in items)
        {
            if (item.GetService<SoftwareContainer>()?.Software is PlcSoftware plc)
            {
                yield return plc;
            }
            foreach (var inner in Software(item.DeviceItems))
            {
                yield return inner;
            }
        }
    }

    private static bool Same(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    // Solo se cierran los TIA sin ventana abiertos aquí.
    public void Dispose()
    {
        foreach (var portal in _own.Values)
        {
            portal.Dispose();
        }
        _own.Clear();
    }
}
