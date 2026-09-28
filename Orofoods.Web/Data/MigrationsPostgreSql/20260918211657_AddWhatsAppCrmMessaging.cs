using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddWhatsAppCrmMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppConversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssignedUserId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastInboundAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastOutboundAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppConversations_AspNetUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WhatsAppConversations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TextBody = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppMessages_WhatsAppConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "WhatsAppConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_AssignedUserId",
                table: "WhatsAppConversations",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_CustomerId",
                table: "WhatsAppConversations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_LastMessageAt",
                table: "WhatsAppConversations",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_PhoneNumber",
                table: "WhatsAppConversations",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_ConversationId_CreatedAt",
                table: "WhatsAppMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_ExternalMessageId",
                table: "WhatsAppMessages",
                column: "ExternalMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_Status",
                table: "WhatsAppMessages",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppMessages");

            migrationBuilder.DropTable(
                name: "WhatsAppConversations");
        }
    }
}
