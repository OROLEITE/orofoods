using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021160000_AddWmcOrderReconciliationMetadata")]
public partial class AddWmcOrderReconciliationMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WmcSalesRepresentativeCode",
            table: "WmcExportAudits",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WmcDestination",
            table: "WmcExportAudits",
            type: "TEXT",
            maxLength: 2000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WmcSalesRepresentativeCode", table: "WmcExportAudits");
        migrationBuilder.DropColumn(name: "WmcDestination", table: "WmcExportAudits");
    }
}
