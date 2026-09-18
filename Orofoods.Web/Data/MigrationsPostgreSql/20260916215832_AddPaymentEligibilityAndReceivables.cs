using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddPaymentEligibilityAndReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "PaymentTerms",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DaysUntilDue",
                table: "PaymentTerms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CreditBlocked",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreditNotes",
                table: "Customers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreditOverrideEnabled",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreditReleaseDate",
                table: "Customers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumPaymentTermDays",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DigitableLine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankSlipUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExternalPaymentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerId",
                table: "Payments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId",
                unique: true);

            migrationBuilder.Sql("""
                UPDATE "PaymentTerms"
                SET "Code" = CASE "Name"
                        WHEN 'PIX' THEN 'PIX'
                        WHEN 'À vista' THEN 'CASH'
                        WHEN 'A vista' THEN 'CASH'
                        WHEN 'Cartão de crédito' THEN 'CREDIT_CARD'
                        WHEN 'Cartao de credito' THEN 'CREDIT_CARD'
                        WHEN '7 dias' THEN 'BOLETO_7D'
                        WHEN 'Boleto bancário — 7 dias' THEN 'BOLETO_7D'
                        WHEN '14 dias' THEN 'BOLETO_14D'
                        WHEN 'Boleto bancário — 14 dias' THEN 'BOLETO_14D'
                        ELSE "Code"
                    END,
                    "Name" = CASE "Name"
                        WHEN '7 dias' THEN 'Boleto bancário — 7 dias'
                        WHEN '14 dias' THEN 'Boleto bancário — 14 dias'
                        ELSE "Name"
                    END,
                    "DaysUntilDue" = CASE "Name"
                        WHEN '7 dias' THEN 7
                        WHEN 'Boleto bancário — 7 dias' THEN 7
                        WHEN '14 dias' THEN 14
                        WHEN 'Boleto bancário — 14 dias' THEN 14
                        ELSE 0
                    END,
                    "IsActive" = "Name" IN ('PIX', 'À vista', 'A vista', 'Cartão de crédito', 'Cartao de credito', '7 dias', '14 dias', 'Boleto bancário — 7 dias', 'Boleto bancário — 14 dias');
                """);

            migrationBuilder.Sql("""
                INSERT INTO "PaymentTerms" ("Code", "Name", "DaysUntilDue", "SortOrder", "IsActive")
                SELECT 'CASH', 'À vista', 0, 2, TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "PaymentTerms" WHERE "Code" = 'CASH');

                INSERT INTO "PaymentTerms" ("Code", "Name", "DaysUntilDue", "SortOrder", "IsActive")
                SELECT 'CREDIT_CARD', 'Cartão de crédito', 0, 3, TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "PaymentTerms" WHERE "Code" = 'CREDIT_CARD');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "PaymentTerms");

            migrationBuilder.DropColumn(
                name: "DaysUntilDue",
                table: "PaymentTerms");

            migrationBuilder.DropColumn(
                name: "CreditBlocked",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditNotes",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditOverrideEnabled",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditReleaseDate",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MaximumPaymentTermDays",
                table: "Customers");
        }
    }
}
