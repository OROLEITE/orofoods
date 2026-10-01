using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

[DbContext(typeof(PostgreSqlApplicationDbContext))]
[Migration("20260929120000_AddCardOnDeliveryPaymentTerm")]
public sealed class AddCardOnDeliveryPaymentTerm : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO "PaymentTerms" ("Code", "Name", "DaysUntilDue", "SortOrder", "IsActive")
            SELECT 'CARD_ON_DELIVERY', 'Cartão na entrega', 0, 4, TRUE
            WHERE NOT EXISTS (SELECT 1 FROM "PaymentTerms" WHERE "Code" = 'CARD_ON_DELIVERY');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "PaymentTerms" AS term
            WHERE term."Code" = 'CARD_ON_DELIVERY'
              AND NOT EXISTS (SELECT 1 FROM "Orders" AS orders WHERE orders."PaymentTermId" = term."Id")
              AND NOT EXISTS (SELECT 1 FROM "CustomerPaymentTerms" AS customer_terms WHERE customer_terms."PaymentTermId" = term."Id");
            """);
    }
}
