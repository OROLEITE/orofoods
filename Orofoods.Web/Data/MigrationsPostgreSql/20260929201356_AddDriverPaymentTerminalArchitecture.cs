using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddDriverPaymentTerminalArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DriverPaymentTerminalAssignmentId",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTerminals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    StoreId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PosId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerminals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DriverPaymentTerminalAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DriverId = table.Column<int>(type: "integer", nullable: false),
                    PaymentTerminalId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverPaymentTerminalAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverPaymentTerminalAssignments_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverPaymentTerminalAssignments_PaymentTerminals_PaymentTe~",
                        column: x => x.PaymentTerminalId,
                        principalTable: "PaymentTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DriverPaymentTerminalAssignmentId",
                table: "Payments",
                column: "DriverPaymentTerminalAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverPaymentTerminalAssignments_DriverId",
                table: "DriverPaymentTerminalAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverPaymentTerminalAssignments_PaymentTerminalId",
                table: "DriverPaymentTerminalAssignments",
                column: "PaymentTerminalId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Name",
                table: "Drivers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerminals_Provider_DeviceId",
                table: "PaymentTerminals",
                columns: new[] { "Provider", "DeviceId" },
                unique: true,
                filter: "\"DeviceId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_DriverPaymentTerminalAssignments_DriverPaymentTerm~",
                table: "Payments",
                column: "DriverPaymentTerminalAssignmentId",
                principalTable: "DriverPaymentTerminalAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_DriverPaymentTerminalAssignments_DriverPaymentTerm~",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "DriverPaymentTerminalAssignments");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropTable(
                name: "PaymentTerminals");

            migrationBuilder.DropIndex(
                name: "IX_Payments_DriverPaymentTerminalAssignmentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DriverPaymentTerminalAssignmentId",
                table: "Payments");
        }
    }
}
