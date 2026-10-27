using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql
{
    /// <inheritdoc />
    public partial class AddSalesRepresentativeWmcCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WmcCode",
                table: "SalesRepresentatives",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_SalesRepresentatives_WmcCode\" ON \"SalesRepresentatives\" (\"WmcCode\") WHERE \"WmcCode\" IS NOT NULL AND \"WmcCode\" <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_SalesRepresentatives_WmcCode\";");

            migrationBuilder.DropColumn(
                name: "WmcCode",
                table: "SalesRepresentatives");
        }
    }
}
