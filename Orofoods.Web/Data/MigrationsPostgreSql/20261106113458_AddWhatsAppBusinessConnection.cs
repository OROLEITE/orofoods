using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddWhatsAppBusinessConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppBusinessConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessAccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PhoneNumberId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayPhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProtectedAccessToken = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppBusinessConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBusinessConnections_PhoneNumberId",
                table: "WhatsAppBusinessConnections",
                column: "PhoneNumberId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppBusinessConnections");
        }
    }
}
