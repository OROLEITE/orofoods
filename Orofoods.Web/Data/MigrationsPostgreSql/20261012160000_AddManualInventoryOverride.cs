using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261012160000_AddManualInventoryOverride")]
public partial class AddManualInventoryOverride : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsManuallyAdjusted",
            table: "ProductInventories",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // Prior Admin adjustments are the only durable evidence that a balance
        // may have been manually curated. Protect those rows until an Admin
        // explicitly resumes WMC stock synchronization.
        migrationBuilder.Sql("""
            UPDATE "ProductInventories" AS inventory
            SET "IsManuallyAdjusted" = TRUE
            WHERE EXISTS (
                SELECT 1
                FROM "InventoryAdjustments" AS adjustment
                WHERE adjustment."ProductInventoryId" = inventory."Id");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "IsManuallyAdjusted",
            table: "ProductInventories");
}
