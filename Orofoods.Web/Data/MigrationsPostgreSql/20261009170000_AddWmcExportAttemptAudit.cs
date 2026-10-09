using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261009170000_AddWmcExportAttemptAudit")]
public partial class AddWmcExportAttemptAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "AttemptId", table: "WmcExportAudits", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<int>(name: "Source", table: "WmcExportAudits", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "Outcome", table: "WmcExportAudits", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(name: "GeneratedAt", table: "WmcExportAudits", type: "timestamp without time zone", nullable: true);

        migrationBuilder.Sql("UPDATE \"WmcExportAudits\" SET \"Outcome\" = CASE WHEN \"Succeeded\" THEN 1 ELSE 3 END, \"GeneratedAt\" = CASE WHEN \"Succeeded\" THEN \"ExportedAt\" ELSE NULL END;");

        migrationBuilder.CreateIndex(
            name: "IX_WmcExportAudits_OrderId_AttemptId",
            table: "WmcExportAudits",
            columns: new[] { "OrderId", "AttemptId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_WmcExportAudits_OrderId_AttemptId", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "AttemptId", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "Source", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "Outcome", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "GeneratedAt", table: "WmcExportAudits");
    }
}
