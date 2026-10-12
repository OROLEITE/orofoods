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
        row.IsDBNull(14) ? null : Convert.ToDateTime(row.GetValue(14)));

    private static string? Text(DbDataReader row, int index) => row.IsDBNull(index) ? null : row.GetValue(index).ToString()?.Trim();
    private static decimal? Decimal(DbDataReader row, int index) => row.IsDBNull(index) ? null : Convert.ToDecimal(row.GetValue(index));
}
