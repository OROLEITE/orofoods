using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830213000_AddWmcExportEmailSnapshotSqliteSchema")]
public partial class AddWmcExportEmailSnapshotSqliteSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExportedByEmail",
            table: "WmcExportAudits",
            type: "TEXT",
            maxLength: 256,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExportedByEmail", table: "WmcExportAudits");
    }
}
