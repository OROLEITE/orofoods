using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261016161824_AddWmcBimboProductImportAuditSqlite")]
public sealed class AddWmcBimboProductImportAuditSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcProductImportRuns",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BrandCode = table.Column<short>(type: "INTEGER", nullable: false),
                ActorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Outcome = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                ImportedProductCodes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                SkippedProductCodes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                ErrorSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_WmcProductImportRuns", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_WmcProductImportRuns_BrandCode_StartedAtUtc",
            table: "WmcProductImportRuns",
            columns: new[] { "BrandCode", "StartedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "WmcProductImportRuns");
}
