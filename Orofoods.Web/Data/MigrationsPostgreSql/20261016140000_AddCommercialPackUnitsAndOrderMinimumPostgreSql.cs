using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20261016140000_AddCommercialPackUnitsAndOrderMinimumPostgreSql")]
public sealed class AddCommercialPackUnitsAndOrderMinimumPostgreSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Ean13", table: "Products", type: "character varying(13)", maxLength: 13, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Dun14", table: "Products", type: "character varying(14)", maxLength: 14, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CommercialUnitCode", table: "Products", type: "character varying(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<string>(name: "LogisticsUnitCode", table: "Products", type: "character varying(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<int>(name: "PackagesPerCase", table: "Products", type: "integer", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SalesUnitCodeSnapshot", table: "OrderItems", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.CreateTable(
            name: "OrderMinimumSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                MinimumAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderMinimumSettings", x => x.Id);
                table.CheckConstraint("CK_OrderMinimumSettings_Singleton", "\"Id\" = 1");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OrderMinimumSettings");
        migrationBuilder.DropColumn(name: "Ean13", table: "Products");
        migrationBuilder.DropColumn(name: "Dun14", table: "Products");
        migrationBuilder.DropColumn(name: "CommercialUnitCode", table: "Products");
        migrationBuilder.DropColumn(name: "LogisticsUnitCode", table: "Products");
        migrationBuilder.DropColumn(name: "PackagesPerCase", table: "Products");
        migrationBuilder.DropColumn(name: "SalesUnitCodeSnapshot", table: "OrderItems");
    }
}
