using System.Data.Common;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public interface IWmcCustomerPaymentConditionReader
{
    Task<WmcCustomerPaymentCondition?> GetForCustomerAsync(string customerCode, CancellationToken cancellationToken = default);
}

/// <summary>Reads the customer's single primary payment condition from the read-only WMC Firebird mirror.</summary>
public sealed class WmcCustomerPaymentConditionReader(IWmcFirebirdReader reader, TimeProvider timeProvider)
    : IWmcCustomerPaymentConditionReader
{
    public async Task<WmcCustomerPaymentCondition?> GetForCustomerAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        var rows = await reader.QueryAsync(
            """
            SELECT c.CODCLIENTE, c.CODCPAGTO, p.CODCPAGTO, p.DESCRICAO,
                   p.NROPARCELAS, p.DIASPRIMPARC, p.DIASPARCELAS, p.SITUACAO
            FROM CLIENTES c
            LEFT JOIN CPAGTO p ON p.CODCPAGTO = c.CODCPAGTO
            WHERE c.CODCLIENTE = @CODCLIENTE
            """,
            row => Map(row),
            new Dictionary<string, object?> { ["CODCLIENTE"] = customerCode.Trim() },
            cancellationToken);

        // Query time records when Orofoods read the mirror; it is not evidence of mirror freshness.
        var readAtUtc = timeProvider.GetUtcNow();
        return rows.SingleOrDefault() is { } condition
            ? condition with { ReadAtUtc = readAtUtc }
            : null;
    }

    private static WmcCustomerPaymentCondition Map(DbDataReader row) => new(
        Text(row, 0)!,
        Text(row, 1),
        Text(row, 2),
        Text(row, 3),
        Integer(row, 4),
        Integer(row, 5),
        Integer(row, 6),
        Text(row, 7),
        "WMC Firebird mirror",
        DateTimeOffset.MinValue);

    private static string? Text(DbDataReader row, int index) => row.IsDBNull(index) ? null : row.GetValue(index).ToString()?.Trim();
    private static int? Integer(DbDataReader row, int index) => row.IsDBNull(index) ? null : Convert.ToInt32(row.GetValue(index));
}

public sealed record WmcCustomerPaymentCondition(
    string CustomerCode,
    string? CustomerConditionCode,
    string? ConditionCode,
    string? Description,
    int? InstallmentCount,
    int? FirstInstallmentDays,
    int? InstallmentIntervalDays,
    string? Situation,
    string Source,
    DateTimeOffset ReadAtUtc,
    DateTimeOffset? SynchronizationAtUtc = null);
