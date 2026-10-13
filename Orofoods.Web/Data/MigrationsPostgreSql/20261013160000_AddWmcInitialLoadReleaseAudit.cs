using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261013160000_AddWmcInitialLoadReleaseAudit")]
public partial class AddWmcInitialLoadReleaseAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcInitialLoadReleaseAudits",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ProductId = table.Column<int>(type: "integer", nullable: false),
                WmcCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                AdminUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WmcInitialLoadReleaseAudits", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_WmcInitialLoadReleaseAudits_ProductId_ReleasedAtUtc",
            table: "WmcInitialLoadReleaseAudits",
            columns: new[] { "ProductId", "ReleasedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "WmcInitialLoadReleaseAudits");
}
