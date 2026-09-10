using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Serialization;

namespace PowerToysRun.PluginManager.Core.Services;

public sealed class PackageStagingService(
    HttpClient httpClient,
    GitHubReleaseClient releaseClient,
    AppPaths paths)
{
    private const long MaximumDownloadBytes = 250L * 1024 * 1024;
    private const long MaximumExtractedBytes = 500L * 1024 * 1024;
    private const int MaximumEntryCount = 5000;

    public async Task<StagedPluginPackage> StageLatestAsync(
        PluginCatalogEntry entry,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureDataDirectories();
        var release = await releaseClient.GetLatestAsync(entry.RepositoryUrl, cancellationToken);
        var asset = GitHubReleaseClient.SelectAsset(release, RuntimeInformation.OSArchitecture);
        if (asset.Size > MaximumDownloadBytes)
        {
            throw new InvalidDataException("The release archive is larger than the allowed package size.");
        }

        var stageDirectory = Path.Combine(paths.StagingDirectory, Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(stageDirectory, "plugin.zip");
        var extractDirectory = Path.Combine(stageDirectory, "payload");
        Directory.CreateDirectory(extractDirectory);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
            request.Headers.UserAgent.ParseAdd("PowerToysRun-PluginManager/0.1");
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumDownloadBytes)
            {
                throw new InvalidDataException("The release archive is larger than the allowed package size.");
            }

            await using (var destination = File.Create(archivePath))
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                await CopyWithLimitAsync(source, destination, MaximumDownloadBytes, cancellationToken);
            }

            await ExtractSafelyAsync(archivePath, extractDirectory, cancellationToken);
            var pluginDirectory = await FindAndValidatePluginDirectoryAsync(
                extractDirectory,
                entry,
                cancellationToken);

            return new StagedPluginPackage
            {
                Manifest = await ReadManifestAsync(Path.Combine(pluginDirectory, "plugin.json"), cancellationToken),
                Release = release,
                Asset = asset,
                PluginDirectory = pluginDirectory,
                Sha256 = await ComputeSha256Async(archivePath, cancellationToken),
            };
        }
        catch
        {
            TryDeleteDirectory(stageDirectory);
            throw;
        }
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("The release archive exceeded the allowed package size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task ExtractSafelyAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaximumEntryCount)
        {
            throw new InvalidDataException("The release archive contains too many files.");
        }

        long extractedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            extractedBytes += entry.Length;
            if (extractedBytes > MaximumExtractedBytes)
            {
                throw new InvalidDataException("The extracted plugin would exceed the allowed size.");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The release archive contains an unsafe path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var input = entry.Open();
            await using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            await input.CopyToAsync(output, cancellationToken);
        }
    }

    private static async Task<string> FindAndValidatePluginDirectoryAsync(
        string extractDirectory,
        PluginCatalogEntry catalogEntry,
        CancellationToken cancellationToken)
    {
        var candidates = Directory.EnumerateFiles(extractDirectory, "plugin.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}__MACOSX{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidDataException("The release archive does not contain plugin.json.");
        }

        var validated = new List<(string Directory, PluginManifest Manifest)>();
        foreach (var manifestPath in candidates)
        {
            var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
            var directory = Path.GetDirectoryName(manifestPath)!;
            if (InstalledPluginScanner.IsSafeRelativePath(manifest.ExecuteFileName) &&
                File.Exists(Path.Combine(directory, manifest.ExecuteFileName)))
            {
                validated.Add((directory, manifest));
            }
        }

        var selected = validated.FirstOrDefault(candidate =>
            string.Equals(candidate.Manifest.Id, catalogEntry.Id, StringComparison.OrdinalIgnoreCase));
        if (selected == default)
        {
            selected = validated.FirstOrDefault(candidate =>
                string.Equals(
                    NormalizeName(candidate.Manifest.Name),
                    NormalizeName(catalogEntry.Name),
                    StringComparison.OrdinalIgnoreCase));
        }
        if (selected == default && validated.Count == 1)
        {
            selected = validated[0];
        }

        return selected == default
            ? throw new InvalidDataException("The release does not contain one unambiguous, valid PowerToys Run plugin.")
            : selected.Directory;
    }

    private static string NormalizeName(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static async Task<PluginManifest> ReadManifestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<PluginManifest>(
            stream,
            JsonDefaults.Options,
            cancellationToken) ?? throw new InvalidDataException("plugin.json is invalid.");
        if (string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidDataException("plugin.json is missing the plugin ID or name.");
        }

        return manifest;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
