using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830210000_AddWmcExportAuditsSqliteSchema")]
public partial class AddWmcExportAuditsSqliteSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcExportAudits",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                ExportedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                ExportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                Error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WmcExportAudits", x => x.Id);
                table.ForeignKey(
                    name: "FK_WmcExportAudits_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WmcExportAudits_OrderId_ExportedAt",
            table: "WmcExportAudits",
            columns: new[] { "OrderId", "ExportedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WmcExportAudits");
    }
}
