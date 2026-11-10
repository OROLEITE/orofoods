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
        migrationBuilder.AddColumn<int>(name: "HttpStatusCode", table: "WmcExportAudits", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResponseBody", table: "WmcExportAudits", type: "TEXT", maxLength: 2000, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HttpStatusCode", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "ResponseBody", table: "WmcExportAudits");
    }
}
