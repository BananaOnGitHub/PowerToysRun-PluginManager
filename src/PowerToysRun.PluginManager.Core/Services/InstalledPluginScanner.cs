using System.Text.Json;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Serialization;

namespace PowerToysRun.PluginManager.Core.Services;

public sealed class InstalledPluginScanner
{
    public async Task<IReadOnlyList<InstalledPlugin>> ScanAsync(
        string pluginDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            return [];
        }

        var results = new List<InstalledPlugin>();
        foreach (var directory in Directory.EnumerateDirectories(pluginDirectory).Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifestPath = Path.Combine(directory, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                results.Add(new InstalledPlugin
                {
                    DirectoryPath = directory,
                    LoadError = "plugin.json was not found.",
                });
                continue;
            }

            try
            {
                await using var stream = File.OpenRead(manifestPath);
                var manifest = await JsonSerializer.DeserializeAsync<PluginManifest>(
                    stream,
                    JsonDefaults.Options,
                    cancellationToken);
                results.Add(new InstalledPlugin
                {
                    DirectoryPath = directory,
                    Manifest = manifest,
                    LoadError = Validate(manifest, directory),
                });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                results.Add(new InstalledPlugin
                {
                    DirectoryPath = directory,
                    LoadError = exception.Message,
                });
            }
        }

        return results;
    }

    internal static bool IsSafeRelativePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !Path.IsPathRooted(path) &&
        !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment == "..");

    private static string? Validate(PluginManifest? manifest, string directory)
    {
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Name))
        {
            return "plugin.json is empty or missing its ID or name.";
        }

        if (!IsSafeRelativePath(manifest.ExecuteFileName))
        {
            return "The plugin executable path is invalid.";
        }

        return File.Exists(Path.Combine(directory, manifest.ExecuteFileName))
            ? null
            : $"The plugin executable '{manifest.ExecuteFileName}' was not found.";
    }
}
