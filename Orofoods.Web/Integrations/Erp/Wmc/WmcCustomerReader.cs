using System.Data.Common;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public interface IWmcCustomerReader
{
    Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads only the CLIENTES columns already confirmed with WMC (CODCLIENTE, NOME).</summary>
public sealed class WmcCustomerReader(IWmcFirebirdReader reader) : IWmcCustomerReader
{
    public Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            "SELECT CODCLIENTE, NOME FROM CLIENTES",
            Map,
            cancellationToken: cancellationToken);

    private static WmcCustomerRecord Map(DbDataReader row) =>
        new(row.GetValue(0).ToString()!.Trim(), row.IsDBNull(1) ? "" : row.GetString(1).Trim());
}
