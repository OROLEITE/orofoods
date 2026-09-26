namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed record WmcSellerRecord(string CodVendedor, string Nome);

public interface IWmcSellerReader
{
    Task<IReadOnlyList<WmcSellerRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Placeholder until the real WMC seller table/columns are confirmed (orders only expose CODVENDEDOR as a
/// foreign key; the table itself was never confirmed). Deliberately does not guess a table name - returns
/// no rows so sync stays a safe no-op instead of guessing and silently reading/writing wrong data.
/// </summary>
public sealed class UndiscoveredWmcSellerReader(ILogger<UndiscoveredWmcSellerReader> logger) : IWmcSellerReader
{
    public Task<IReadOnlyList<WmcSellerRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Seller sync skipped: the WMC seller table/columns have not been confirmed yet.");
        return Task.FromResult<IReadOnlyList<WmcSellerRecord>>([]);
    }
}
