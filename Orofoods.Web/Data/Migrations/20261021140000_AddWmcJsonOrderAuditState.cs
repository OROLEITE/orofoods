using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021140000_AddWmcJsonOrderAuditState")]
public partial class AddWmcJsonOrderAuditState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "AttemptId", table: "WmcExportAudits", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>(name: "Source", table: "WmcExportAudits", type: "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "Outcome", table: "WmcExportAudits", type: "INTEGER", nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<int>(name: "HttpStatusCode", table: "WmcExportAudits", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResponseBody", table: "WmcExportAudits", type: "TEXT", maxLength: 2000, nullable: true);
        migrationBuilder.Sql("UPDATE \"WmcExportAudits\" SET \"Outcome\" = CASE WHEN \"Succeeded\" THEN 1 ELSE 2 END;");
        migrationBuilder.CreateIndex(
            name: "IX_WmcExportAudits_OrderId_AttemptId",
            table: "WmcExportAudits",
            columns: new[] { "OrderId", "AttemptId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_WmcExportAudits_OrderId_AttemptId", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "AttemptId", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "Source", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "Outcome", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "HttpStatusCode", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "ResponseBody", table: "WmcExportAudits");
    }
}
