using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orofoods.Web.Data;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261009125415_AddOrderSalesRepresentativeSnapshotSqlite")]
public partial class AddOrderSalesRepresentativeSnapshotSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SalesRepresentativeId",
            table: "Orders",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_SalesRepresentativeId",
            table: "Orders",
            column: "SalesRepresentativeId");

        migrationBuilder.AddForeignKey(
            name: "FK_Orders_SalesRepresentatives_SalesRepresentativeId",
            table: "Orders",
            column: "SalesRepresentativeId",
            principalTable: "SalesRepresentatives",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Orders_SalesRepresentatives_SalesRepresentativeId",
            table: "Orders");

        migrationBuilder.DropIndex(
            name: "IX_Orders_SalesRepresentativeId",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "SalesRepresentativeId",
            table: "Orders");
    }
}
