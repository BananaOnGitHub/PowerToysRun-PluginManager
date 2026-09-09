using System.Text.Json.Serialization;

namespace PowerToysRun.PluginManager.Core.Models;

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; init; } = [];
}

public sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }
}
