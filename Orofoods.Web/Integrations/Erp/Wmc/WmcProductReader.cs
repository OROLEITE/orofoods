using System.Data.Common;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public interface IWmcProductReader
{
    Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads only the PRODUTOS columns already confirmed with WMC (CODPRODUTO, PRODUTO, ESTOQUEDISPONIVEL, ESTOQUEATUAL, SITUACAO, UN).</summary>
public sealed class WmcProductReader(IWmcFirebirdReader reader) : IWmcProductReader
{
    public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            "SELECT CODPRODUTO, PRODUTO, SITUACAO, UN, ESTOQUEDISPONIVEL, ESTOQUEATUAL FROM PRODUTOS",
            Map,
            cancellationToken: cancellationToken);

    private static WmcProductRecord Map(DbDataReader row) => new(
        row.GetValue(0).ToString()!.Trim(),
        row.IsDBNull(1) ? "" : row.GetString(1).Trim(),
        row.IsDBNull(2) ? null : row.GetValue(2).ToString()!.Trim(),
        row.IsDBNull(3) ? null : row.GetString(3).Trim(),
        row.IsDBNull(4) ? null : Convert.ToInt32(row.GetValue(4)),
        row.IsDBNull(5) ? null : Convert.ToInt32(row.GetValue(5)));
}
