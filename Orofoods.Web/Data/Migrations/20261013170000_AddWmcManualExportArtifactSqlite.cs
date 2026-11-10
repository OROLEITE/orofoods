using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261013170000_AddWmcManualExportArtifactSqlite")]
public partial class AddWmcManualExportArtifactSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WmcManualExportArtifacts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                AttemptId = table.Column<Guid>(type: "TEXT", nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                ContentSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                CreatedByEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                SupersedesArtifactId = table.Column<int>(type: "INTEGER", nullable: true),
                RegenerationReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WmcManualExportArtifacts", x => x.Id);
                table.ForeignKey("FK_WmcManualExportArtifacts_Orders_OrderId", x => x.OrderId, "Orders", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_WmcManualExportArtifacts_WmcManualExportArtifacts_SupersedesArtifactId", x => x.SupersedesArtifactId, "WmcManualExportArtifacts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_WmcManualExportArtifacts_AttemptId", "WmcManualExportArtifacts", "AttemptId", unique: true);
        migrationBuilder.CreateIndex("IX_WmcManualExportArtifacts_OrderId_CreatedAt", "WmcManualExportArtifacts", new[] { "OrderId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_WmcManualExportArtifacts_SupersedesArtifactId", "WmcManualExportArtifacts", "SupersedesArtifactId");

        migrationBuilder.Sql("""
            CREATE TABLE "__WmcOrderExportStates" (
                "OrderId" INTEGER NOT NULL CONSTRAINT "PK_WmcOrderExportStates" PRIMARY KEY,
                "Status" TEXT NOT NULL,
                "RequiresManualReview" INTEGER NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "ProcessingStartedAt" TEXT NULL,
                "Version" INTEGER NOT NULL,
                "ExportedByUserId" TEXT NULL,
                "ExportedByEmail" TEXT NULL,
                "ExportedAt" TEXT NULL,
                "FileName" TEXT NULL,
                "ReviewedByUserId" TEXT NULL,
                "ReviewedByEmail" TEXT NULL,
                "ReviewedAt" TEXT NULL,
                "ImportedByUserId" TEXT NULL,
                "ImportedByEmail" TEXT NULL,
                "ImportedAt" TEXT NULL,
                "LastError" TEXT NULL,
                "CurrentArtifactId" INTEGER NULL,
                CONSTRAINT "CK_WmcOrderExportStates_Status" CHECK ("Status" IN ('Pending', 'Processing', 'Exported', 'Rejected', 'Imported', 'Error')),
                CONSTRAINT "FK_WmcOrderExportStates_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_WmcOrderExportStates_WmcManualExportArtifacts_CurrentArtifactId" FOREIGN KEY ("CurrentArtifactId") REFERENCES "WmcManualExportArtifacts" ("Id") ON DELETE SET NULL
            );
            """);
        migrationBuilder.Sql("""
            INSERT INTO "__WmcOrderExportStates" (
                "OrderId", "Status", "RequiresManualReview", "UpdatedAt", "ProcessingStartedAt", "Version",
                "ExportedByUserId", "ExportedByEmail", "ExportedAt", "FileName", "ReviewedByUserId", "ReviewedByEmail",
                "ReviewedAt", "ImportedByUserId", "ImportedByEmail", "ImportedAt", "LastError", "CurrentArtifactId")
            SELECT "OrderId", "Status", "RequiresManualReview", "UpdatedAt", "ProcessingStartedAt", "Version",
                "ExportedByUserId", "ExportedByEmail", "ExportedAt", "FileName", "ReviewedByUserId", "ReviewedByEmail",
                "ReviewedAt", "ImportedByUserId", "ImportedByEmail", "ImportedAt", "LastError", NULL
            FROM "WmcOrderExportStates";
            """);
        migrationBuilder.Sql("DROP TABLE \"WmcOrderExportStates\";");
        migrationBuilder.Sql("ALTER TABLE \"__WmcOrderExportStates\" RENAME TO \"WmcOrderExportStates\";");
        migrationBuilder.CreateIndex("IX_WmcOrderExportStates_CurrentArtifactId", "WmcOrderExportStates", "CurrentArtifactId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "__WmcOrderExportStates" (
                "OrderId" INTEGER NOT NULL CONSTRAINT "PK_WmcOrderExportStates" PRIMARY KEY,
                "Status" TEXT NOT NULL,
                "RequiresManualReview" INTEGER NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "ProcessingStartedAt" TEXT NULL,
                "Version" INTEGER NOT NULL,
                "ExportedByUserId" TEXT NULL,
                "ExportedByEmail" TEXT NULL,
                "ExportedAt" TEXT NULL,
                "FileName" TEXT NULL,
                "ReviewedByUserId" TEXT NULL,
                "ReviewedByEmail" TEXT NULL,
                "ReviewedAt" TEXT NULL,
                "ImportedByUserId" TEXT NULL,
                "ImportedByEmail" TEXT NULL,
                "ImportedAt" TEXT NULL,
                "LastError" TEXT NULL,
                CONSTRAINT "CK_WmcOrderExportStates_Status" CHECK ("Status" IN ('Pending', 'Processing', 'Exported', 'Imported', 'Error')),
                CONSTRAINT "FK_WmcOrderExportStates_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE
            );
            """);
        migrationBuilder.Sql("""
            INSERT INTO "__WmcOrderExportStates" (
                "OrderId", "Status", "RequiresManualReview", "UpdatedAt", "ProcessingStartedAt", "Version",
                "ExportedByUserId", "ExportedByEmail", "ExportedAt", "FileName", "ReviewedByUserId", "ReviewedByEmail",
                "ReviewedAt", "ImportedByUserId", "ImportedByEmail", "ImportedAt", "LastError")
            SELECT "OrderId", "Status", "RequiresManualReview", "UpdatedAt", "ProcessingStartedAt", "Version",
                "ExportedByUserId", "ExportedByEmail", "ExportedAt", "FileName", "ReviewedByUserId", "ReviewedByEmail",
                "ReviewedAt", "ImportedByUserId", "ImportedByEmail", "ImportedAt", "LastError"
            FROM "WmcOrderExportStates";
            """);
        migrationBuilder.Sql("DROP TABLE \"WmcOrderExportStates\";");
        migrationBuilder.Sql("ALTER TABLE \"__WmcOrderExportStates\" RENAME TO \"WmcOrderExportStates\";");
        migrationBuilder.DropTable("WmcManualExportArtifacts");
    }
}
