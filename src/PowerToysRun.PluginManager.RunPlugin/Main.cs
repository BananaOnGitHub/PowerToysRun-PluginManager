using System.Diagnostics;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.PluginManager;

public sealed class Main : IPlugin
{
    public string Name => "Plugin Manager";

    public string Description => "Browse, install, update, and remove PowerToys Run plugins.";

    public void Init(PluginInitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }

    public List<Result> Query(Query query)
    {
        var search = query.Search?.Trim() ?? string.Empty;
        return
        [
            new Result
            {
                Title = string.IsNullOrWhiteSpace(search)
                    ? "Open PowerToys Run Plugin Manager"
                    : $"Search plugins for “{search}”",
                SubTitle = "Browse descriptions, authors, versions, and repository metadata",
                IcoPath = @"Images\plugin-manager.png",
                Action = _ =>
                {
                    LaunchManager(search);
                    return true;
                },
            },
        ];
    }

    private static void LaunchManager(string search)
    {
        var managerPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerToysRunPluginManager",
            "App",
            "PowerToysRun.PluginManager.exe");
        if (!File.Exists(managerPath))
        {
            Process.Start(new ProcessStartInfo(
                "https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases")
            {
                UseShellExecute = true,
            });
            return;
        }

        var startInfo = new ProcessStartInfo(managerPath)
        {
            UseShellExecute = true,
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            startInfo.ArgumentList.Add("--search");
            startInfo.ArgumentList.Add(search);
        }

        Process.Start(startInfo);
    }
}
