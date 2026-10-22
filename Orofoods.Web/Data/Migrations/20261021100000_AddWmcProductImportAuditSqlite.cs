using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;

#nullable disable
namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021100000_AddWmcProductImportAuditSqlite")]
public sealed class AddWmcProductImportAuditSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "WmcProductImportAudits", columns: table => new
        {
            Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
            BrandCode = table.Column<short>(type: "INTEGER", nullable: false),
            StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
            CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
            Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
            Selected = table.Column<int>(type: "INTEGER", nullable: false),
            Created = table.Column<int>(type: "INTEGER", nullable: false),
            Updated = table.Column<int>(type: "INTEGER", nullable: false),
            Unchanged = table.Column<int>(type: "INTEGER", nullable: false),
            Errors = table.Column<int>(type: "INTEGER", nullable: false),
            ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
            ResultsJson = table.Column<string>(type: "TEXT", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_WmcProductImportAudits", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_WmcProductImportAudits_CompletedAt", table: "WmcProductImportAudits", column: "CompletedAt");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "WmcProductImportAudits");
}
