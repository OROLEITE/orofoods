using System.Data.Common;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public interface IWmcBrandReader
{
    Task<IReadOnlyList<WmcBrandRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed class WmcBrandReader(IWmcFirebirdReader reader) : IWmcBrandReader
{
    internal const string BrandListSql = "SELECT m.CODMARCA, m.DESCRICAO, stats.TOTAL_PRODUCT_COUNT, stats.ACTIVE_PRODUCT_COUNT FROM MARCAS m JOIN (SELECT p.CODMARCA, COUNT(p.CODPRODUTO) AS TOTAL_PRODUCT_COUNT, SUM(CASE WHEN p.SITUACAO = 'A' THEN 1 ELSE 0 END) AS ACTIVE_PRODUCT_COUNT FROM PRODUTOS p GROUP BY p.CODMARCA) stats ON stats.CODMARCA = m.CODMARCA WHERE stats.ACTIVE_PRODUCT_COUNT > 0 ORDER BY m.DESCRICAO";

    public Task<IReadOnlyList<WmcBrandRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            BrandListSql,
            Map,
            cancellationToken: cancellationToken);

    private static WmcBrandRecord Map(DbDataReader row) => new(
        Convert.ToInt32(row.GetValue(0)),
        row.IsDBNull(1) ? "" : row.GetString(1).Trim(),
        Convert.ToInt32(row.GetValue(2)),
        row.IsDBNull(3) ? 0 : Convert.ToInt32(row.GetValue(3)));
}
