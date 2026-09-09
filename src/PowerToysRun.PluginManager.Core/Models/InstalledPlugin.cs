namespace PowerToysRun.PluginManager.Core.Models;

public sealed class InstalledPlugin
{
    public required string DirectoryPath { get; init; }

    public PluginManifest? Manifest { get; init; }

    public string? LoadError { get; init; }

    public bool IsValid => Manifest is not null && LoadError is null;
}

public sealed class PluginState
{
    public required PluginCatalogEntry CatalogEntry { get; init; }

    public InstalledPlugin? InstalledPlugin { get; init; }

    public bool IsInstalled => InstalledPlugin?.IsValid == true;

    public bool UpdateAvailable => IsInstalled &&
        LooseVersionComparer.IsNewer(
            CatalogEntry.LatestVersion,
            InstalledPlugin!.Manifest!.Version);
}

public static class LooseVersionComparer
{
    public static bool IsNewer(string? candidate, string? installed)
    {
        if (!TryParse(candidate, out var candidateVersion, out var candidatePrerelease) ||
            !TryParse(installed, out var installedVersion, out var installedPrerelease))
        {
            return false;
        }

        var comparison = candidateVersion.CompareTo(installedVersion);
        return comparison != 0 ? comparison > 0 : installedPrerelease && !candidatePrerelease;
    }

    private static bool TryParse(string? value, out Version version, out bool prerelease)
    {
        version = new Version();
        prerelease = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var separator = normalized.IndexOfAny(['-', '+']);
        prerelease = separator >= 0 && normalized[separator] == '-';
        if (separator >= 0)
        {
            normalized = normalized[..separator];
        }

        return Version.TryParse(normalized, out version!);
    }
}
