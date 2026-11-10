using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021150000_AddWmcTransmissionIdentity")]
public partial class AddWmcTransmissionIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "WmcTransmissionId", table: "Orders", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<int>(name: "WmcTransmissionId", table: "WmcExportAudits", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<int>(name: "NroPedidoHttpStatusCode", table: "WmcExportAudits", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<string>(name: "NroPedidoResponseBody", table: "WmcExportAudits", type: "TEXT", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "NroPedidoError", table: "WmcExportAudits", type: "TEXT", maxLength: 2000, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Orders_WmcTransmissionId", table: "Orders", column: "WmcTransmissionId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Orders_WmcTransmissionId", table: "Orders");
        migrationBuilder.DropColumn(name: "WmcTransmissionId", table: "Orders");
        migrationBuilder.DropColumn(name: "WmcTransmissionId", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "NroPedidoHttpStatusCode", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "NroPedidoResponseBody", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "NroPedidoError", table: "WmcExportAudits");
    }
}
