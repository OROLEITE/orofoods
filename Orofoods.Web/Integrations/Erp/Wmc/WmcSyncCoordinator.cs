namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed record WmcSyncEntityResult(int RecordsRead, int RecordsCreated, int RecordsUpdated, int RecordsSkipped, string? ErrorMessage)
{
    public static WmcSyncEntityResult Empty { get; } = new(0, 0, 0, 0, null);
}

public sealed record WmcSyncRunResult(
    DateTime StartedAt,
    DateTime FinishedAt,
    WmcSyncEntityResult Customers,
    WmcSyncEntityResult Products,
    WmcSyncEntityResult Sellers,
    WmcSyncEntityResult Stock,
    string Status = "Succeeded");

/// <summary>
/// Tracks whether a sync is currently running and the outcome of the last one, in memory only. Once
/// IntegrationSyncLog is authorized and migrated this becomes a thin read-through cache over that table.
/// </summary>
public sealed class WmcSyncCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private WmcSyncRunResult? lastRun;

    public bool IsRunning { get; private set; }
    public WmcSyncRunResult? LastRun => lastRun;

    public async Task<WmcSyncRunResult?> RunExclusivelyAsync(Func<Task<WmcSyncRunResult>> run, CancellationToken cancellationToken = default)
    {
        var result = await RunExclusivelyAsync<WmcSyncRunResult>(run, cancellationToken);
        lastRun = result;
        return result;
    }

    public async Task<T?> RunExclusivelyAsync<T>(Func<Task<T>> run, CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return default; // Another sync is already in progress.
        }

        IsRunning = true;
        try
        {
            return await run();
        }
        finally
        {
            IsRunning = false;
            gate.Release();
        }
    }
}
