using System.Runtime.InteropServices;
using System.Text.Json;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Serialization;

namespace PowerToysRun.PluginManager.Core.Services;

public sealed class GitHubReleaseClient(HttpClient httpClient)
{
    public async Task<GitHubRelease> GetLatestAsync(
        string repositoryUrl,
        CancellationToken cancellationToken = default)
    {
        if (!GitHubRepository.TryParse(repositoryUrl, out var repository))
        {
            throw new InvalidOperationException("Only GitHub-hosted plugins are currently installable.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{repository.Slug}/releases/latest");
        request.Headers.UserAgent.ParseAdd("PowerToysRun-PluginManager/0.1");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonDefaults.Options, cancellationToken)
            ?? throw new JsonException("GitHub returned an empty release document.");
    }

    public async Task<GitHubRelease?> GetLatestDevelopmentAsync(
        string repositoryUrl,
        CancellationToken cancellationToken = default)
    {
        if (!GitHubRepository.TryParse(repositoryUrl, out var repository))
        {
            throw new InvalidOperationException("Only GitHub-hosted repositories are supported.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{repository.Slug}/releases?per_page=50");
        request.Headers.UserAgent.ParseAdd("PowerToysRun-PluginManager/0.4");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(
            stream, JsonDefaults.Options, cancellationToken)
            ?? throw new JsonException("GitHub returned an empty release list.");
        return releases
            .Where(release => release.Prerelease &&
                release.TagName.Contains("-dev.", StringComparison.OrdinalIgnoreCase))
            .Aggregate<GitHubRelease, GitHubRelease?>(null, (latest, release) =>
                latest is null || LooseVersionComparer.IsNewer(release.TagName, latest.TagName)
                    ? release
                    : latest);
    }

    public static GitHubReleaseAsset SelectAsset(
        GitHubRelease release,
        Architecture architecture = Architecture.X64)
    {
        var archives = release.Assets
            .Where(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var architectureTerms = architecture == Architecture.Arm64
            ? new[] { "arm64", "aarch64" }
            : new[] { "x64", "amd64", "win64" };
        var oppositeTerms = architecture == Architecture.Arm64
            ? new[] { "x64", "amd64", "win64" }
            : new[] { "arm64", "aarch64" };

        var match = archives.FirstOrDefault(asset =>
            architectureTerms.Any(term => asset.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        match ??= archives.Count == 1 &&
            !oppositeTerms.Any(term => archives[0].Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                ? archives[0]
                : null;

        return match ?? throw new InvalidOperationException(
            $"Release '{release.TagName}' does not contain a ZIP for {architecture}.");
    }

    public static GitHubReleaseAsset SelectManagerInstallerAsset(
        GitHubRelease release,
        Architecture architecture = Architecture.X64)
    {
        var architectureName = architecture == Architecture.Arm64 ? "arm64" : "x64";
        return release.Assets.FirstOrDefault(asset =>
                   asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                   asset.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) &&
                   asset.Name.Contains(architectureName, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException(
                   $"Release '{release.TagName}' does not contain a {architectureName} setup executable.");
    }
}
