namespace PowerToysRun.PluginManager.Core.Models;

public sealed class TransactionPlan
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? PowerToysExecutablePath { get; init; }

    public bool RestartPowerToys { get; init; } = true;

    public List<PluginTransactionOperation> Operations { get; init; } = [];
}

public sealed class PluginTransactionOperation
{
    public PluginTransactionKind Kind { get; init; }

    public string PluginId { get; init; } = string.Empty;

    public string PluginName { get; init; } = string.Empty;

    public string? SourceDirectory { get; init; }

    public string TargetDirectory { get; init; } = string.Empty;

    public string BackupDirectory { get; init; } = string.Empty;
}

public enum PluginTransactionKind
{
    Install,
    Update,
    Uninstall,
}

public sealed class StagedPluginPackage
{
    public required PluginManifest Manifest { get; init; }

    public required GitHubRelease Release { get; init; }

    public required GitHubReleaseAsset Asset { get; init; }

    public required string PluginDirectory { get; init; }

    public required string Sha256 { get; init; }
}
