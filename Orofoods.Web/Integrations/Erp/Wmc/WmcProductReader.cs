using System.Data.Common;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public interface IWmcProductReader
{
    Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default) =>
        GetAllAsync(cancellationToken);
}

/// <summary>Reads the confirmed product master and stock columns from the read-only WMC replica.</summary>
public sealed class WmcProductReader(IWmcFirebirdReader reader) : IWmcProductReader
{
    public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        GetAllByBrandAsync(null, cancellationToken);

    public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            brandCode is null
                ? "SELECT CODPRODUTO, PRODUTO, SITUACAO, UN, ESTOQUEDISPONIVEL, ESTOQUEATUAL, CODBARRASFAB, CODMARCA, UN_ALTERN, QTDE_EMB, QTDE_CONV, PRECOCUSTO, PRECOVENDA, ESTOQUERESERVADO, DATA_ALTERACAO FROM PRODUTOS"
                : "SELECT CODPRODUTO, PRODUTO, SITUACAO, UN, ESTOQUEDISPONIVEL, ESTOQUEATUAL, CODBARRASFAB, CODMARCA, UN_ALTERN, QTDE_EMB, QTDE_CONV, PRECOCUSTO, PRECOVENDA, ESTOQUERESERVADO, DATA_ALTERACAO FROM PRODUTOS WHERE CODMARCA = @CODMARCA",
            Map,
            brandCode is null ? null : new Dictionary<string, object?> { ["CODMARCA"] = brandCode.Value },
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandWithUnitsAsync(
        short? brandCode,
        CancellationToken cancellationToken = default)
    {
        var rows = await reader.QueryAsync(
            brandCode is null
                ? "SELECT p.CODPRODUTO, p.PRODUTO, p.SITUACAO, p.UN, p.ESTOQUEDISPONIVEL, p.ESTOQUEATUAL, p.CODBARRASFAB, p.CODMARCA, p.UN_ALTERN, p.QTDE_EMB, p.QTDE_CONV, p.PRECOCUSTO, p.PRECOVENDA, p.ESTOQUERESERVADO, p.DATA_ALTERACAO, m.DESCRICAO, pu.UN, u.DESCRICAO, pu.FATOR, pu.CODBARRAS, pu.UNMASTER, (SELECT COUNT(*) FROM PRODUTOS_UNCONV pu_count WHERE pu_count.CODPRODUTO = p.CODPRODUTO) AS TOTAL_UNITS FROM PRODUTOS p LEFT JOIN MARCAS m ON m.CODMARCA = p.CODMARCA LEFT JOIN PRODUTOS_UNCONV pu ON pu.CODPRODUTO = p.CODPRODUTO LEFT JOIN UNIDADES u ON u.CODUNIDADE = pu.UN ORDER BY p.CODPRODUTO, pu.UN"
                : "SELECT p.CODPRODUTO, p.PRODUTO, p.SITUACAO, p.UN, p.ESTOQUEDISPONIVEL, p.ESTOQUEATUAL, p.CODBARRASFAB, p.CODMARCA, p.UN_ALTERN, p.QTDE_EMB, p.QTDE_CONV, p.PRECOCUSTO, p.PRECOVENDA, p.ESTOQUERESERVADO, p.DATA_ALTERACAO, m.DESCRICAO, pu.UN, u.DESCRICAO, pu.FATOR, pu.CODBARRAS, pu.UNMASTER, (SELECT COUNT(*) FROM PRODUTOS_UNCONV pu_count WHERE pu_count.CODPRODUTO = p.CODPRODUTO) AS TOTAL_UNITS FROM PRODUTOS p LEFT JOIN MARCAS m ON m.CODMARCA = p.CODMARCA LEFT JOIN PRODUTOS_UNCONV pu ON pu.CODPRODUTO = p.CODPRODUTO LEFT JOIN UNIDADES u ON u.CODUNIDADE = pu.UN WHERE p.CODMARCA = @CODMARCA ORDER BY p.CODPRODUTO, pu.UN",
            MapProductUnit,
            brandCode is null ? null : new Dictionary<string, object?> { ["CODMARCA"] = brandCode.Value },
            cancellationToken);

        return rows
            .GroupBy(unit => NormalizeCode(unit.CodProduto), StringComparer.Ordinal)
            .Select(group =>
            {
                var productRows = group.ToArray();
                var expectedCount = productRows[0].ExpectedUnitCount;
                var units = productRows.Where(item => item.Unit is not null).Select(item => item.Unit!).ToArray();
                var complete = expectedCount >= 0
                    && productRows.All(item => item.ExpectedUnitCount == expectedCount)
                    && productRows.LongLength == Math.Max(1L, expectedCount)
                    && units.LongLength == expectedCount;
                return productRows[0].Product with
                {
                    Units = complete ? units : null,
                    UnitsSnapshotComplete = complete
                };
            })
            .ToArray();
    }

    private static WmcProductRecord Map(DbDataReader row) => new(
        row.GetValue(0).ToString()!.Trim(),
        row.IsDBNull(1) ? "" : row.GetString(1).Trim(),
        row.IsDBNull(2) ? null : row.GetValue(2).ToString()!.Trim(),
        row.IsDBNull(3) ? null : row.GetString(3).Trim(),
        row.IsDBNull(4) ? null : Convert.ToDecimal(row.GetValue(4)),
        row.IsDBNull(5) ? null : Convert.ToDecimal(row.GetValue(5)),
        Text(row, 6),
        Text(row, 7),
        Text(row, 8),
        Decimal(row, 9),
        Decimal(row, 10),
        Decimal(row, 11),
        Decimal(row, 12),
        Decimal(row, 13),
        row.IsDBNull(14) ? null : Convert.ToDateTime(row.GetValue(14)),
        Text(row, 15));

    private static ProductUnitQueryRow MapProductUnit(DbDataReader row) =>
        new(Map(row), row.IsDBNull(16) ? null : new WmcProductUnitRecord(
            Text(row, 16) ?? "",
            Text(row, 17) ?? "",
            Decimal(row, 18),
            Text(row, 19),
            IsTrue(row, 20)),
            Convert.ToInt64(row.GetValue(21)));

    private sealed record ProductUnitQueryRow(WmcProductRecord Product, WmcProductUnitRecord? Unit, long ExpectedUnitCount)
    {
        public string CodProduto => Product.CodProduto;
    }

    private static string? Text(DbDataReader row, int index) => row.IsDBNull(index) ? null : row.GetValue(index).ToString()?.Trim();
    private static decimal? Decimal(DbDataReader row, int index) => row.IsDBNull(index) ? null : Convert.ToDecimal(row.GetValue(index));
    private static bool IsTrue(DbDataReader row, int index)
    {
        if (row.IsDBNull(index)) return false;
        var value = row.GetValue(index);
        return value is bool flag
            ? flag
            : string.Equals(value.ToString()?.Trim(), "T", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.ToString()?.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.ToString()?.Trim(), "1", StringComparison.Ordinal);
    }

    private static string NormalizeCode(string value) => value.Trim();
}
