using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Orofoods.Web.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261008120000_AddWmcProductSyncState")]
public partial class AddWmcProductSyncState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Ean", table: "Products", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "WmcBrandCode", table: "Products", type: "character varying(30)", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "WmcAlternateUnit", table: "Products", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "WmcPackageQuantity", table: "Products", type: "numeric(12,3)", nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "WmcConversionQuantity", table: "Products", type: "numeric(12,3)", nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "IsWmcActive", table: "Products", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(
            name: "WmcStockAvailable", table: "Products", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(
            name: "WmcInitialLoadReady", table: "Products", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX "IX_Products_WmcCode"
            ON "Products" (LOWER(BTRIM("WmcCode")))
            WHERE NULLIF(BTRIM("WmcCode"), '') IS NOT NULL;
            """);
        migrationBuilder.CreateTable(
            name: "WmcSyncRuns",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                FinishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                InitialLoad = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                FailedStage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CustomersRead = table.Column<int>(type: "integer", nullable: false),
                ProductsRead = table.Column<int>(type: "integer", nullable: false),
                SellersRead = table.Column<int>(type: "integer", nullable: false),
                StockRead = table.Column<int>(type: "integer", nullable: false),
                ProductsCreated = table.Column<int>(type: "integer", nullable: false),
                ProductsUpdated = table.Column<int>(type: "integer", nullable: false),
                StockCreated = table.Column<int>(type: "integer", nullable: false),
                StockUpdated = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WmcSyncRuns", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_WmcSyncRuns_Status_StartedAt", table: "WmcSyncRuns", columns: new[] { "Status", "StartedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Products_WmcCode\";");
        migrationBuilder.DropIndex(name: "IX_WmcSyncRuns_Status_StartedAt", table: "WmcSyncRuns");
        migrationBuilder.DropColumn(name: "Ean", table: "Products");
        migrationBuilder.DropColumn(name: "WmcBrandCode", table: "Products");
        migrationBuilder.DropColumn(name: "WmcAlternateUnit", table: "Products");
        migrationBuilder.DropColumn(name: "WmcPackageQuantity", table: "Products");
        migrationBuilder.DropColumn(name: "WmcConversionQuantity", table: "Products");
        migrationBuilder.DropColumn(name: "IsWmcActive", table: "Products");
        migrationBuilder.DropColumn(name: "WmcStockAvailable", table: "Products");
        migrationBuilder.DropColumn(name: "WmcInitialLoadReady", table: "Products");
        migrationBuilder.DropTable(name: "WmcSyncRuns");
    }
}
