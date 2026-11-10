using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261015100000_AddAdminPortalAccessAudits")]
public sealed class AddAdminPortalAccessAudits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AdminPortalAccessAudits",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AdministratorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                AdministratorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                AccessType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                TargetId = table.Column<int>(type: "integer", nullable: false),
                TargetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                SelectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
