using System.Data.Common;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public interface IWmcBrandReader
{
    Task<IReadOnlyList<WmcBrandRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed class WmcBrandReader(IWmcFirebirdReader reader) : IWmcBrandReader
{
    public Task<IReadOnlyList<WmcBrandRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            "SELECT m.CODMARCA, m.DESCRICAO, COUNT(p.CODPRODUTO) FROM MARCAS m LEFT JOIN PRODUTOS p ON p.CODMARCA = m.CODMARCA GROUP BY m.CODMARCA, m.DESCRICAO ORDER BY m.DESCRICAO",
            Map,
            cancellationToken: cancellationToken);

    private static WmcBrandRecord Map(DbDataReader row) => new(
        Convert.ToInt32(row.GetValue(0)),
        row.IsDBNull(1) ? "" : row.GetString(1).Trim(),
        Convert.ToInt32(row.GetValue(2)));
}
