using Orofoods.Web.Integrations.Erp.Wmc;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcCustomerReaderTests
{
    [Fact]
    public async Task Reader_filters_clients_by_seller_and_selects_only_confirmed_fields()
    {
        var reader = new CapturingReader();

        await new WmcCustomerReader(reader).GetBySellerAsync(" V1 ");

        Assert.Contains("CODCLIENTE, NOME, FANTASIA, CNPJ, CODVENDEDOR, SITUACAO, RESTRICAO", reader.Sql, StringComparison.Ordinal);
        Assert.Contains("WHERE CODVENDEDOR = @CODVENDEDOR", reader.Sql, StringComparison.Ordinal);
        Assert.Equal("V1", reader.Parameters!["CODVENDEDOR"]);
    }

    [Fact]
    public async Task Reader_lists_seller_codes_without_reading_customer_details()
    {
        var reader = new CapturingReader();

        await new WmcCustomerReader(reader).GetSellerCodesAsync();

        Assert.Contains("SELECT DISTINCT CODVENDEDOR", reader.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("NOME", reader.Sql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingReader : IWmcFirebirdReader
    {
        public string Sql { get; private set; } = "";
        public IReadOnlyDictionary<string, object?>? Parameters { get; private set; }

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<System.Data.Common.DbDataReader, T> map, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            Sql = sql;
            Parameters = parameters;
            return Task.FromResult<IReadOnlyList<T>>([]);
        }
    }
}
