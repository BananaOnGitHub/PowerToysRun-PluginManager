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
    [InlineData("v0.4.0-dev.12", "0.4.0-dev.9+abcdef", true)]
    [InlineData("v0.4.0-dev.9", "0.4.0-dev.12", false)]
    [InlineData("v0.4.0", "0.4.0-dev.12", true)]
    [InlineData("v0.4.0-dev.13", "0.4.0", false)]
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
    public void ManagerInstallerSelection_ChoosesSetupForRequestedArchitecture()
    {
        var release = new GitHubRelease
        {
            TagName = "v0.3.0",
            Assets =
            [
                new GitHubReleaseAsset { Name = "PowerToysRun-PluginManager-Setup-win-x64.exe" },
                new GitHubReleaseAsset { Name = "PowerToysRun-PluginManager-Setup-win-arm64.exe" },
                new GitHubReleaseAsset { Name = "PowerToysRun-PluginManager-win-x64.zip" },
            ],
        };

        Assert.Equal(
            "PowerToysRun-PluginManager-Setup-win-arm64.exe",
            GitHubReleaseClient.SelectManagerInstallerAsset(release, Architecture.Arm64).Name);
    }

    [Fact]
    public async Task ManagerUpdate_OnlyOffersDevelopmentBuildsToDevelopmentInstallations()
    {
        using var client = new HttpClient(new StubReleaseHandler());
        var updateService = new ManagerUpdateService(
            client, new GitHubReleaseClient(client), new PowerToysRun.PluginManager.Core.AppPaths());

        var stableUpdate = await updateService.CheckAsync("0.3.0");
        var developmentUpdate = await updateService.CheckAsync("0.4.0-dev.9+abcdef");

        Assert.Null(stableUpdate);
        Assert.Equal("0.4.0-dev.12", developmentUpdate?.Version);
        Assert.EndsWith("Setup-win-x64.exe", developmentUpdate?.Asset.Name);
    }

    [Fact]
    public async Task ChannelSwitch_OffersTargetReleaseEvenWhenItsVersionIsLower()
    {
        using var client = new HttpClient(new StubReleaseHandler());
        var updateService = new ManagerUpdateService(
            client, new GitHubReleaseClient(client), new PowerToysRun.PluginManager.Core.AppPaths());

        var stable = await updateService.GetChannelReleaseAsync(development: false);
        var development = await updateService.GetChannelReleaseAsync(development: true);

        Assert.Equal("0.3.0", stable?.Version);
        Assert.Equal("0.4.0-dev.12", development?.Version);
        Assert.EndsWith("Setup-win-x64.exe", development?.Asset.Name);
    }

    [Fact]
    public void ChangeQueue_ReplacesSameTargetAndBuildsOneTransaction()
    {
        using var temporary = new TemporaryDirectory();
        var paths = new PowerToysRun.PluginManager.Core.AppPaths(temporary.Path);
        var target = Path.Combine(paths.PluginDirectory, "Example");
        var queue = new PluginChangeQueue();
        queue.Set(new PendingPluginChange
        {
            Kind = PluginTransactionKind.Install,
            PluginId = "example",
            PluginName = "Example",
            SourceDirectory = Path.Combine(paths.StagingDirectory, "one", "payload"),
            TargetDirectory = target,
        });
        queue.Set(new PendingPluginChange
        {
            Kind = PluginTransactionKind.Uninstall,
            PluginId = "example",
            PluginName = "Example",
            TargetDirectory = target,
        });

        var plan = queue.CreatePlan(paths, "PowerToys.exe");

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(PluginTransactionKind.Uninstall, operation.Kind);
        Assert.Equal("PowerToys.exe", plan.PowerToysExecutablePath);
        Assert.StartsWith(
            paths.BackupDirectory,
            operation.BackupDirectory,
            StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void ReadmeParser_FindsPluginLogoScreenshotsAndOverview()
    {
        const string readme = """
            # Example plugin

            <img src="assets/example.logo.png" alt="Example logo">
            <img src="https://img.shields.io/badge/build-passing-green" alt="Build">

            ## Overview

            **Example** keeps useful tools close without leaving your keyboard.

            - Metadata that should not become prose.

            ## Demo

            ![Example in action](assets/demo-main.png)
            ![Second screenshot](https://github.com/owner/repo/blob/main/assets/screenshot-two.jpg)
            """;

        var metadata = RepositoryReadmeParser.Parse(
            readme,
            new GitHubRepository("owner", "repo"),
            "Example");

        Assert.Equal(
            "https://raw.githubusercontent.com/owner/repo/HEAD/assets/example.logo.png",
            metadata.IconUrl);
        Assert.Equal(
            "Example keeps useful tools close without leaving your keyboard.",
            metadata.LongDescription);
        Assert.Equal(2, metadata.ScreenshotUrls.Count);
        Assert.Contains(
            "https://raw.githubusercontent.com/owner/repo/HEAD/assets/demo-main.png",
            metadata.ScreenshotUrls);
        Assert.Contains(
            "https://raw.githubusercontent.com/owner/repo/main/assets/screenshot-two.jpg",
            metadata.ScreenshotUrls);
    }

    [Fact]
    public void ReadmeParser_IgnoresBadgesAndUnsafeOrUnsupportedImages()
    {
        const string readme = """
            # Example plugin

            ![Status](https://img.shields.io/badge/build-passing-green.png)
            ![Vector](assets/icon.svg)
            ![Embedded](data:image/png;base64,AAAA)
            ![Traversal](../secret.png)
            """;

        var metadata = RepositoryReadmeParser.Parse(
            readme,
            new GitHubRepository("owner", "repo"),
            "Example");

        Assert.Null(metadata.IconUrl);
        Assert.Empty(metadata.ScreenshotUrls);
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/owner/repo/main/icon.png", true)]
    [InlineData("https://user-images.githubusercontent.com/123/demo.gif", true)]
    [InlineData("http://raw.githubusercontent.com/owner/repo/main/icon.png", false)]
    [InlineData("https://example.com/icon.png", false)]
    [InlineData("file:///C:/Windows/System32/icon.png", false)]
    [InlineData("https://raw.githubusercontent.com/owner/repo/main/icon.svg", false)]
    public void CatalogMediaUrlPolicy_AllowsOnlyTrustedRasterImages(string url, bool expected)
    {
        Assert.Equal(expected, CatalogMediaUrlPolicy.IsAllowed(url));
    }

    [Theory]
    [InlineData("Images/icon.png", true)]
    [InlineData("Images\\icon.png", true)]
    [InlineData("../icon.png", false)]
    [InlineData("Images\\..\\icon.png", false)]
    [InlineData("C:\\plugin\\icon.png", false)]
    [InlineData("\\\\server\\share\\icon.png", false)]
    public void InstalledPluginPathPolicy_RejectsRootedAndTraversingPaths(string path, bool expected)
    {
        Assert.Equal(expected, InstalledPluginScanner.IsSafeRelativePath(path));
    }

    [Fact]
    public void ReadmeParser_PrefersPowerToysRunMediaInMultiProjectRepository()
    {
        const string readme = """
            # Example

            ![All features](Example-all.png)
            ![MCP demo](Example.McpServer-demo.png)
            ![CLI](Example.DotnetTool.gif)
            ![PowerToys Run](Example.PowerToysRun.gif)
            """;

        var metadata = RepositoryReadmeParser.Parse(
            readme,
            new GitHubRepository("owner", "repo"),
            "Example");

        Assert.Null(metadata.IconUrl);
        Assert.Equal(
            ["https://raw.githubusercontent.com/owner/repo/HEAD/Example.PowerToysRun.gif"],
            metadata.ScreenshotUrls);
    }

    [Fact]
    public void TreeMediaSelector_FindsManifestStyleIconAndScreenshots()
    {
        var metadata = RepositoryTreeMediaSelector.Select(
            [
                "src/Images/process-killer.dark.png",
                "src/Images/process-killer.light.png",
                "assets/screenshots/demo-main.png",
                "assets/screenshots/settings.png",
            ],
            new GitHubRepository("owner", "PowerToysRun-ProcessKiller"),
            "Process Killer");

        Assert.Equal(
            "https://raw.githubusercontent.com/owner/PowerToysRun-ProcessKiller/HEAD/src/Images/process-killer.dark.png",
            metadata.IconUrl);
        Assert.Equal(
            ["https://raw.githubusercontent.com/owner/PowerToysRun-ProcessKiller/HEAD/assets/screenshots/demo-main.png"],
            metadata.ScreenshotUrls);
    }

    [Fact]
    public void TreeMediaSelector_PrefersPowerToysRunIconOverSiblingApps()
    {
        var metadata = RepositoryTreeMediaSelector.Select(
            [
                "src/Example.CmdPal/Assets/icon.png",
                "src/Example.PowerToysRun/Images/example.dark.png",
                "Standalone App/Assets/StoreLogo.png",
            ],
            new GitHubRepository("owner", "Example"),
            "Example");

        Assert.Equal(
            "https://raw.githubusercontent.com/owner/Example/HEAD/src/Example.PowerToysRun/Images/example.dark.png",
            metadata.IconUrl);
    }
}

internal sealed class StubReleaseHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var json = request.RequestUri?.AbsolutePath.EndsWith("/latest", StringComparison.Ordinal) == true
            ? """
              {"tag_name":"v0.3.0","html_url":"https://github.com/owner/repo/releases/tag/v0.3.0","prerelease":false,"assets":[{"name":"PowerToysRun-PluginManager-Setup-win-x64.exe"}]}
              """
            : """
              [
                {"tag_name":"v0.4.0-dev.9","prerelease":true,"assets":[]},
                {"tag_name":"v0.4.0-dev.12","html_url":"https://github.com/owner/repo/releases/tag/v0.4.0-dev.12","prerelease":true,"assets":[{"name":"PowerToysRun-PluginManager-Setup-win-x64.exe"}]}
              ]
              """;
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        });
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
