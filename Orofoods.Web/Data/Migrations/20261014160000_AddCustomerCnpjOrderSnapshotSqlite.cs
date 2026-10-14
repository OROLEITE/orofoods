using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261014160000_AddCustomerCnpjOrderSnapshotSqlite")]
public partial class AddCustomerCnpjOrderSnapshotSqlite : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CustomerCnpjSnapshot",
            table: "Orders",
            type: "TEXT",
            maxLength: 18,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "CustomerCnpjAudits",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                OrderId = table.Column<int>(type: "INTEGER", nullable: true),
                Action = table.Column<int>(type: "INTEGER", nullable: false),
                PreviousCnpj = table.Column<string>(type: "TEXT", maxLength: 18, nullable: true),
                NewCnpj = table.Column<string>(type: "TEXT", maxLength: 18, nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ChangedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                ChangedByEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerCnpjAudits", x => x.Id);
                table.ForeignKey(
                    name: "FK_CustomerCnpjAudits_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CustomerCnpjAudits_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CustomerCnpjAudits_CustomerId_ChangedAt",
            table: "CustomerCnpjAudits",
            columns: new[] { "CustomerId", "ChangedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_CustomerCnpjAudits_OrderId_ChangedAt",
            table: "CustomerCnpjAudits",
            columns: new[] { "OrderId", "ChangedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CustomerCnpjAudits");
        migrationBuilder.DropColumn(name: "CustomerCnpjSnapshot", table: "Orders");
    }
}
