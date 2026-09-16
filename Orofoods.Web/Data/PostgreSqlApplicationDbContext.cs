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
    }
}
