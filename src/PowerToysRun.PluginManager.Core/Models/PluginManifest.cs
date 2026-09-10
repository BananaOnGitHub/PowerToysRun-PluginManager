using System.Text.Json.Serialization;

namespace PowerToysRun.PluginManager.Core.Models;

public sealed class PluginManifest
{
    [JsonPropertyName("ID")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("ActionKeyword")]
    public string ActionKeyword { get; init; } = string.Empty;

    [JsonPropertyName("IsGlobal")]
    public bool IsGlobal { get; init; }

    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("Author")]
    public string Author { get; init; } = string.Empty;

    [JsonPropertyName("Version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("Language")]
    public string Language { get; init; } = string.Empty;

    [JsonPropertyName("Website")]
    public string? Website { get; init; }

    [JsonPropertyName("ExecuteFileName")]
    public string ExecuteFileName { get; init; } = string.Empty;

    [JsonPropertyName("IcoPathDark")]
    public string? IconPathDark { get; init; }

    [JsonPropertyName("IcoPathLight")]
    public string? IconPathLight { get; init; }

    [JsonPropertyName("DynamicLoading")]
    public bool DynamicLoading { get; init; }
}
