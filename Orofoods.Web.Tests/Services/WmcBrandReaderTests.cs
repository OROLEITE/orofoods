using System.Data;
using System.Reflection;
using Microsoft.Data.Sqlite;
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
        Assert.Contains("JOIN (", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY p.CODMARCA", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(p.CODPRODUTO)", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SUM(CASE WHEN p.SITUACAO = 'A'", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ACTIVE_PRODUCT_COUNT", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE stats.ACTIVE_PRODUCT_COUNT > 0", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LEFT JOIN PRODUTOS", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT COUNT(*) FROM PRODUTOS", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Visconti", brands.Single().Description);
        Assert.Equal(5, brands.Single().Code);
        Assert.Equal(36, brands.Single().ProductCount);
        Assert.Equal(8, brands.Single().ActiveProductCount);
    }

    [Fact]
    public async Task Brand_reader_keeps_total_and_active_counts_in_the_derived_aggregate()
    {
        var reader = new CapturingReader();

        var brands = await new WmcBrandReader(reader).GetAllAsync();

        var brand = Assert.Single(brands);
        Assert.Equal(36, brand.ProductCount);
        Assert.Equal(8, brand.ActiveProductCount);
        Assert.Contains("TOTAL_PRODUCT_COUNT", reader.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ACTIVE_PRODUCT_COUNT", reader.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Firebird_reader_only_classifies_the_brand_query_for_timing_logs()
    {
        var classifier = typeof(WmcFirebirdReader).GetMethod(
            "GetQueryKind",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(classifier);
        Assert.Equal(
            "BrandList",
            classifier!.Invoke(null, ["SELECT m.CODMARCA, stats.ACTIVE_PRODUCT_COUNT FROM MARCAS m JOIN (SELECT p.CODMARCA FROM PRODUTOS p) stats ON stats.CODMARCA = m.CODMARCA"]));
        Assert.Null(classifier.Invoke(null, ["SELECT CODCLIENTE, NOME FROM CLIENTES"]));
    }

    [Fact]
    public async Task Brand_reader_query_preserves_counts_per_brand()
    {
        var reader = new SqlExecutingReader();

        var brands = await new WmcBrandReader(reader).GetAllAsync();

        Assert.Equal(
            [
                new WmcBrandRecord(1, "Alfa", 2, 2),
                new WmcBrandRecord(2, "Zeta", 3, 1)
            ],
            brands);
    }

    private sealed class CapturingReader : IWmcFirebirdReader
    {
        public string Sql { get; private set; } = "";

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<System.Data.Common.DbDataReader, T> map, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            Sql = sql;
            if (typeof(T) != typeof(WmcBrandRecord))
            {
                return Task.FromResult<IReadOnlyList<T>>([]);
            }

            var table = new DataTable();
            table.Columns.Add("CODMARCA", typeof(int));
            table.Columns.Add("DESCRICAO", typeof(string));
            table.Columns.Add("TOTAL_PRODUCT_COUNT", typeof(int));
            table.Columns.Add("ACTIVE_PRODUCT_COUNT", typeof(int));
            table.Rows.Add(5, "Visconti", 36, 8);

            using var dataReader = table.CreateDataReader();
            Assert.True(dataReader.Read());
            return Task.FromResult<IReadOnlyList<T>>([map(dataReader)]);
        }
    }

    private sealed class SqlExecutingReader : IWmcFirebirdReader
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<System.Data.Common.DbDataReader, T> map, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var setup = connection.CreateCommand();
            setup.CommandText = """
                CREATE TABLE MARCAS (CODMARCA INTEGER NOT NULL, DESCRICAO TEXT NULL);
                CREATE TABLE PRODUTOS (CODPRODUTO INTEGER NOT NULL, CODMARCA INTEGER NULL, SITUACAO TEXT NULL);
                INSERT INTO MARCAS VALUES (1, 'Alfa'), (2, 'Zeta'), (3, 'SemProduto'), (4, 'Inativa');
                INSERT INTO PRODUTOS VALUES
                    (101, 1, 'A'), (102, 1, 'A'),
                    (201, 2, 'A'), (202, 2, 'I'), (203, 2, 'I'),
                    (301, 4, 'I'), (302, 4, 'I');
                """;
            setup.ExecuteNonQuery();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var dataReader = command.ExecuteReader();
            var results = new List<T>();
            while (dataReader.Read())
            {
                results.Add(map(dataReader));
            }

            return Task.FromResult<IReadOnlyList<T>>(results);
        }
    }
}
