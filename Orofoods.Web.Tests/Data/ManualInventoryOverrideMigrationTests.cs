using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;

namespace Orofoods.Web.Tests.Data;

public class ManualInventoryOverrideMigrationTests
{
    [Fact]
    public void PostgreSql_migration_script_adds_false_default_for_existing_inventory_rows()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=orofoods_migration_script_only;Username=script;Password=script")
            .Options;
        using var db = new PostgreSqlApplicationDbContext(options);

        var script = db.GetService<IMigrator>().GenerateScript(
            "20261012120000_AddWmcOrderExportState",
            "20261012160000_AddManualInventoryOverride");

        Assert.Contains("ALTER TABLE \"ProductInventories\" ADD \"IsManuallyAdjusted\" boolean NOT NULL DEFAULT FALSE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPDATE \"ProductInventories\" AS inventory", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM \"InventoryAdjustments\" AS adjustment", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("adjustment.\"ProductInventoryId\" = inventory.\"Id\"", script, StringComparison.OrdinalIgnoreCase);
    }
}
