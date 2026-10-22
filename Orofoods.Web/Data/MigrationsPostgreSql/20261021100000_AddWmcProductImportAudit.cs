using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;

#nullable disable
namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261021100000_AddWmcProductImportAudit")]
public sealed class AddWmcProductImportAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "WmcProductImportAudits", columns: table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            BrandCode = table.Column<short>(type: "smallint", nullable: false),
            StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
            CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
            Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
            Selected = table.Column<int>(type: "integer", nullable: false),
            Created = table.Column<int>(type: "integer", nullable: false),
            Updated = table.Column<int>(type: "integer", nullable: false),
            Unchanged = table.Column<int>(type: "integer", nullable: false),
            Errors = table.Column<int>(type: "integer", nullable: false),
            ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
            ResultsJson = table.Column<string>(type: "text", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_WmcProductImportAudits", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_WmcProductImportAudits_CompletedAt", table: "WmcProductImportAudits", column: "CompletedAt");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "WmcProductImportAudits");
}
