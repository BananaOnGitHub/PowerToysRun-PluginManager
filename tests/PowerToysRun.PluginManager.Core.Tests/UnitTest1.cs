using System.Runtime.InteropServices;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Services;

namespace PowerToysRun.PluginManager.Core.Tests;

public sealed class CoreTests
{
    [Theory]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("1.0.0", "1.0.0-beta.1", true)]
    [InlineData("1.0.0-beta.2", "1.0.0", false)]
    [InlineData(null, "1.0.0", false)]
    public void LooseVersionComparer_HandlesTags(
        string? candidate,
        string? installed,
        bool expected)
    {
        Assert.Equal(expected, LooseVersionComparer.IsNewer(candidate, installed));
    }

    [Fact]
    public void GitHubRepository_NormalizesGitSuffixAndNestedUrls()
    {
        Assert.True(GitHubRepository.TryParse(
            "https://github.com/example/plugin.git/releases/latest",
            out var repository));
        Assert.Equal("example/plugin", repository.Slug);
    }

    [Fact]
    public void AssetSelection_ChoosesRequestedArchitecture()
    {
        var release = new GitHubRelease
        {
            TagName = "v1.0.0",
            Assets =
            [
                new GitHubReleaseAsset { Name = "Plugin-x64.zip" },
                new GitHubReleaseAsset { Name = "Plugin-arm64.zip" },
            ],
        };

        Assert.Equal(
            "Plugin-arm64.zip",
            GitHubReleaseClient.SelectAsset(release, Architecture.Arm64).Name);
    }

    [Fact]
    public async Task Scanner_ReportsValidAndBrokenPlugins()
    {
        using var temporary = new TemporaryDirectory();
        var valid = Directory.CreateDirectory(Path.Combine(temporary.Path, "Valid")).FullName;
        await File.WriteAllTextAsync(
            Path.Combine(valid, "plugin.json"),
            """
            {
              "ID": "VALID",
              "Name": "Valid",
              "Version": "1.0.0",
              "ExecuteFileName": "Valid.dll"
            }
            """);
        await File.WriteAllBytesAsync(Path.Combine(valid, "Valid.dll"), [0]);
        Directory.CreateDirectory(Path.Combine(temporary.Path, "Broken"));

        var plugins = await new InstalledPluginScanner().ScanAsync(temporary.Path);

        Assert.Equal(2, plugins.Count);
        Assert.Contains(plugins, plugin => plugin.Manifest?.Id == "VALID" && plugin.IsValid);
        Assert.Contains(plugins, plugin => plugin.LoadError == "plugin.json was not found.");
    }

    [Fact]
    public void StateBuilder_MatchesPluginsFromTheSameRepositoryByName()
    {
        var catalog = new CatalogDocument
        {
            Plugins =
            [
                new PluginCatalogEntry
                {
                    Id = "owner/repo:one",
                    Name = "One",
                    RepositoryUrl = "https://github.com/owner/repo",
                },
                new PluginCatalogEntry
                {
                    Id = "owner/repo:two",
                    Name = "Two",
                    RepositoryUrl = "https://github.com/owner/repo",
                },
            ],
        };
        var installed = new[]
        {
            new InstalledPlugin
            {
                DirectoryPath = "two",
                Manifest = new PluginManifest
                {
                    Id = "actual-id",
                    Name = "Two",
                    Website = "https://github.com/owner/repo",
                    ExecuteFileName = "Two.dll",
                },
            },
        };

        var states = PluginStateBuilder.Build(catalog, installed);

        Assert.False(states.Single(state => state.CatalogEntry.Name == "One").IsInstalled);
        Assert.True(states.Single(state => state.CatalogEntry.Name == "Two").IsInstalled);
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }
}
