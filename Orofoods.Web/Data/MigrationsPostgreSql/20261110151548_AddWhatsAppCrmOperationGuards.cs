using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddWhatsAppCrmOperationGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "WhatsAppConversations"
                        GROUP BY "PhoneNumber"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'WhatsApp CRM migration blocked: duplicate conversation phone numbers require manual history consolidation.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppConversations_PhoneNumber",
                table: "WhatsAppConversations");

            migrationBuilder.AddColumn<string>(
                name: "ClientRequestId",
                table: "WhatsAppMessages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_ClientRequestId",
                table: "WhatsAppMessages",
                column: "ClientRequestId",
                unique: true,
                filter: "\"ClientRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_PhoneNumber",
                table: "WhatsAppConversations",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppMessages_ClientRequestId",
                table: "WhatsAppMessages");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppConversations_PhoneNumber",
                table: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "WhatsAppMessages");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_PhoneNumber",
                table: "WhatsAppConversations",
                column: "PhoneNumber");
        }
    }
}
