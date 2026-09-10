using PowerToysRun.PluginManager.Core.Models;

namespace PowerToysRun.PluginManager.Core.Services;

public sealed class PluginChangeQueue
{
    private readonly Dictionary<string, PendingPluginChange> _changes =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _changes.Count;

    public IReadOnlyCollection<PendingPluginChange> Changes => _changes.Values;

    public void Set(PendingPluginChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _changes[Path.GetFullPath(change.TargetDirectory)] = change;
    }

    public bool Contains(string targetDirectory) =>
        _changes.ContainsKey(Path.GetFullPath(targetDirectory));

    public PendingPluginChange? Remove(string targetDirectory)
    {
        var key = Path.GetFullPath(targetDirectory);
        if (!_changes.Remove(key, out var change))
        {
            return null;
        }

        return change;
    }

    public void Clear() => _changes.Clear();

    public TransactionPlan CreatePlan(
        AppPaths paths,
        string? powerToysExecutablePath,
        bool restartPowerToys = true)
    {
        if (_changes.Count == 0)
        {
            throw new InvalidOperationException("No plugin changes are queued.");
        }

        var transactionId = Guid.NewGuid();
        return new TransactionPlan
        {
            Id = transactionId,
            PowerToysExecutablePath = powerToysExecutablePath,
            RestartPowerToys = restartPowerToys,
            Operations = _changes.Values.Select((change, index) => new PluginTransactionOperation
            {
                Kind = change.Kind,
                PluginId = change.PluginId,
                PluginName = change.PluginName,
                SourceDirectory = change.SourceDirectory,
                TargetDirectory = change.TargetDirectory,
                BackupDirectory = Path.Combine(
                    paths.BackupDirectory,
                    transactionId.ToString("N"),
                    $"{index:D2}-{SafeName(change.PluginName)}"),
            }).ToList(),
        };
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(result) ? "Plugin" : result;
    }
}
