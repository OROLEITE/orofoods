using System.Data.Common;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public interface IWmcCustomerReader
{
    Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WmcCustomerRecord>> GetBySellerAsync(string sellerCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSellerCodesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads only the CLIENTES columns already confirmed with WMC (CODCLIENTE, NOME).</summary>
public sealed class WmcCustomerReader(IWmcFirebirdReader reader) : IWmcCustomerReader
{
    public Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            "SELECT CODCLIENTE, NOME, FANTASIA, CNPJ, CODVENDEDOR, SITUACAO, RESTRICAO FROM CLIENTES",
            Map,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<WmcCustomerRecord>> GetBySellerAsync(string sellerCode, CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            "SELECT CODCLIENTE, NOME, FANTASIA, CNPJ, CODVENDEDOR, SITUACAO, RESTRICAO FROM CLIENTES WHERE CODVENDEDOR = @CODVENDEDOR",
            Map,
            new Dictionary<string, object?> { ["CODVENDEDOR"] = sellerCode.Trim() },
            cancellationToken);

    public Task<IReadOnlyList<string>> GetSellerCodesAsync(CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            "SELECT DISTINCT CODVENDEDOR FROM CLIENTES WHERE CODVENDEDOR IS NOT NULL ORDER BY CODVENDEDOR",
            row => row.IsDBNull(0) ? "" : row.GetValue(0).ToString()!.Trim(),
            cancellationToken: cancellationToken);

    private static WmcCustomerRecord Map(DbDataReader row) =>
        new(
            Text(row, 0),
            Text(row, 1),
            Text(row, 2),
            Text(row, 3),
            Text(row, 4),
            Text(row, 5),
            Text(row, 6));

    private static string Text(DbDataReader row, int index) => row.IsDBNull(index) ? "" : row.GetValue(index).ToString()!.Trim();
}
