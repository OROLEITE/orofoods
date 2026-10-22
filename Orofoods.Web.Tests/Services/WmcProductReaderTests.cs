using System.Data;
using System.Data.Common;
using Orofoods.Web.Integrations.Erp.Wmc;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcProductReaderTests
{
    [Fact]
    public async Task Reads_official_units_and_attaches_them_to_the_selected_brand_product()
    {
        var firebird = new FakeFirebirdReader();

        var products = await new WmcProductReader(firebird).GetAllByBrandWithUnitsAsync(5);

        var product = Assert.Single(products);
        var units = Assert.IsAssignableFrom<IReadOnlyList<WmcProductUnitRecord>>(product.Units);
        Assert.True(product.UnitsSnapshotComplete);
        Assert.Equal("503813", product.CodProduto);
        Assert.Equal(["PC", "CX"], units.Select(unit => unit.UnitCode));
        Assert.Equal("PACOTE", units[0].Description);
        Assert.Equal(1m, units[0].Factor);
        Assert.Equal("7899679701348", units[0].Barcode);
        Assert.False(units[0].IsMaster);
        Assert.Equal("CAIXA", units[1].Description);
        Assert.Equal(18m, units[1].Factor);
        Assert.Equal("17899679701345", units[1].Barcode);
        Assert.True(units[1].IsMaster);
        var query = Assert.Single(firebird.Calls);
        Assert.Contains("PRODUTOS_UNCONV", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LEFT JOIN PRODUTOS_UNCONV", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SELECT COUNT(*) FROM PRODUTOS_UNCONV", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNIDADES", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((short)5, query.Parameters!["CODMARCA"]);
    }

    [Theory]
    [InlineData("503833", 1)]
    [InlineData("503838", 1)]
    public async Task Reads_confirmed_BIMBO_case_factors_without_substituting_product_packaging_quantities(string code, decimal expectedCaseFactor)
    {
        var firebird = new FakeFirebirdReader(productCode: code);

        var product = Assert.Single(await new WmcProductReader(firebird).GetAllByBrandWithUnitsAsync(5));
        var units = Assert.IsAssignableFrom<IReadOnlyList<WmcProductUnitRecord>>(product.Units);

        Assert.True(product.UnitsSnapshotComplete);
        Assert.Equal(expectedCaseFactor, Assert.Single(units, unit => unit.UnitCode == "CX").Factor);
        Assert.Equal(12m, product.QtdeConversao);
        Assert.NotEqual(product.QtdeConversao, Assert.Single(units, unit => unit.UnitCode == "CX").Factor);
        Assert.Single(firebird.Calls);
        Assert.Contains("PRODUTOS_UNCONV", firebird.Calls[0].Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirms_an_empty_unit_list_only_when_the_product_is_present_in_the_completed_snapshot()
    {
        var firebird = new FakeFirebirdReader(productHasUnits: false);

        var products = await new WmcProductReader(firebird).GetAllByBrandWithUnitsAsync(5);

        var product = Assert.Single(products);
        Assert.True(product.UnitsSnapshotComplete);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<WmcProductUnitRecord>>(product.Units));
    }

    [Fact]
    public async Task Returns_no_products_when_a_product_is_absent_from_the_single_product_and_unit_snapshot()
    {
        var firebird = new FakeFirebirdReader(includeProductInUnitSnapshot: false);

        var products = await new WmcProductReader(firebird).GetAllByBrandWithUnitsAsync(5);

        Assert.Empty(products);
        Assert.Single(firebird.Calls);
    }

    [Fact]
    public async Task Does_not_confirm_a_truncated_unit_list_when_the_source_count_is_higher()
    {
        var firebird = new FakeFirebirdReader(truncateUnitResults: true);

        var products = await new WmcProductReader(firebird).GetAllByBrandWithUnitsAsync(5);

        var product = Assert.Single(products);
        Assert.False(product.UnitsSnapshotComplete);
        Assert.Null(product.Units);
    }

    private sealed class FakeFirebirdReader(
        bool includeProductInUnitSnapshot = true,
        bool productHasUnits = true,
        bool truncateUnitResults = false,
        string productCode = "503813") : IWmcFirebirdReader
    {
        public List<QueryCall> Calls { get; } = [];

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IReadOnlyList<T>> QueryAsync<T>(
            string sql,
            Func<DbDataReader, T> map,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new QueryCall(sql, parameters));
            using var rows = (sql.Contains("PRODUTOS_UNCONV", StringComparison.OrdinalIgnoreCase)
                ? JoinedProductAndUnitRows(productCode, includeProductInUnitSnapshot, productHasUnits, truncateUnitResults)
                : ProductRows(productCode)).CreateDataReader();
            var results = new List<T>();
            while (rows.Read())
            {
                results.Add(map(rows));
            }

            return Task.FromResult<IReadOnlyList<T>>(results);
        }

        private static DataTable ProductRows(string productCode)
        {
            var table = new DataTable();
            table.Columns.Add("CODPRODUTO", typeof(string));
            table.Columns.Add("PRODUTO", typeof(string));
            table.Columns.Add("SITUACAO", typeof(string));
            table.Columns.Add("UN", typeof(string));
            table.Columns.Add("ESTOQUEDISPONIVEL", typeof(decimal));
            table.Columns.Add("ESTOQUEATUAL", typeof(decimal));
            table.Columns.Add("CODBARRASFAB", typeof(string));
            table.Columns.Add("CODMARCA", typeof(short));
            table.Columns.Add("UN_ALTERN", typeof(string));
            table.Columns.Add("QTDE_EMB", typeof(decimal));
            table.Columns.Add("QTDE_CONV", typeof(decimal));
            table.Columns.Add("PRECOCUSTO", typeof(decimal));
            table.Columns.Add("PRECOVENDA", typeof(decimal));
            table.Columns.Add("ESTOQUERESERVADO", typeof(decimal));
            table.Columns.Add("DATA_ALTERACAO", typeof(DateTime));
            table.Columns.Add("DESCRICAO", typeof(string));
            table.Rows.Add(productCode, "Produto", "A", "PC", DBNull.Value, 0m, "7899679701348", (short)5, "CX", 6m, 12m, DBNull.Value, 15m, DBNull.Value, DBNull.Value, "Marca");
            return table;
        }

        private static DataTable JoinedProductAndUnitRows(string productCode, bool includeProduct, bool productHasUnits, bool truncateUnitResults)
        {
            var table = new DataTable();
            table.Columns.Add("P_CODPRODUTO", typeof(string));
            table.Columns.Add("PRODUTO", typeof(string));
            table.Columns.Add("SITUACAO", typeof(string));
            table.Columns.Add("P_UN", typeof(string));
            table.Columns.Add("ESTOQUEDISPONIVEL", typeof(decimal));
            table.Columns.Add("ESTOQUEATUAL", typeof(decimal));
            table.Columns.Add("CODBARRASFAB", typeof(string));
            table.Columns.Add("CODMARCA", typeof(short));
            table.Columns.Add("UN_ALTERN", typeof(string));
            table.Columns.Add("QTDE_EMB", typeof(decimal));
            table.Columns.Add("QTDE_CONV", typeof(decimal));
            table.Columns.Add("PRECOCUSTO", typeof(decimal));
            table.Columns.Add("PRECOVENDA", typeof(decimal));
            table.Columns.Add("ESTOQUERESERVADO", typeof(decimal));
            table.Columns.Add("DATA_ALTERACAO", typeof(DateTime));
            table.Columns.Add("MARCA", typeof(string));
            table.Columns.Add("UNIT_CODE", typeof(string));
            table.Columns.Add("UNIT_DESCRIPTION", typeof(string));
            table.Columns.Add("UNIT_FACTOR", typeof(decimal));
            table.Columns.Add("UNIT_BARCODE", typeof(string));
            table.Columns.Add("UNIT_MASTER", typeof(string));
            table.Columns.Add("TOTAL_UNITS", typeof(long));
            if (!includeProduct)
            {
                return table;
            }

            var (pcBarcode, cxBarcode, cxFactor) = productCode switch
            {
                "503833" => ("7899679702137", "17899679702137", 1m),
                "503838" => ("7899679702659", "17899679702656", 1m),
                _ => ("7899679701348", "17899679701345", 18m)
            };
            void Add(string? unitCode, string? description, decimal? factor, string? barcode, string? isMaster, long count) =>
                table.Rows.Add(productCode, "Produto", "A", "PC", DBNull.Value, 0m, "7899679701348", (short)5, "CX", 6m, 12m,
                    DBNull.Value, 15m, DBNull.Value, DBNull.Value, "Marca", unitCode is null ? DBNull.Value : unitCode,
                    description is null ? DBNull.Value : description, factor.HasValue ? factor.Value : DBNull.Value,
                    barcode is null ? DBNull.Value : barcode, isMaster is null ? DBNull.Value : isMaster, count);

            if (productHasUnits)
            {
                Add("PC", "PACOTE", productCode == "503838" ? 8m : 1m, pcBarcode, "F", 2L);
                if (!truncateUnitResults)
                {
                    Add("CX", "CAIXA", cxFactor, cxBarcode, "T", 2L);
                }
            }
            else
            {
                Add(null, null, null, null, null, 0L);
            }
            return table;
        }

        public sealed record QueryCall(string Sql, IReadOnlyDictionary<string, object?>? Parameters);
    }
}
