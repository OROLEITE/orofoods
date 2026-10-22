using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

public partial class AddAdminUserManagementPostgreSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FullName",
            table: "AspNetUsers",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "AspNetUsers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"FullName\" = \"UserName\" WHERE \"FullName\" = '';");

        migrationBuilder.CreateTable(
            name: "AdminUserChangeAudits",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                ActorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                TargetUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                TargetName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
        migrationBuilder.DropColumn(name: "FullName", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "MustChangePassword", table: "AspNetUsers");
    }
}
