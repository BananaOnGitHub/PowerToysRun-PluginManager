using System.Text.Json;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Serialization;

namespace PowerToysRun.PluginManager.Core.Services;

public sealed class TransactionPlanStore(AppPaths paths)
{
    public async Task<string> WriteAsync(
        TransactionPlan plan,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureDataDirectories();
        var path = Path.Combine(paths.TransactionDirectory, $"{plan.Id:N}.json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(plan, JsonDefaults.Options),
            cancellationToken);
        return path;
    }

    public static async Task<TransactionPlan> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<TransactionPlan>(
            stream,
            JsonDefaults.Options,
            cancellationToken) ?? throw new InvalidDataException("The transaction plan is invalid.");
    }
}
