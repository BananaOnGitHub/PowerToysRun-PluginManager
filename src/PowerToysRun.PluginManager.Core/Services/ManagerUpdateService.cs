using System.Runtime.InteropServices;
using PowerToysRun.PluginManager.Core.Models;

namespace PowerToysRun.PluginManager.Core.Services;

public sealed class ManagerUpdateService(
    HttpClient httpClient,
    GitHubReleaseClient releaseClient,
    AppPaths paths)
{
    private const string RepositoryUrl =
        "https://github.com/BananaOnGitHub/PowerToysRun-PluginManager";
    private const long MaximumInstallerBytes = 400L * 1024 * 1024;

    public async Task<ManagerUpdate?> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        var developmentBuild = currentVersion.Contains("-dev.", StringComparison.OrdinalIgnoreCase);
        var stable = await releaseClient.GetLatestAsync(RepositoryUrl, cancellationToken);
        GitHubRelease? release = LooseVersionComparer.IsNewer(stable.TagName, currentVersion)
            ? stable
            : null;

        if (developmentBuild)
        {
            var development = await releaseClient.GetLatestDevelopmentAsync(
                RepositoryUrl, cancellationToken);
            if (development is not null &&
                LooseVersionComparer.IsNewer(development.TagName, currentVersion) &&
                (release is null || LooseVersionComparer.IsNewer(development.TagName, release.TagName)))
            {
                release = development;
            }
        }

        if (release is null)
        {
            return null;
        }

        var asset = GitHubReleaseClient.SelectManagerInstallerAsset(
            release,
            RuntimeInformation.OSArchitecture);
        return new ManagerUpdate(release.TagName.TrimStart('v', 'V'), release.HtmlUrl, asset);
    }

    public async Task<string> DownloadAsync(
        ManagerUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (update.Asset.Size > MaximumInstallerBytes)
        {
            throw new InvalidDataException("The manager installer is larger than the allowed size.");
        }

        paths.EnsureDataDirectories();
        var destination = Path.Combine(paths.UpdateDirectory, Path.GetFileName(update.Asset.Name));
        var temporary = destination + ".download";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, update.Asset.DownloadUrl);
            request.Headers.UserAgent.ParseAdd("PowerToysRun-PluginManager/0.3");
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumInstallerBytes)
            {
                throw new InvalidDataException("The manager installer is larger than the allowed size.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            await CopyWithLimitAsync(source, output, cancellationToken);
            File.Move(temporary, destination, true);
            return destination;
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
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
            if (total > MaximumInstallerBytes)
            {
                throw new InvalidDataException("The manager installer exceeded the allowed size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}

public sealed record ManagerUpdate(
    string Version,
    string ReleaseUrl,
    GitHubReleaseAsset Asset);
