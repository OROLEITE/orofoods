using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261013170000_AddWmcManualExportArtifact")]
public partial class AddWmcManualExportArtifact : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcManualExportArtifacts",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrderId = table.Column<int>(type: "integer", nullable: false),
                AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                CreatedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                SupersedesArtifactId = table.Column<int>(type: "integer", nullable: true),
                RegenerationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WmcManualExportArtifacts", x => x.Id);
                table.ForeignKey("FK_WmcManualExportArtifacts_Orders_OrderId", x => x.OrderId, "Orders", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_WmcManualExportArtifacts_WmcManualExportArtifacts_SupersedesArtifactId", x => x.SupersedesArtifactId, "WmcManualExportArtifacts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<int>("CurrentArtifactId", "WmcOrderExportStates", type: "integer", nullable: true);
        migrationBuilder.CreateIndex("IX_WmcManualExportArtifacts_AttemptId", "WmcManualExportArtifacts", "AttemptId", unique: true);
        migrationBuilder.CreateIndex("IX_WmcManualExportArtifacts_OrderId_CreatedAt", "WmcManualExportArtifacts", new[] { "OrderId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_WmcManualExportArtifacts_SupersedesArtifactId", "WmcManualExportArtifacts", "SupersedesArtifactId");
        migrationBuilder.CreateIndex("IX_WmcOrderExportStates_CurrentArtifactId", "WmcOrderExportStates", "CurrentArtifactId");
        migrationBuilder.AddForeignKey("FK_WmcOrderExportStates_WmcManualExportArtifacts_CurrentArtifactId", "WmcOrderExportStates", "CurrentArtifactId", "WmcManualExportArtifacts", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        migrationBuilder.DropCheckConstraint("CK_WmcOrderExportStates_Status", "WmcOrderExportStates");
        migrationBuilder.AddCheckConstraint("CK_WmcOrderExportStates_Status", "WmcOrderExportStates", "\"Status\" IN ('Pending', 'Processing', 'Exported', 'Rejected', 'Imported', 'Error')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_WmcOrderExportStates_Status", "WmcOrderExportStates");
        migrationBuilder.AddCheckConstraint("CK_WmcOrderExportStates_Status", "WmcOrderExportStates", "\"Status\" IN ('Pending', 'Processing', 'Exported', 'Imported', 'Error')");
        migrationBuilder.DropForeignKey("FK_WmcOrderExportStates_WmcManualExportArtifacts_CurrentArtifactId", "WmcOrderExportStates");
        migrationBuilder.DropIndex("IX_WmcOrderExportStates_CurrentArtifactId", "WmcOrderExportStates");
        migrationBuilder.DropColumn("CurrentArtifactId", "WmcOrderExportStates");
        migrationBuilder.DropTable("WmcManualExportArtifacts");
    }
}
