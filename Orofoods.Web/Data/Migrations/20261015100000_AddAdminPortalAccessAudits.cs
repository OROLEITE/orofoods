using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261015100000_AddAdminPortalAccessAudits")]
public sealed class AddAdminPortalAccessAudits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AdminPortalAccessAudits",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AdministratorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                AdministratorEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                AccessType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TargetId = table.Column<int>(type: "INTEGER", nullable: false),
                TargetName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                SelectedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AdminPortalAccessAudits", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AdminPortalAccessAudits_AdministratorUserId_SelectedAt",
            table: "AdminPortalAccessAudits",
            columns: new[] { "AdministratorUserId", "SelectedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_AdminPortalAccessAudits_AccessType_TargetId_SelectedAt",
            table: "AdminPortalAccessAudits",
            columns: new[] { "AccessType", "TargetId", "SelectedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "AdminPortalAccessAudits");
}
