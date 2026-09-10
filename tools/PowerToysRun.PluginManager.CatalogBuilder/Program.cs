using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Serialization;

var options = BuilderOptions.Parse(args);
var configuration = await SourceConfiguration.LoadAsync(options.SourcesPath);
using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PowerToysRun-PluginManager-CatalogBuilder/0.1");
httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (!string.IsNullOrWhiteSpace(token))
{
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}

var collected = new List<RawCatalogEntry>();
foreach (var source in configuration.Sources)
{
    Console.WriteLine($"Reading {source.Name}...");
    var markdown = await httpClient.GetStringAsync(source.Url);
    collected.AddRange(source.Format switch
    {
        "table" => MarkdownCatalogParser.ParseTables(markdown, source),
        "list" => MarkdownCatalogParser.ParseList(markdown, source),
        _ => throw new InvalidDataException($"Unsupported source format '{source.Format}'."),
    });
}

var plugins = CatalogMerger.Merge(collected);
if (!options.SkipEnrichment)
{
    await GitHubEnricher.EnrichAsync(httpClient, plugins);
}

var catalog = new CatalogDocument
{
    GeneratedAt = DateTimeOffset.UtcNow,
    Sources = configuration.Sources.Select(source => new CatalogSource
    {
        Id = source.Id,
        Name = source.Name,
        Url = source.PublicUrl,
        Kind = source.Kind,
    }).ToList(),
    Plugins = plugins.OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase).ToList(),
};

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath))!);
await File.WriteAllTextAsync(
    options.OutputPath,
    JsonSerializer.Serialize(catalog, JsonDefaults.Options) + Environment.NewLine);
Console.WriteLine($"Wrote {catalog.Plugins.Count} plugins to {options.OutputPath}.");

internal sealed record BuilderOptions(string SourcesPath, string OutputPath, bool SkipEnrichment)
{
    public static BuilderOptions Parse(string[] arguments)
    {
        var sources = "registry/sources.json";
        var output = "catalog/catalog.json";
        var skipEnrichment = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--sources" when index + 1 < arguments.Length:
                    sources = arguments[++index];
                    break;
                case "--output" when index + 1 < arguments.Length:
                    output = arguments[++index];
                    break;
                case "--skip-enrichment":
                    skipEnrichment = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete argument '{arguments[index]}'.");
            }
        }

        return new BuilderOptions(sources, output, skipEnrichment);
    }
}

internal sealed class SourceConfiguration
{
    public List<SourceDefinition> Sources { get; init; } = [];

    public static async Task<SourceConfiguration> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SourceConfiguration>(stream, JsonDefaults.Options)
            ?? throw new InvalidDataException("The source configuration is empty.");
    }
}

internal sealed class SourceDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string PublicUrl { get; init; } = string.Empty;
    public CatalogSourceKind Kind { get; init; }
    public string Format { get; init; } = string.Empty;
    public string? StartHeading { get; init; }
    public string? EndHeading { get; init; }
}

internal sealed record RawCatalogEntry(
    string Name,
    string Description,
    string Author,
    string RepositoryUrl,
    string SourceId);

