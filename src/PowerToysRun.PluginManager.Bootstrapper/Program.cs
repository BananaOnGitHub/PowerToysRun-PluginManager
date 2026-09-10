using System.Diagnostics;
using PowerToysRun.PluginManager.Core;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Services;

return await BootstrapperProgram.RunAsync(args);

internal static class BootstrapperProgram
{
    private const string PluginId = "9A68C4D267E24E88A20DF0E199C50E74";
    private const string PluginName = "Plugin Manager";

    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length != 1 ||
            arguments[0] is not ("--install" or "--uninstall"))
        {
            Console.Error.WriteLine("Usage: PowerToysRun.PluginManager.Bootstrapper <--install|--uninstall>");
            return 2;
        }

        var paths = new AppPaths();
        paths.EnsureDataDirectories();
        var targetDirectory = Path.Combine(paths.PluginDirectory, "PluginManager");
        var uninstalling = arguments[0] == "--uninstall";
        if (uninstalling && !Directory.Exists(targetDirectory))
        {
            return 0;
        }

        try
        {
            var transactionId = Guid.NewGuid();
            string? sourceDirectory = null;
            if (!uninstalling)
            {
                var bundledPlugin = Path.Combine(AppContext.BaseDirectory, "Bootstrap");
                if (!File.Exists(Path.Combine(bundledPlugin, "plugin.json")))
                {
                    throw new InvalidDataException("The installer bootstrap plugin is missing.");
                }

                sourceDirectory = Path.Combine(
                    paths.StagingDirectory,
                    transactionId.ToString("N"),
                    "PluginManager");
                CopyDirectory(bundledPlugin, sourceDirectory);
            }

            var plan = new TransactionPlan
            {
                Id = transactionId,
                PowerToysExecutablePath = FindPowerToysExecutable(),
                Operations =
                [
                    new PluginTransactionOperation
                    {
                        Kind = uninstalling
                            ? PluginTransactionKind.Uninstall
                            : Directory.Exists(targetDirectory)
                                ? PluginTransactionKind.Update
                                : PluginTransactionKind.Install,
                        PluginId = PluginId,
                        PluginName = PluginName,
                        SourceDirectory = sourceDirectory,
                        TargetDirectory = targetDirectory,
                        BackupDirectory = Path.Combine(
                            paths.BackupDirectory,
                            transactionId.ToString("N"),
                            "PluginManager"),
                    },
                ],
            };

            var planPath = await new TransactionPlanStore(paths).WriteAsync(plan);
            var updaterPath = Path.Combine(AppContext.BaseDirectory, "PowerToysRun.PluginManager.Updater.exe");
            if (!File.Exists(updaterPath))
            {
                throw new FileNotFoundException("The transaction updater is missing.", updaterPath);
            }

            var startInfo = new ProcessStartInfo(updaterPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(planPath);
            using var updater = Process.Start(startInfo) ??
                throw new InvalidOperationException("The transaction updater could not be started.");
            await updater.WaitForExitAsync();
            return updater.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 3;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var sourcePath in Directory.EnumerateFileSystemEntries(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(sourcePath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The bootstrap payload contains an unsupported link.");
            }

            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                Directory.CreateDirectory(destinationPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath);
            }
        }
    }

    private static string? FindPowerToysExecutable()
    {
        try
        {
            using var process = Process.GetProcessesByName("PowerToys").FirstOrDefault();
            var path = process?.MainModule?.FileName;
            if (File.Exists(path))
            {
                return path;
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }

        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerToys",
                "PowerToys.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PowerToys",
                "PowerToys.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
