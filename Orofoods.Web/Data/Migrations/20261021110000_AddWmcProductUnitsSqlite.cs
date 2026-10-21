using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;

#nullable disable
namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021110000_AddWmcProductUnitsSqlite")]
public sealed class AddWmcProductUnitsSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcProductUnits",
            columns: table => new
            {
                ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                UnitCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Factor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                Barcode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                IsMaster = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WmcProductUnits", x => new { x.ProductId, x.UnitCode });
                table.ForeignKey(
                    name: "FK_WmcProductUnits_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "WmcProductUnits");
}
