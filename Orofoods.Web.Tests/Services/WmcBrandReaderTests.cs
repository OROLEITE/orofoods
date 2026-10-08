using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcBrandReaderTests
{
    [Fact]
    public async Task Brand_reader_uses_confirmed_marcas_join_and_returns_counts()
    {
        var reader = new CapturingReader();
        var brands = await new WmcBrandReader(reader).GetAllAsync();

        Assert.Contains("MARCAS", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRODUTOS", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SITUACAO", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Visconti", brands.Single().Description);
        Assert.Equal(5, brands.Single().Code);
        Assert.Equal(36, brands.Single().ProductCount);
        Assert.Equal(8, brands.Single().ActiveProductCount);
    }

    private sealed class CapturingReader : IWmcFirebirdReader
    {
        public string Sql { get; private set; } = "";

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<System.Data.Common.DbDataReader, T> map, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            Sql = sql;
            object result = typeof(T) == typeof(WmcBrandRecord)
                ? new List<WmcBrandRecord> { new(5, "Visconti", 36, 8) }
                : new List<T>();
            return Task.FromResult((IReadOnlyList<T>)result);
        }
    }
}
