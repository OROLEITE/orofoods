using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261015130000_AddAdminUserManagement")]
public sealed class AddAdminUserManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FullName",
            table: "AspNetUsers",
            type: "TEXT",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "AspNetUsers",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE AspNetUsers SET FullName = UserName WHERE FullName = '';");

        migrationBuilder.CreateTable(
            name: "AdminUserChangeAudits",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ActorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                ActorName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                TargetUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                TargetName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AdminUserChangeAudits", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AdminUserChangeAudits_ActorUserId_ChangedAt",
            table: "AdminUserChangeAudits",
            columns: new[] { "ActorUserId", "ChangedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_AdminUserChangeAudits_TargetUserId_ChangedAt",
            table: "AdminUserChangeAudits",
            columns: new[] { "TargetUserId", "ChangedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AdminUserChangeAudits");
        migrationBuilder.Sql("ALTER TABLE AspNetUsers DROP COLUMN MustChangePassword;");
        migrationBuilder.Sql("ALTER TABLE AspNetUsers DROP COLUMN FullName;");
    }
}
