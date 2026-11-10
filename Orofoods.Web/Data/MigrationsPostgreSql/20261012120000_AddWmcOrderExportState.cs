using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261012120000_AddWmcOrderExportState")]
public partial class AddWmcOrderExportState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcOrderExportStates",
            columns: table => new
            {
                OrderId = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                RequiresManualReview = table.Column<bool>(type: "boolean", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                ProcessingStartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ExportedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                ExportedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ExportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                FileName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                ReviewedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                ReviewedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                ImportedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                ImportedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ImportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WmcOrderExportStates", x => x.OrderId);
                table.CheckConstraint(
                    name: "CK_WmcOrderExportStates_Status",
                    sql: "\"Status\" IN ('Pending', 'Processing', 'Exported', 'Imported', 'Error')");
                table.ForeignKey(
                    name: "FK_WmcOrderExportStates_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql("""
            INSERT INTO "WmcOrderExportStates"
                ("OrderId", "Status", "RequiresManualReview", "UpdatedAt", "Version", "FileName", "LastError")
            SELECT audit."OrderId",
                'Pending',
                TRUE,
                MAX(audit."ExportedAt"),
                1,
                (
                    SELECT latest."FileName"
                    FROM "WmcExportAudits" AS latest
                    WHERE latest."OrderId" = audit."OrderId"
                    ORDER BY latest."ExportedAt" DESC, latest."Id" DESC
                    LIMIT 1
                ),
                'Legacy WMC history requires manual review; it does not confirm delivery or import.'
            FROM "WmcExportAudits" AS audit
            GROUP BY audit."OrderId";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "WmcOrderExportStates");
}
