using System.Data;
using System.Data.Common;
using Orofoods.Web.Integrations.Erp.Wmc;

namespace Orofoods.Web.Tests.Services;

public class WmcCustomerPaymentConditionReaderTests
{
    [Fact]
    public async Task Reads_primary_condition_and_schedule_from_confirmed_firebird_join()
    {
        var observedAt = new DateTimeOffset(2026, 10, 20, 12, 30, 0, TimeSpan.Zero);
        var reader = new CapturingFirebirdReader(CreateConditionTable());
        var sut = new WmcCustomerPaymentConditionReader(reader, new FixedTimeProvider(observedAt));

        var result = await sut.GetForCustomerAsync(" 5264 ");

        Assert.NotNull(result);
        Assert.Equal("5264", result!.CustomerCode);
        Assert.Equal("3", result.CustomerConditionCode);
        Assert.Equal("3", result.ConditionCode);
        Assert.Equal("14 DIAS", result.Description);
        Assert.Equal(1, result.InstallmentCount);
        Assert.Equal(14, result.FirstInstallmentDays);
        Assert.Equal(0, result.InstallmentIntervalDays);
        Assert.Equal("A", result.Situation);
        Assert.Equal("WMC Firebird mirror", result.Source);
        Assert.Equal(observedAt, result.ReadAtUtc);
        Assert.Null(result.SynchronizationAtUtc);
        Assert.Contains("FROM CLIENTES c", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LEFT JOIN CPAGTO p ON p.CODCPAGTO = c.CODCPAGTO", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p.NROPARCELAS", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p.DIASPRIMPARC", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p.DIASPARCELAS", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p.SITUACAO", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("5264", reader.Parameters!["CODCLIENTE"]);
    }

    private static DataTable CreateConditionTable()
    {
        var table = new DataTable();
        table.Columns.Add("CODCLIENTE", typeof(string));
        table.Columns.Add("CLIENTES_CODCPAGTO", typeof(string));
        table.Columns.Add("CPAGTO_CODCPAGTO", typeof(string));
        table.Columns.Add("DESCRICAO", typeof(string));
        table.Columns.Add("NROPARCELAS", typeof(int));
        table.Columns.Add("DIASPRIMPARC", typeof(int));
        table.Columns.Add("DIASPARCELAS", typeof(int));
        table.Columns.Add("SITUACAO", typeof(string));
        table.Rows.Add("5264", "3", "3", "14 DIAS", 1, 14, 0, "A");
        return table;
    }

    private sealed class CapturingFirebirdReader(DataTable table) : IWmcFirebirdReader
    {
        public string Sql { get; private set; } = "";
        public IReadOnlyDictionary<string, object?>? Parameters { get; private set; }

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            Sql = sql;
            Parameters = parameters;
            using var rows = table.CreateDataReader();
            var result = new List<T>();
            while (rows.Read()) result.Add(map(rows));
            return Task.FromResult<IReadOnlyList<T>>(result);
        }

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
