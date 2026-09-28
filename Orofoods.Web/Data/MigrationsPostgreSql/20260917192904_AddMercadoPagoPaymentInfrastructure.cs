using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddMercadoPagoPaymentInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.AddColumn<string>(
                name: "AuthorizationCode",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardBrand",
                table: "Payments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Payments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "Payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gateway",
                table: "Payments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayOrderId",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentId",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Installments",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastFourDigits",
                table: "Payments",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Method",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PixCopyPaste",
                table: "Payments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PixQrCodeBase64",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Payments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Payments\" SET \"UpdatedAt\" = \"CreatedAt\" WHERE \"UpdatedAt\" IS NULL;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Payments",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayOrderId",
                table: "Payments",
                column: "GatewayOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayPaymentId",
                table: "Payments",
                column: "GatewayPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration is not reversible while multiple attempts exist for one order.
            // Abort before any schema change; never delete or consolidate payment records implicitly.
            migrationBuilder.Sql("""
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Payments"
                        GROUP BY "OrderId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot roll back Mercado Pago payment infrastructure: multiple payment attempts exist for an order. Preserve payment records and keep this migration applied.';
                    END IF;
                END;
                $migration$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Payments_GatewayOrderId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_GatewayPaymentId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AuthorizationCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CardBrand",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayOrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Installments",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LastFourDigits",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PixCopyPaste",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PixQrCodeBase64",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId",
                unique: true);
        }
    }
}
