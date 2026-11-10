using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddCustomerCnpjOrderSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerCnpjSnapshot",
                table: "Orders",
                type: "character varying(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerCnpjAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    PreviousCnpj = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: true),
                    NewCnpj = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ChangedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCnpjAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerCnpjAudits_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCnpjAudits_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCnpjAudits_CustomerId_ChangedAt",
                table: "CustomerCnpjAudits",
                columns: new[] { "CustomerId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCnpjAudits_OrderId_ChangedAt",
                table: "CustomerCnpjAudits",
                columns: new[] { "OrderId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCnpjAudits");

            migrationBuilder.DropColumn(
                name: "CustomerCnpjSnapshot",
                table: "Orders");
        }
    }
}
