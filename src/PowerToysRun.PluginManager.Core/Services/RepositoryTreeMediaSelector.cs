using PowerToysRun.PluginManager.Core.Models;

namespace PowerToysRun.PluginManager.Core.Services;

public static class RepositoryTreeMediaSelector
{
    private const int MaximumScreenshots = 6;

    public static RepositoryReadmeMetadata Select(
        IEnumerable<string> repositoryPaths,
        GitHubRepository repository,
        string pluginName)
    {
        var imagePaths = repositoryPaths
            .Where(IsSupportedImagePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var icon = imagePaths
            .Select(path => new { Path = path, Score = ScoreIcon(path, repository.Name, pluginName) })
            .Where(candidate => candidate.Score >= 100)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path.Length)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
        var screenshots = imagePaths
            .Where(path => !string.Equals(path, icon, StringComparison.OrdinalIgnoreCase))
            .Select(path => new { Path = path, Score = ScoreScreenshot(path, repository.Name, pluginName) })
            .Where(candidate => candidate.Score >= 100)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => RawUrl(repository, candidate.Path))
            .Take(MaximumScreenshots)
            .ToList();

        return new RepositoryReadmeMetadata
        {
            IconUrl = icon is null ? null : RawUrl(repository, icon),
            ScreenshotUrls = screenshots,
        };
    }

    private static int ScoreIcon(string path, string repositoryName, string pluginName)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var normalizedFileName = NormalizeName(fileName);
        var normalizedPluginName = NormalizeName(pluginName);
        var normalizedRepositoryName = NormalizeRepositoryName(repositoryName);
        var normalizedPath = NormalizeName(path);
        var lowerPath = path.ToLowerInvariant();
        var score = 0;

        if (ContainsAny(lowerPath, "screenshot", "screen-shot", "demo", "preview", "showcase", "banner", "settings"))
        {
            score -= 180;
        }

        if (ContainsAny(fileName, "logo", "icon"))
        {
            score += 120;
        }

        if (normalizedPluginName.Length > 2 && normalizedFileName.Contains(normalizedPluginName, StringComparison.Ordinal))
        {
            score += 130;
        }

        if (normalizedPluginName.Length > 2 &&
            normalizedRepositoryName.Contains(normalizedPluginName, StringComparison.Ordinal) &&
            ContainsAny(fileName, "logo", "icon"))
        {
            score += 70;
        }

        if (ContainsAny(lowerPath, "images/", "assets/", "icons/", "data/"))
        {
            score += 35;
        }

        if (normalizedPath.Contains("powertoysrun", StringComparison.Ordinal))
        {
            score += 140;
        }

        if (ContainsAny(lowerPath, "cmdpal", "commandpalette", "mcpserver", "dotnettool", "standalone"))
        {
            score -= 120;
        }

        if (ContainsAny(lowerPath, "/test", "/sample", "docs/"))
        {
            score -= 35;
        }

        return score;
    }

    private static int ScoreScreenshot(string path, string repositoryName, string pluginName)
    {
        var lowerPath = path.ToLowerInvariant();
        if (lowerPath.Contains("settings", StringComparison.Ordinal))
        {
            return 0;
        }

        var isScreenshot = ContainsAny(
            lowerPath,
            "screenshot",
            "screen-shot",
            "/demo",
            "demo-",
            "demo_",
            "/preview",
            "showcase");
        if (!isScreenshot)
        {
            return 0;
        }

        var normalizedPath = NormalizeName(path);
        var normalizedPluginName = NormalizeName(pluginName);
        var dedicatedRepository = NormalizeRepositoryName(repositoryName)
            .Contains(normalizedPluginName, StringComparison.Ordinal);
        if (!dedicatedRepository &&
            normalizedPluginName.Length > 2 &&
            !normalizedPath.Contains(normalizedPluginName, StringComparison.Ordinal))
        {
            return 0;
        }

        var score = 120;
        if (lowerPath.Contains("powertoysrun", StringComparison.Ordinal))
        {
            score += 35;
        }

        if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }

    private static string RawUrl(GitHubRepository repository, string path)
    {
        var encodedPath = string.Join(
            '/',
            path.Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        return $"https://raw.githubusercontent.com/{repository.Slug}/HEAD/{encodedPath}";
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

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeRepositoryName(string value) => NormalizeName(value)
        .Replace("powertoysrun", string.Empty, StringComparison.Ordinal)
        .Replace("community", string.Empty, StringComparison.Ordinal)
        .Replace("plugin", string.Empty, StringComparison.Ordinal);

    private static string NormalizeName(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
