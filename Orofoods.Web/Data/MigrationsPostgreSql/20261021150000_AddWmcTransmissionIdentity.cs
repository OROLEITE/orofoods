using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261021150000_AddWmcTransmissionIdentity")]
public partial class AddWmcTransmissionIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE SEQUENCE \"WmcTransmissionIdSequence\" AS integer START WITH 1 INCREMENT BY 1 MINVALUE 1 MAXVALUE 2147483647 NO CYCLE;");
        migrationBuilder.AddColumn<int>(name: "WmcTransmissionId", table: "Orders", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "WmcTransmissionId", table: "WmcExportAudits", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "NroPedidoHttpStatusCode", table: "WmcExportAudits", type: "integer", nullable: true);
        migrationBuilder.AddColumn<string>(name: "NroPedidoResponseBody", table: "WmcExportAudits", type: "character varying(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "NroPedidoError", table: "WmcExportAudits", type: "character varying(2000)", maxLength: 2000, nullable: true);
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
        migrationBuilder.Sql("DROP SEQUENCE IF EXISTS \"WmcTransmissionIdSequence\";");
    }
}
