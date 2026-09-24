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
        if (comparison != 0)
        {
            return comparison > 0;
        }

        if (candidatePrerelease is null || installedPrerelease is null)
        {
            return candidatePrerelease is null && installedPrerelease is not null;
        }

        var candidateParts = candidatePrerelease.Split('.');
        var installedParts = installedPrerelease.Split('.');
        for (var index = 0; index < Math.Min(candidateParts.Length, installedParts.Length); index++)
        {
            var leftNumeric = long.TryParse(candidateParts[index], out var left);
            var rightNumeric = long.TryParse(installedParts[index], out var right);
            comparison = leftNumeric && rightNumeric
                ? left.CompareTo(right)
                : leftNumeric != rightNumeric
                    ? (leftNumeric ? -1 : 1)
                    : StringComparer.OrdinalIgnoreCase.Compare(candidateParts[index], installedParts[index]);
            if (comparison != 0)
            {
                return comparison > 0;
            }
        }

        return candidateParts.Length > installedParts.Length;
    }

    private static bool TryParse(string? value, out Version version, out string? prerelease)
    {
        version = new Version();
        prerelease = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var metadata = normalized.IndexOf('+');
        if (metadata >= 0)
        {
            normalized = normalized[..metadata];
        }

        var separator = normalized.IndexOf('-');
        if (separator >= 0)
        {
            prerelease = normalized[(separator + 1)..];
            normalized = normalized[..separator];
        }

        if (prerelease is { Length: 0 } ||
            (prerelease is not null && prerelease.Split('.').Any(part => part.Length == 0)))
        {
            return false;
        }

        return Version.TryParse(normalized, out version!);
    }
}
