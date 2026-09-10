namespace PowerToysRun.PluginManager.Core.Models;

public sealed class PendingPluginChange
{
    public required PluginTransactionKind Kind { get; init; }

    public required string PluginId { get; init; }

    public required string PluginName { get; init; }

    public string? SourceDirectory { get; init; }

    public required string TargetDirectory { get; init; }
}
