using PowerToysRun.PluginManager.Core.Models;

namespace PowerToysRun.PluginManager.Core.Services;

public static class PluginStateBuilder
{
    public static IReadOnlyList<PluginState> Build(
        CatalogDocument catalog,
        IReadOnlyList<InstalledPlugin> installedPlugins)
    {
        var byId = installedPlugins
            .Where(plugin => plugin.Manifest is not null)
            .GroupBy(plugin => plugin.Manifest!.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var byWebsite = installedPlugins
            .Where(plugin => Uri.TryCreate(plugin.Manifest?.Website, UriKind.Absolute, out _))
            .GroupBy(plugin => NormalizeUrl(plugin.Manifest!.Website!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var catalogEntriesByRepository = catalog.Plugins
            .Where(entry => !string.IsNullOrWhiteSpace(entry.RepositoryUrl))
            .GroupBy(entry => NormalizeUrl(entry.RepositoryUrl), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var states = catalog.Plugins.Select(entry =>
        {
            byId.TryGetValue(entry.Id, out var installed);
            if (installed is null && !string.IsNullOrWhiteSpace(entry.RepositoryUrl))
            {
                var repository = NormalizeUrl(entry.RepositoryUrl);
                if (byWebsite.TryGetValue(repository, out var candidates))
                {
                    installed = candidates.FirstOrDefault(candidate =>
                        string.Equals(
                            NormalizeName(candidate.Manifest!.Name),
                            NormalizeName(entry.Name),
                            StringComparison.OrdinalIgnoreCase));
                    installed ??= candidates.Count == 1 &&
                        catalogEntriesByRepository[repository] == 1
                            ? candidates[0]
                            : null;
                }
            }

            return new PluginState
            {
                CatalogEntry = entry,
                InstalledPlugin = installed,
            };
        }).ToList();

        var matchedDirectories = states
            .Where(state => state.InstalledPlugin is not null)
            .Select(state => state.InstalledPlugin!.DirectoryPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        states.AddRange(installedPlugins
            .Where(plugin => plugin.Manifest is not null && !matchedDirectories.Contains(plugin.DirectoryPath))
            .Select(plugin => new PluginState
            {
                CatalogEntry = new PluginCatalogEntry
                {
                    Id = plugin.Manifest!.Id,
                    Name = plugin.Manifest.Name,
                    Description = "Installed locally; this plugin is not in the current catalog.",
                    Author = plugin.Manifest.Author,
                    RepositoryUrl = plugin.Manifest.Website ?? string.Empty,
                    Website = plugin.Manifest.Website,
                },
                InstalledPlugin = plugin,
            }));

        return states;
    }

    private static string NormalizeUrl(string value) => value.Trim().TrimEnd('/').ToLowerInvariant();

    private static string NormalizeName(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
