namespace PowerToysRun.PluginManager.Core;

public sealed class AppPaths
{
    public AppPaths(string? localApplicationData = null)
    {
        LocalApplicationData = localApplicationData ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    public string LocalApplicationData { get; }

    public string PluginDirectory => Path.Combine(
        LocalApplicationData,
        "Microsoft",
        "PowerToys",
        "PowerToys Run",
        "Plugins");

    public string DataDirectory => Path.Combine(LocalApplicationData, "PowerToysRunPluginManager");

    public string AppDirectory => Path.Combine(DataDirectory, "App");

    public string CacheDirectory => Path.Combine(DataDirectory, "Cache");

    public string StagingDirectory => Path.Combine(DataDirectory, "Staging");

    public string BackupDirectory => Path.Combine(DataDirectory, "Backups");

    public string TransactionDirectory => Path.Combine(DataDirectory, "Transactions");

    public void EnsureDataDirectories()
    {
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(StagingDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(TransactionDirectory);
    }
}
