using System.Text.Json;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Serialization;

namespace PowerToysRun.PluginManager.Core.Services;

public sealed class CatalogClient(HttpClient httpClient, AppPaths paths)
{
    public static readonly Uri DefaultCatalogUri = new(
        "https://raw.githubusercontent.com/BananaOnGitHub/PowerToysRun-PluginManager/main/catalog/catalog.json");

    public async Task<CatalogDocument> LoadAsync(
        string bundledCatalogPath,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureDataDirectories();
        var cachePath = Path.Combine(paths.CacheDirectory, "catalog.json");

        try
        {
            using var response = await httpClient.GetAsync(DefaultCatalogUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var remote = await DeserializeAsync(responseStream, cancellationToken);

            var temporaryPath = cachePath + ".tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(remote, JsonDefaults.Options),
                cancellationToken);
            File.Move(temporaryPath, cachePath, true);
            return remote;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or JsonException or TaskCanceledException)
        {
            var fallbackPath = File.Exists(cachePath) ? cachePath : bundledCatalogPath;
            await using var stream = File.OpenRead(fallbackPath);
            return await DeserializeAsync(stream, cancellationToken);
        }
    }

    private static async Task<CatalogDocument> DeserializeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var catalog = await JsonSerializer.DeserializeAsync<CatalogDocument>(
            stream,
            JsonDefaults.Options,
            cancellationToken) ?? throw new JsonException("The catalog document was empty.");

        if (catalog.SchemaVersion != CatalogDocument.CurrentSchemaVersion)
        {
            throw new JsonException($"Unsupported catalog schema '{catalog.SchemaVersion}'.");
        }

        return catalog;
    }
}
