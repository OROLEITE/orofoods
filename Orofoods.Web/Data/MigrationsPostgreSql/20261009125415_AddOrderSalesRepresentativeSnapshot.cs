using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddOrderSalesRepresentativeSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalesRepresentativeId",
                table: "Orders",
                type: "integer",
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

        /// <inheritdoc />
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
}
