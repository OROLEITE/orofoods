using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

public partial class AddSellerOrderDraftCreateRequestKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CreateRequestKey",
            table: "SellerOrderDrafts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SellerOrderDrafts_OwnerUserId_CreateRequestKey",
            table: "SellerOrderDrafts",
            columns: new[] { "OwnerUserId", "CreateRequestKey" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SellerOrderDrafts_OwnerUserId_CreateRequestKey",
            table: "SellerOrderDrafts");

        migrationBuilder.DropColumn(
            name: "CreateRequestKey",
            table: "SellerOrderDrafts");
    }
}
