using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using PowerToysRun.PluginManager.Core;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Serialization;
using PowerToysRun.PluginManager.Core.Services;

return await UpdaterProgram.RunAsync(args);

internal static class UpdaterProgram
{
    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length != 1)
        {
            Console.Error.WriteLine("Usage: PowerToysRun.PluginManager.Updater <transaction.json>");
            return 2;
        }

        var paths = new AppPaths();
        string planPath;
        TransactionPlan plan;
        try
        {
            planPath = Path.GetFullPath(arguments[0]);
            if (!PathPolicy.IsWithin(planPath, paths.TransactionDirectory))
            {
                throw new InvalidDataException("The transaction file is outside the manager transaction directory.");
            }

            plan = await TransactionPlanStore.ReadAsync(planPath);
            TransactionValidator.Validate(plan, paths);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Invalid transaction: {exception.Message}");
            return 3;
        }

        var powerToysExecutable = ResolvePowerToysExecutable(plan.PowerToysExecutablePath);
        try
        {
            Console.WriteLine("Closing PowerToys...");
            await PowerToysShutdown.CloseAsync(TimeSpan.FromSeconds(25));
        }
        catch (TimeoutException exception)
        {
            await WriteResultAsync(planPath, false, exception.Message);
            Console.Error.WriteLine(exception.Message);
            return 4;
        }

        try
        {
            ApplyTransaction(plan);
            await WriteResultAsync(planPath, true, null);
            Console.WriteLine("Plugin changes applied.");
            return 0;
        }
        catch (Exception exception)
        {
            await WriteResultAsync(planPath, false, exception.Message);
            Console.Error.WriteLine($"Transaction failed and was rolled back: {exception.Message}");
            return 5;
        }
        finally
        {
            if (plan.RestartPowerToys && powerToysExecutable is not null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(powerToysExecutable) { UseShellExecute = true });
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"PowerToys could not be restarted: {exception.Message}");
                }
            }
        }
    }

    private static void ApplyTransaction(TransactionPlan plan)
    {
        var applied = new List<PluginTransactionOperation>();
        try
        {
            foreach (var operation in plan.Operations)
            {
                applied.Add(operation);
                Directory.CreateDirectory(Path.GetDirectoryName(operation.BackupDirectory)!);
                if (Directory.Exists(operation.TargetDirectory))
                {
                    Directory.Move(operation.TargetDirectory, operation.BackupDirectory);
                }

                if (operation.Kind != PluginTransactionKind.Uninstall)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetDirectory)!);
                    Directory.Move(operation.SourceDirectory!, operation.TargetDirectory);
                }
            }
        }
        catch
        {
            RollBack(applied);
            throw;
        }
    }

    private static void RollBack(IEnumerable<PluginTransactionOperation> applied)
    {
        foreach (var operation in applied.Reverse())
        {
            try
            {
                if (operation.Kind != PluginTransactionKind.Uninstall &&
                    Directory.Exists(operation.TargetDirectory) &&
                    operation.SourceDirectory is not null &&
                    !Directory.Exists(operation.SourceDirectory))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(operation.SourceDirectory)!);
                    Directory.Move(operation.TargetDirectory, operation.SourceDirectory);
                }

                if (Directory.Exists(operation.BackupDirectory) &&
                    !Directory.Exists(operation.TargetDirectory))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetDirectory)!);
                    Directory.Move(operation.BackupDirectory, operation.TargetDirectory);
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Rollback warning for {operation.PluginName}: {exception.Message}");
            }
        }
    }

    private static string? ResolvePowerToysExecutable(string? configuredPath)
    {
        if (File.Exists(configuredPath))
        {
            return configuredPath;
        }

        try
        {
            using var process = Process.GetProcessesByName("PowerToys").FirstOrDefault();
            var runningPath = process?.MainModule?.FileName;
            return File.Exists(runningPath) ? runningPath : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static Task WriteResultAsync(string planPath, bool succeeded, string? error) =>
        File.WriteAllTextAsync(
            planPath + ".result.json",
            JsonSerializer.Serialize(
                new TransactionResult(succeeded, DateTimeOffset.UtcNow, error),
                JsonDefaults.Options));
}

internal static class TransactionValidator
{
    public static void Validate(TransactionPlan plan, AppPaths paths)
    {
        if (plan.Operations.Count == 0)
        {
            throw new InvalidDataException("The transaction contains no operations.");
        }

        foreach (var operation in plan.Operations)
        {
            if (string.IsNullOrWhiteSpace(operation.PluginId) ||
                string.IsNullOrWhiteSpace(operation.PluginName) ||
                !PathPolicy.IsWithin(operation.TargetDirectory, paths.PluginDirectory) ||
                !PathPolicy.IsWithin(operation.BackupDirectory, paths.BackupDirectory))
            {
                throw new InvalidDataException("The transaction contains an invalid plugin path or identity.");
            }

            if (operation.Kind != PluginTransactionKind.Uninstall &&
                (operation.SourceDirectory is null ||
                 !PathPolicy.IsWithin(operation.SourceDirectory, paths.StagingDirectory) ||
                 !Directory.Exists(operation.SourceDirectory)))
            {
                throw new InvalidDataException($"The staged package for {operation.PluginName} is missing or invalid.");
            }

            if (Directory.Exists(operation.BackupDirectory))
            {
                throw new InvalidDataException($"The backup target for {operation.PluginName} already exists.");
            }
        }
    }
}

internal static class PathPolicy
{
    public static bool IsWithin(string candidate, string root)
    {
        var fullCandidate = Path.GetFullPath(candidate);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class PowerToysShutdown
{
    private static readonly string[] ProcessNames = ["PowerToys", "PowerToys.PowerLauncher"];

    public static async Task CloseAsync(TimeSpan timeout)
    {
        foreach (var processName in ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    NativeMethods.RequestClose(process.Id);
                }
            }
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!AnyProcessRunning())
            {
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            "PowerToys did not close in time. No plugin files were changed; exit PowerToys manually and retry.");
    }

    private static bool AnyProcessRunning()
    {
        foreach (var processName in ProcessNames)
        {
            var processes = Process.GetProcessesByName(processName);
            var running = processes.Length > 0;
            foreach (var process in processes)
            {
                process.Dispose();
            }

            if (running)
            {
                return true;
            }
        }

        return false;
    }
}

internal static class NativeMethods
{
    private const uint WmClose = 0x0010;

    public static void RequestClose(int processId)
    {
        EnumWindows(
            (windowHandle, _) =>
            {
                GetWindowThreadProcessId(windowHandle, out var ownerProcessId);
                if (ownerProcessId == processId)
                {
                    PostMessage(windowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
                }

                return true;
            },
            IntPtr.Zero);
    }

    private delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);
}

internal sealed record TransactionResult(bool Succeeded, DateTimeOffset CompletedAt, string? Error);
