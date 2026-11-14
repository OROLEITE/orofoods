using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddWhatsAppContactProfileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileName",
                table: "WhatsAppConversations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileName",
                table: "WhatsAppConversations");
        }
    }
}
