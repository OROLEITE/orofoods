using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830203500_AddWmcMappingsToSqliteSchema")]
public partial class AddWmcMappingsToSqliteSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "WmcCode",
            table: "Customers",
            type: "TEXT",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WmcCode",
            table: "Products",
            type: "TEXT",
            maxLength: 30,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WmcCode", table: "Customers");
        migrationBuilder.DropColumn(name: "WmcCode", table: "Products");
    }
}
