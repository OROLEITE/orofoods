using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddInternalSalesUserToCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternalSalesUserId",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedUserId",
                table: "CommercialActivities",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_InternalSalesUserId",
                table: "Customers",
                column: "InternalSalesUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialActivities_AssignedUserId",
                table: "CommercialActivities",
                column: "AssignedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommercialActivities_AspNetUsers_AssignedUserId",
                table: "CommercialActivities",
                column: "AssignedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AspNetUsers_InternalSalesUserId",
                table: "Customers",
                column: "InternalSalesUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommercialActivities_AspNetUsers_AssignedUserId",
                table: "CommercialActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AspNetUsers_InternalSalesUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_InternalSalesUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CommercialActivities_AssignedUserId",
                table: "CommercialActivities");

            migrationBuilder.DropColumn(
                name: "InternalSalesUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "CommercialActivities");
        }
    }
}
