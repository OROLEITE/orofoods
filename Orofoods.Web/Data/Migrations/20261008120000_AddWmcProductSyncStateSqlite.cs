using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Orofoods.Web.Data;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261008120000_AddWmcProductSyncStateSqlite")]
public partial class AddWmcProductSyncStateSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Ean", table: "Products", type: "TEXT", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "WmcBrandCode", table: "Products", type: "TEXT", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "WmcAlternateUnit", table: "Products", type: "TEXT", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "WmcPackageQuantity", table: "Products", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "WmcConversionQuantity", table: "Products", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "IsWmcActive", table: "Products", type: "INTEGER", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(
            name: "WmcStockAvailable", table: "Products", type: "INTEGER", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(
            name: "WmcInitialLoadReady", table: "Products", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX "IX_Products_WmcCode"
            ON "Products" (LOWER(TRIM("WmcCode")))
            WHERE NULLIF(TRIM("WmcCode"), '') IS NOT NULL;
            """);
        migrationBuilder.CreateTable(
            name: "WmcSyncRuns",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                InitialLoad = table.Column<bool>(type: "INTEGER", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                FailedStage = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                CustomersRead = table.Column<int>(type: "INTEGER", nullable: false),
                ProductsRead = table.Column<int>(type: "INTEGER", nullable: false),
                SellersRead = table.Column<int>(type: "INTEGER", nullable: false),
                StockRead = table.Column<int>(type: "INTEGER", nullable: false),
                ProductsCreated = table.Column<int>(type: "INTEGER", nullable: false),
                ProductsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                StockCreated = table.Column<int>(type: "INTEGER", nullable: false),
                StockUpdated = table.Column<int>(type: "INTEGER", nullable: false)
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
