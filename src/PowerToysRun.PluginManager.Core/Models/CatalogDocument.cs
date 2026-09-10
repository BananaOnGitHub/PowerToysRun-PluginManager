using System.Text.Json.Serialization;

namespace PowerToysRun.PluginManager.Core.Models;

public sealed class CatalogDocument
{
    public const string CurrentSchemaVersion = "1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public List<CatalogSource> Sources { get; init; } = [];

    public List<PluginCatalogEntry> Plugins { get; init; } = [];
}

public sealed class CatalogSource
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public CatalogSourceKind Kind { get; init; }
}

public enum CatalogSourceKind
{
    Microsoft,
    Community,
    Discovered,
    Manual,
}

public sealed class PluginCatalogEntry
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public string RepositoryUrl { get; init; } = string.Empty;

    public string? Website { get; init; }

    public string? IconUrl { get; init; }

    public string? LatestVersion { get; set; }

    public List<string> SourceIds { get; init; } = [];

    public List<string> Tags { get; init; } = [];

    public ArchitectureSupport Architectures { get; set; } = new();

    [JsonIgnore]
    public string RepositorySlug => GitHubRepository.TryParse(RepositoryUrl, out var repository)
        ? repository.Slug
        : string.Empty;
}

public sealed class ArchitectureSupport
{
    public bool X64 { get; init; }

    public bool Arm64 { get; init; }
}

public readonly record struct GitHubRepository(string Owner, string Name)
{
    public string Slug => $"{Owner}/{Name}";

    public static bool TryParse(string? value, out GitHubRepository repository)
    {
        repository = default;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        repository = new GitHubRepository(
            parts[0],
            parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? parts[1][..^4]
                : parts[1]);
        return true;
    }
}
