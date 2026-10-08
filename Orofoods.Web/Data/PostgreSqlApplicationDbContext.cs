using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Data;

public class PostgreSqlApplicationDbContext(DbContextOptions<PostgreSqlApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Order>()
            .Property(x => x.RequestedDeliveryDate)
            .HasColumnType("timestamp without time zone");

        // Keep the PostgreSQL model aligned with the existing schema, which
        // stores application DateTime values without a time zone.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties()
                         .Where(property => property.ClrType == typeof(DateTime)
                             || property.ClrType == typeof(DateTime?)))
            {
                property.SetColumnType("timestamp without time zone");
            }
        }
    }
}
