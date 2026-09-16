using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIntegrationStateSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ExternalOrderId", table: "Orders", type: "TEXT", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ErpOrderNumber", table: "Orders", type: "TEXT", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<int>(name: "IntegrationStatus", table: "Orders", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateTime>(name: "LastIntegrationAttempt", table: "Orders", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "IntegrationError", table: "Orders", type: "TEXT", maxLength: 2000, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExternalOrderId", table: "Orders");
            migrationBuilder.DropColumn(name: "ErpOrderNumber", table: "Orders");
            migrationBuilder.DropColumn(name: "IntegrationStatus", table: "Orders");
            migrationBuilder.DropColumn(name: "LastIntegrationAttempt", table: "Orders");
            migrationBuilder.DropColumn(name: "IntegrationError", table: "Orders");
        }
    }
}
