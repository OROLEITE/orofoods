using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261016140000_AddCommercialPackUnitsAndOrderMinimum")]
public sealed class AddCommercialPackUnitsAndOrderMinimum : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Ean13", table: "Products", type: "TEXT", maxLength: 13, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Dun14", table: "Products", type: "TEXT", maxLength: 14, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CommercialUnitCode", table: "Products", type: "TEXT", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<string>(name: "LogisticsUnitCode", table: "Products", type: "TEXT", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<int>(name: "PackagesPerCase", table: "Products", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SalesUnitCodeSnapshot", table: "OrderItems", type: "TEXT", maxLength: 20, nullable: true);
        migrationBuilder.CreateTable(
            name: "OrderMinimumSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                MinimumAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderMinimumSettings", x => x.Id);
                table.CheckConstraint("CK_OrderMinimumSettings_Singleton", "\"Id\" = 1");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OrderMinimumSettings");
        migrationBuilder.DropColumn(name: "Ean13", table: "Products");
        migrationBuilder.DropColumn(name: "Dun14", table: "Products");
        migrationBuilder.DropColumn(name: "CommercialUnitCode", table: "Products");
        migrationBuilder.DropColumn(name: "LogisticsUnitCode", table: "Products");
        migrationBuilder.DropColumn(name: "PackagesPerCase", table: "Products");
        migrationBuilder.DropColumn(name: "SalesUnitCodeSnapshot", table: "OrderItems");
    }
}