internal static partial class MarkdownCatalogParser
{
    public static IReadOnlyList<RawCatalogEntry> ParseTables(string markdown, SourceDefinition source)
    {
        var entries = new List<RawCatalogEntry>();
        foreach (var line in Slice(markdown, source).Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('|') || IsSeparatorRow(trimmed))
            {
                continue;
            }

            var cells = trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToList();
            if (cells.Count < 3 || cells[0].Contains("plugin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var repositoryUrl = LinkRegex().Matches(trimmed)
                .Select(match => match.Groups["url"].Value)
                .FirstOrDefault(IsRepositoryUrl);
            var name = PlainText(cells[0]);
            if (repositoryUrl is null || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            GitHubRepository.TryParse(repositoryUrl, out var repository);
            entries.Add(new RawCatalogEntry(
                name,
                PlainText(cells[2]),
                PlainText(cells[1]) is { Length: > 0 } author ? author : repository.Owner,
                CanonicalRepositoryUrl(repository),
                source.Id));
        }

        return entries;
    }

    public static IReadOnlyList<RawCatalogEntry> ParseList(string markdown, SourceDefinition source)
    {
        var entries = new List<RawCatalogEntry>();
        foreach (var line in Slice(markdown, source).Split('\n'))
        {
            if (!ListItemRegex().IsMatch(line))
            {
                continue;
            }

            var primaryLink = LinkRegex().Matches(line)
                .FirstOrDefault(match => IsRepositoryUrl(match.Groups["url"].Value));
            if (primaryLink is null)
            {
                continue;
            }

            var repositoryUrl = primaryLink.Groups["url"].Value;
            var name = primaryLink.Groups["text"].Value.Trim();
            GitHubRepository.TryParse(repositoryUrl, out var repository);
            var remainder = line[(primaryLink.Index + primaryLink.Length)..];
            entries.Add(new RawCatalogEntry(
                name,
                PlainText(remainder.TrimStart(' ', '-', '–', '—', ':')),
                repository.Owner,
                CanonicalRepositoryUrl(repository),
                source.Id));
        }

        return entries;
    }

    private static string Slice(string markdown, SourceDefinition source)
    {
        var start = 0;
        if (!string.IsNullOrWhiteSpace(source.StartHeading))
        {
            var heading = markdown.IndexOf(source.StartHeading, StringComparison.OrdinalIgnoreCase);
            start = heading < 0 ? 0 : heading + source.StartHeading.Length;
        }

        var end = markdown.Length;
        if (!string.IsNullOrWhiteSpace(source.EndHeading))
        {
            var heading = markdown.IndexOf(source.EndHeading, start, StringComparison.OrdinalIgnoreCase);
            end = heading < 0 ? end : heading;
        }

        return markdown[start..end];
    }

    private static bool IsSeparatorRow(string row) =>
        row.Replace("|", string.Empty).Replace(":", string.Empty).Replace("-", string.Empty).Trim().Length == 0;

    private static bool IsRepositoryUrl(string value) =>
        GitHubRepository.TryParse(value, out _) &&
        !value.Contains("/issues", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("/releases", StringComparison.OrdinalIgnoreCase);

    private static string CanonicalRepositoryUrl(GitHubRepository repository) =>
        $"https://github.com/{repository.Slug}";

    private static string PlainText(string value) => HtmlTagRegex().Replace(
        LinkRegex().Replace(value, match => match.Groups["text"].Value),
        string.Empty).Trim();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\((?<url>https?://[^)\s]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"^\s*[-*+]\s+")]
    private static partial Regex ListItemRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}

internal static class CatalogMerger
{
    public static List<PluginCatalogEntry> Merge(IEnumerable<RawCatalogEntry> entries) => entries
        .Where(entry => GitHubRepository.TryParse(entry.RepositoryUrl, out _))
        .GroupBy(entry =>
        {
            GitHubRepository.TryParse(entry.RepositoryUrl, out var repository);
            return $"{repository.Slug}|{NormalizeName(entry.Name)}";
        }, StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            var preferred = group.OrderByDescending(entry => entry.Description.Length).First();
            GitHubRepository.TryParse(preferred.RepositoryUrl, out var repository);
            return new PluginCatalogEntry
            {
                Id = $"{repository.Slug}:{NormalizeName(preferred.Name)}",
                Name = preferred.Name,
                Description = preferred.Description,
                Author = preferred.Author,
                RepositoryUrl = preferred.RepositoryUrl,
                Website = preferred.RepositoryUrl,
                IconUrl = $"https://github.com/{repository.Owner}.png?size=96",
                SourceIds = group.Select(entry => entry.SourceId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };
        })
        .ToList();

    private static string NormalizeName(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}

internal static class GitHubEnricher
{
    public static async Task EnrichAsync(HttpClient httpClient, IReadOnlyList<PluginCatalogEntry> plugins)
    {
        using var gate = new SemaphoreSlim(4);
        await Task.WhenAll(plugins.Select(async plugin =>
        {
            await gate.WaitAsync();
            try
            {
                await EnrichOneAsync(httpClient, plugin);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or JsonException or TaskCanceledException)
            {
                Console.Error.WriteLine($"Warning: could not enrich {plugin.RepositorySlug}: {exception.Message}");
            }
            finally
            {
                gate.Release();
            }
        }));
    }

    private static async Task EnrichOneAsync(HttpClient httpClient, PluginCatalogEntry plugin)
    {
        using var response = await httpClient.GetAsync(
            $"https://api.github.com/repos/{plugin.RepositorySlug}/releases/latest");
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonDefaults.Options);
        if (release is null)
        {
            return;
        }

        plugin.LatestVersion = release.TagName;
        var archives = release.Assets
            .Where(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var generic = archives.Count == 1 &&
            !ContainsAny(archives[0].Name, "x64", "amd64", "win64", "arm64", "aarch64");
        plugin.Architectures = new ArchitectureSupport
        {
            X64 = generic || archives.Any(asset => ContainsAny(asset.Name, "x64", "amd64", "win64")),
            Arm64 = generic || archives.Any(asset => ContainsAny(asset.Name, "arm64", "aarch64")),
        };
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
