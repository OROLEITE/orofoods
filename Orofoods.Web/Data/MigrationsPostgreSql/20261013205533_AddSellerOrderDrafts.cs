using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddSellerOrderDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SellerOrderDraftId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SellerOrderDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SalesRepresentativeId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    DeliveryAddressId = table.Column<int>(type: "integer", nullable: true),
                    PaymentTermId = table.Column<int>(type: "integer", nullable: true),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerOrderDrafts", x => x.Id);
                    table.CheckConstraint("CK_SellerOrderDrafts_Status", "\"Status\" IN ('Draft', 'Submitted')");
                    table.CheckConstraint("CK_SellerOrderDrafts_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_SellerOrderDrafts_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerOrderDrafts_CustomerAddresses_DeliveryAddressId",
                        column: x => x.DeliveryAddressId,
                        principalTable: "CustomerAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerOrderDrafts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerOrderDrafts_PaymentTerms_PaymentTermId",
                        column: x => x.PaymentTermId,
                        principalTable: "PaymentTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerOrderDrafts_SalesRepresentatives_SalesRepresentativeId",
                        column: x => x.SalesRepresentativeId,
                        principalTable: "SalesRepresentatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellerOrderDraftItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SellerOrderDraftId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RequestedDiscountAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerOrderDraftItems", x => x.Id);
                    table.CheckConstraint("CK_SellerOrderDraftItems_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_SellerOrderDraftItems_RequestedDiscountAmount", "\"RequestedDiscountAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_SellerOrderDraftItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerOrderDraftItems_SellerOrderDrafts_SellerOrderDraftId",
                        column: x => x.SellerOrderDraftId,
                        principalTable: "SellerOrderDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SellerOrderDraftId",
                table: "Orders",
                column: "SellerOrderDraftId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrderDraftItems_ProductId",
                table: "SellerOrderDraftItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrderDraftItems_SellerOrderDraftId_ProductId",
                table: "SellerOrderDraftItems",
                columns: new[] { "SellerOrderDraftId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrderDrafts_CustomerId",
                table: "SellerOrderDrafts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrderDrafts_DeliveryAddressId",
                table: "SellerOrderDrafts",
                column: "DeliveryAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrderDrafts_OwnerUserId_Status_UpdatedAt",
                table: "SellerOrderDrafts",
                columns: new[] { "OwnerUserId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrderDrafts_PaymentTermId",
                table: "SellerOrderDrafts",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerDrafts_Rep_Customer_Status_Updated",
                table: "SellerOrderDrafts",
                columns: new[] { "SalesRepresentativeId", "CustomerId", "Status", "UpdatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_SellerOrderDrafts_SellerOrderDraftId",
                table: "Orders",
                column: "SellerOrderDraftId",
                principalTable: "SellerOrderDrafts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_SellerOrderDrafts_SellerOrderDraftId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "SellerOrderDraftItems");

            migrationBuilder.DropTable(
                name: "SellerOrderDrafts");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SellerOrderDraftId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SellerOrderDraftId",
                table: "Orders");
        }
    }
}
