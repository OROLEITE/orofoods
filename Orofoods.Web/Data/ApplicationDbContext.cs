using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models;

namespace Orofoods.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CustomerPrice> CustomerPrices => Set<CustomerPrice>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Product>().HasOne(x => x.SubstituteProduct).WithMany().HasForeignKey(x => x.SubstituteProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CustomerPrice>().HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique();
        builder.Entity<Customer>().Property(x => x.MinimumOrder).HasPrecision(12, 2);
        builder.Entity<Customer>().Property(x => x.CreditLimit).HasPrecision(12, 2);
        builder.Entity<Customer>().Property(x => x.CreditUsed).HasPrecision(12, 2);
        builder.Entity<Product>().Property(x => x.BasePrice).HasPrecision(12, 2);
        builder.Entity<CustomerPrice>().Property(x => x.Price).HasPrecision(12, 2);
        builder.Entity<Order>().Property(x => x.Total).HasPrecision(12, 2);
        builder.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
    }
}
