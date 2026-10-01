using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddWhatsAppInboundMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Caption",
                table: "WhatsAppMessages",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "WhatsAppMessages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVoiceMessage",
                table: "WhatsAppMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MediaId",
                table: "WhatsAppMessages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MediaSizeBytes",
                table: "WhatsAppMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaState",
                table: "WhatsAppMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MediaStorageReference",
                table: "WhatsAppMessages",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "WhatsAppMessages",
                type: "character varying(127)",
                maxLength: 127,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_MediaId",
                table: "WhatsAppMessages",
                column: "MediaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppMessages_MediaId",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "Caption",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "IsVoiceMessage",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaSizeBytes",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaState",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaStorageReference",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "WhatsAppMessages");
        }
    }
}
