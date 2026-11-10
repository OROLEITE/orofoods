using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;

#nullable disable
namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261021110000_AddWmcProductUnits")]
public sealed class AddWmcProductUnits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcProductUnits",
            columns: table => new
            {
                ProductId = table.Column<int>(type: "integer", nullable: false),
                UnitCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Description = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                Barcode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                IsMaster = table.Column<bool>(type: "boolean", nullable: false)
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
