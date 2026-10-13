using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261013160001_AddWmcInitialLoadReleaseAuditSqlite")]
public partial class AddWmcInitialLoadReleaseAuditSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcInitialLoadReleaseAudits",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                WmcCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                AdminUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                ReleasedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WmcInitialLoadReleaseAudits", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_WmcInitialLoadReleaseAudits_ProductId_ReleasedAtUtc",
            table: "WmcInitialLoadReleaseAudits",
            columns: new[] { "ProductId", "ReleasedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "WmcInitialLoadReleaseAudits");
}
