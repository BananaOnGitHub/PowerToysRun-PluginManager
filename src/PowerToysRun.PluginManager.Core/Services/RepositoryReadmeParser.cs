using System.Net;
using System.Text.RegularExpressions;
using PowerToysRun.PluginManager.Core.Models;

namespace PowerToysRun.PluginManager.Core.Services;

public sealed class RepositoryReadmeMetadata
{
    public string? IconUrl { get; init; }

    public string? LongDescription { get; init; }

    public IReadOnlyList<string> ScreenshotUrls { get; init; } = [];
}

public static partial class RepositoryReadmeParser
{
    private const int MaximumDescriptionLength = 1200;
    private const int MaximumScreenshots = 6;

    public static RepositoryReadmeMetadata Parse(
        string markdown,
        GitHubRepository repository,
        string pluginName)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var images = EnumerateImages(markdown, repository)
            .DistinctBy(image => image.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var icon = images
            .Select((image, index) => new { Image = image, Score = ScoreIcon(image, pluginName, index) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Image.Url, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Image)
            .FirstOrDefault();
        var screenshotCandidates = images
            .Where(image => !string.Equals(image.Url, icon?.Url, StringComparison.OrdinalIgnoreCase))
            .Select((image, index) => new { Image = image, Score = ScoreScreenshot(image, index) })
            .Where(candidate => candidate.Score > 0)
            .ToList();
        var pluginSpecificScreenshots = screenshotCandidates
            .Where(candidate => MatchesPlugin(candidate.Image, pluginName))
            .ToList();
        var powerToysRunScreenshots = screenshotCandidates
            .Where(candidate => candidate.Image.Url.Contains("powertoysrun", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (powerToysRunScreenshots.Count > 0 && powerToysRunScreenshots.Count < screenshotCandidates.Count)
        {
            screenshotCandidates = powerToysRunScreenshots;
        }
        else if (screenshotCandidates.Count > 3 && pluginSpecificScreenshots.Count > 0)
        {
            screenshotCandidates = pluginSpecificScreenshots;
        }

        var screenshots = screenshotCandidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Image.Order)
            .Select(candidate => candidate.Image.Url)
            .Take(MaximumScreenshots)
            .ToList();

        return new RepositoryReadmeMetadata
        {
            IconUrl = icon?.Url,
            LongDescription = ExtractOverview(markdown),
            ScreenshotUrls = screenshots,
        };
    }

    private static IEnumerable<ReadmeImage> EnumerateImages(
        string markdown,
        GitHubRepository repository)
    {
        var order = 0;
        foreach (Match match in MarkdownImageRegex().Matches(markdown))
        {
            var source = match.Groups["source"].Value.Trim().Trim('<', '>');
            if (NormalizeImageUrl(source, repository) is { } url && !IsBadge(url))
            {
                yield return new ReadmeImage(
                    url,
                    WebUtility.HtmlDecode(match.Groups["alt"].Value),
                    order++);
            }
        }

        foreach (Match match in HtmlImageRegex().Matches(markdown))
        {
            var source = match.Groups["source"].Value;
            if (NormalizeImageUrl(source, repository) is { } url && !IsBadge(url))
            {
                yield return new ReadmeImage(
                    url,
                    WebUtility.HtmlDecode(match.Groups["alt"].Value),
                    order++);
            }
        }
    }

    private static string? NormalizeImageUrl(string value, GitHubRepository repository)
    {
        var decoded = WebUtility.HtmlDecode(value).Trim();
        if (decoded.Length == 0 || decoded.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Uri.TryCreate(decoded, UriKind.Absolute, out var absolute))
        {
            if (absolute.Scheme is not ("http" or "https") || !IsSupportedImagePath(absolute.AbsolutePath))
            {
                return null;
            }

            var normalized = ConvertGitHubBlobUrl(absolute) ?? absolute.AbsoluteUri;
            return CatalogMediaUrlPolicy.IsAllowed(normalized) ? normalized : null;
        }

        var path = decoded.Split(['?', '#'], 2)[0]
            .Replace('\\', '/')
            .TrimStart('.', '/');
        if (path.Length == 0 || path.Split('/').Any(segment => segment == "..") || !IsSupportedImagePath(path))
        {
            return null;
        }

        var encodedPath = string.Join(
            '/',
            path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        var rawUrl = $"https://raw.githubusercontent.com/{repository.Slug}/HEAD/{encodedPath}";
        return CatalogMediaUrlPolicy.IsAllowed(rawUrl) ? rawUrl : null;
    }

    private static string? ConvertGitHubBlobUrl(Uri uri)
    {
        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || !string.Equals(parts[2], "blob", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"https://raw.githubusercontent.com/{parts[0]}/{parts[1]}/{parts[3]}/{string.Join('/', parts.Skip(4))}";
    }

    private static bool IsSupportedImagePath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBadge(string url) => ContainsAny(
        url,
        "img.shields.io",
        "shields.io",
        "badge.svg",
        "badge-flat.svg",
        "/actions/workflows/",
        "codecov.io",
        "snyk.io",
        "awesome.re/mentioned",
        "github.com/downloads/");

    private static int ScoreIcon(ReadmeImage image, string pluginName, int index)
    {
        var haystack = $"{ImageFileName(image.Url)} {image.AltText}".ToLowerInvariant();
        var score = 0;
        if (ContainsAny(haystack, "screenshot", "screen-shot", "demo", "preview", "showcase", "gallery", "banner"))
        {
            score -= 160;
        }

        if (haystack.Contains("logo", StringComparison.Ordinal))
        {
            score += 140;
        }

        if (haystack.Contains("icon", StringComparison.Ordinal))
        {
            score += 110;
        }

        if (ContainsAny(haystack, ".light.", ".dark.", "light theme", "dark theme"))
        {
            score += 35;
        }

        if (haystack.Contains("settings", StringComparison.Ordinal))
        {
            score -= 100;
        }

        var normalizedName = NormalizeName(pluginName);
        if (normalizedName.Length > 2 &&
            NormalizeName(ImageFileName(image.Url)).Contains(normalizedName, StringComparison.Ordinal) &&
            ContainsAny(image.Url, "/assets/", "/images/", "/icons/", "/docs/", "/data/"))
        {
            score += 60;
        }

        if (image.Url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            score -= 80;
        }

        return score;
    }

    private static bool MatchesPlugin(ReadmeImage image, string pluginName)
    {
        var normalizedName = NormalizeName(pluginName);
        return normalizedName.Length > 2 &&
            NormalizeName($"{ImageFileName(image.Url)} {image.AltText}")
                .Contains(normalizedName, StringComparison.Ordinal);
    }

    private static string ImageFileName(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Path.GetFileName(uri.AbsolutePath)
            : Path.GetFileName(url);

    private static int ScoreScreenshot(ReadmeImage image, int index)
    {
        var haystack = $"{image.Url} {image.AltText}".ToLowerInvariant();
        var score = ContainsAny(
            haystack,
            "screenshot",
            "screen-shot",
            "demo",
            "preview",
            "showcase",
            "gallery",
            "in action",
            "how it looks")
            ? 120
            : 0;
        if (image.Url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (ContainsAny(haystack, "logo", " icon", ".light.", ".dark.", "fluentui-emoji"))
        {
            score -= 100;
        }

        return score - Math.Min(index, 20);
    }

    private static string? ExtractOverview(string markdown)
    {
        var heading = OverviewHeadingRegex().Match(markdown);
        if (!heading.Success)
        {
            return null;
        }

        var section = markdown[(heading.Index + heading.Length)..];
        var nextHeading = AnyHeadingRegex().Match(section);
        if (nextHeading.Success)
        {
            section = section[..nextHeading.Index];
        }

        section = FencedCodeRegex().Replace(section, string.Empty);
        section = HtmlElementRegex().Replace(section, string.Empty);
        section = MarkdownImageRegex().Replace(section, string.Empty);
        section = MarkdownLinkRegex().Replace(section, match => match.Groups["text"].Value);
        section = MarkdownDecorationRegex().Replace(section, string.Empty);

        var paragraphs = new List<string>();
        var current = new List<string>();
        foreach (var rawLine in section.Split('\n'))
        {
            var line = WebUtility.HtmlDecode(rawLine).Trim();
            var skip = line.StartsWith('|') ||
                line.StartsWith("-") ||
                line.StartsWith("*") ||
                line.StartsWith("<") ||
                line.StartsWith("[") ||
                line.StartsWith("---", StringComparison.Ordinal);
            if (line.Length == 0 || skip)
            {
                AddParagraph(paragraphs, current);
                if (paragraphs.Sum(paragraph => paragraph.Length) >= MaximumDescriptionLength)
                {
                    break;
                }

                continue;
            }

            current.Add(line);
        }

        AddParagraph(paragraphs, current);
        var result = string.Join(Environment.NewLine + Environment.NewLine, paragraphs).Trim();
        if (result.Length == 0)
        {
            return null;
        }

        return result.Length <= MaximumDescriptionLength
            ? result
            : result[..MaximumDescriptionLength].TrimEnd() + "…";
    }

    private static void AddParagraph(List<string> paragraphs, List<string> current)
    {
        if (current.Count == 0)
        {
            return;
        }

        paragraphs.Add(string.Join(' ', current));
        current.Clear();
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeName(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private sealed record ReadmeImage(string Url, string AltText, int Order);

    [GeneratedRegex("""!\[(?<alt>[^\]]*)\]\((?<source><[^>]+>|[^\s\)]+)(?:\s+['"].*?['"])?\)""", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownImageRegex();

    [GeneratedRegex("""<img\b(?=[^>]*\bsrc\s*=\s*['"](?<source>[^'"]+)['"])(?=[^>]*(?:\balt\s*=\s*['"](?<alt>[^'"]*)['"])?)[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlImageRegex();

    [GeneratedRegex(@"(?im)^#{1,4}\s+(?:[^\r\n]*?\s)?(?:overview|about|description)\s*$")]
    private static partial Regex OverviewHeadingRegex();

    [GeneratedRegex(@"(?m)^#{1,4}\s+")]
    private static partial Regex AnyHeadingRegex();

    [GeneratedRegex(@"```.*?```", RegexOptions.Singleline)]
    private static partial Regex FencedCodeRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlElementRegex();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\([^\)]+\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"[*_`>#]")]
    private static partial Regex MarkdownDecorationRegex();
}
