using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Delivery;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.Data;

public class ApplicationDbContext(DbContextOptions options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<SalesRepresentative> SalesRepresentatives => Set<SalesRepresentative>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<FavoriteProduct> FavoriteProducts => Set<FavoriteProduct>();
    public DbSet<PriceTable> PriceTables => Set<PriceTable>();
    public DbSet<PriceTableItem> PriceTableItems => Set<PriceTableItem>();
    public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();
    public DbSet<CustomerPaymentTerm> CustomerPaymentTerms => Set<CustomerPaymentTerm>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PointPaymentAuditEvent> PointPaymentAuditEvents => Set<PointPaymentAuditEvent>();
    public DbSet<PaymentTerminal> PaymentTerminals => Set<PaymentTerminal>();
    public DbSet<DriverPaymentTerminalAssignment> DriverPaymentTerminalAssignments => Set<DriverPaymentTerminalAssignment>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<ProductInventory> ProductInventories => Set<ProductInventory>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<WmcExportAudit> WmcExportAudits => Set<WmcExportAudit>();
    public DbSet<SavedOrder> SavedOrders => Set<SavedOrder>();
    public DbSet<SavedOrderItem> SavedOrderItems => Set<SavedOrderItem>();
    public DbSet<CommercialActivity> CommercialActivities => Set<CommercialActivity>();
    public DbSet<CrmOpportunity> CrmOpportunities => Set<CrmOpportunity>();
    public DbSet<CrmOpportunityHistory> CrmOpportunityHistories => Set<CrmOpportunityHistory>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<WhatsAppConversation> WhatsAppConversations => Set<WhatsAppConversation>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Preserve the established PostgreSQL Identity schema widths across EF provider upgrades.
        builder.Entity<IdentityUserLogin<string>>().Property(x => x.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<string>>().Property(x => x.ProviderKey).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(x => x.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(x => x.Name).HasMaxLength(128);

        builder.Entity<ApplicationUser>()
            .HasOne(x => x.Customer)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ApplicationUser>()
            .HasOne(x => x.SalesRepresentative)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.SalesRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Customer>()
            .HasIndex(x => x.Cnpj)
            .IsUnique();

        builder.Entity<Customer>()
            .HasOne(x => x.PriceTable)
            .WithMany(x => x.Customers)
            .HasForeignKey(x => x.PriceTableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Customer>()
            .HasOne(x => x.SalesRepresentative)
            .WithMany(x => x.Customers)
            .HasForeignKey(x => x.SalesRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Customer>()
            .HasOne(x => x.InternalSalesUser)
            .WithMany()
            .HasForeignKey(x => x.InternalSalesUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Customer>()
            .HasIndex(x => x.InternalSalesUserId);

        builder.Entity<ProductCategory>()
            .HasIndex(x => x.Slug)
            .IsUnique();

        builder.Entity<Product>()
            .HasOne(x => x.SubstituteProduct)
            .WithMany()
            .HasForeignKey(x => x.SubstituteProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PriceTableItem>()
            .HasIndex(x => new { x.PriceTableId, x.ProductId })
            .IsUnique();

        builder.Entity<CustomerPaymentTerm>()
            .HasIndex(x => new { x.CustomerId, x.PaymentTermId })
            .IsUnique();

        builder.Entity<FavoriteProduct>()
            .HasIndex(x => new { x.CustomerId, x.ProductId })
            .IsUnique();

        builder.Entity<SavedOrderItem>()
            .HasIndex(x => new { x.SavedOrderId, x.ProductId })
            .IsUnique();

        builder.Entity<CommercialActivity>()
            .HasIndex(x => new { x.ScheduledAt, x.Status });

        builder.Entity<CommercialActivity>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CommercialActivity>()
            .HasOne(x => x.SalesRepresentative)
            .WithMany()
            .HasForeignKey(x => x.SalesRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CommercialActivity>()
            .HasOne(x => x.AssignedUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<CommercialActivity>()
            .HasIndex(x => x.AssignedUserId);

        builder.Entity<CrmOpportunity>().HasIndex(x => x.CustomerId);
        builder.Entity<CrmOpportunity>().HasIndex(x => x.AssignedUserId);
        builder.Entity<CrmOpportunity>().HasIndex(x => x.Stage);
        builder.Entity<CrmOpportunity>().HasIndex(x => x.ExpectedCloseAt);
        builder.Entity<CrmOpportunity>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CrmOpportunity>().HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<CrmOpportunity>().HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<CrmOpportunity>().HasOne(x => x.RelatedOrder).WithMany().HasForeignKey(x => x.RelatedOrderId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<CrmOpportunityHistory>().HasIndex(x => new { x.OpportunityId, x.OccurredAt });
        builder.Entity<CrmOpportunityHistory>().HasOne(x => x.Opportunity).WithMany(x => x.History).HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CrmOpportunityHistory>().HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<UserNotification>().HasIndex(x => new { x.UserId, x.ReadAt });
        builder.Entity<UserNotification>().HasIndex(x => x.CreatedAt);
        builder.Entity<UserNotification>().HasIndex(x => new { x.UserId, x.DeduplicationKey }).IsUnique();
        builder.Entity<UserNotification>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<UserNotification>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<UserNotification>().HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<UserNotification>().HasOne(x => x.Activity).WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<WhatsAppConversation>().HasIndex(x => x.PhoneNumber);
        builder.Entity<WhatsAppConversation>().HasIndex(x => x.CustomerId);
        builder.Entity<WhatsAppConversation>().HasIndex(x => x.AssignedUserId);
        builder.Entity<WhatsAppConversation>().HasIndex(x => x.LastMessageAt);
        builder.Entity<WhatsAppConversation>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<WhatsAppConversation>().HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<WhatsAppMessage>().HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.Entity<WhatsAppMessage>().HasIndex(x => new { x.ConversationId, x.CreatedAt });
        builder.Entity<WhatsAppMessage>().HasIndex(x => x.Status);
        builder.Entity<WhatsAppMessage>().HasIndex(x => x.MediaId);
        builder.Entity<WhatsAppMessage>().HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Driver>().HasIndex(x => x.Name);
        builder.Entity<PaymentTerminal>().HasIndex(x => new { x.Provider, x.DeviceId }).IsUnique().HasFilter("\"DeviceId\" IS NOT NULL");
        builder.Entity<DriverPaymentTerminalAssignment>().HasIndex(x => x.PaymentTerminalId).IsUnique().HasFilter("\"EndedAt\" IS NULL");
        builder.Entity<DriverPaymentTerminalAssignment>()
            .HasOne(x => x.Driver)
            .WithMany(x => x.PaymentTerminalAssignments)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DriverPaymentTerminalAssignment>()
            .HasOne(x => x.PaymentTerminal)
            .WithMany(x => x.DriverAssignments)
            .HasForeignKey(x => x.PaymentTerminalId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Payment>()
            .HasOne(x => x.DriverPaymentTerminalAssignment)
            .WithMany()
            .HasForeignKey(x => x.DriverPaymentTerminalAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Payment>().HasIndex(x => x.DriverPaymentTerminalAssignmentId);

        builder.Entity<PointPaymentAuditEvent>().HasIndex(x => new { x.OrderId, x.OccurredAt });
        builder.Entity<PointPaymentAuditEvent>().HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PointPaymentAuditEvent>().HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PointPaymentAuditEvent>().HasOne<Driver>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<PointPaymentAuditEvent>().HasOne<PaymentTerminal>().WithMany().HasForeignKey(x => x.PaymentTerminalId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<PointPaymentAuditEvent>().HasOne<DriverPaymentTerminalAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<PointPaymentAuditEvent>().HasOne(x => x.AdminUser).WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.SetNull);

        builder.Entity<OrderStatusHistory>()
            .HasIndex(x => new { x.OrderId, x.ChangedAt });

        builder.Entity<Order>()
            .HasIndex(x => new { x.CustomerId, x.CheckoutAttemptKey })
            .HasDatabaseName("IX_Orders_CustomerId_CheckoutAttemptKey")
            .IsUnique();

        builder.Entity<OrderStatusHistory>()
            .HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>()
            .HasIndex(x => x.OrderId);

        builder.Entity<Payment>()
            .HasIndex(x => x.IdempotencyKey)
            .IsUnique();

        builder.Entity<Payment>()
            .HasIndex(x => x.GatewayOrderId);

        builder.Entity<Payment>()
            .HasIndex(x => x.GatewayPaymentId);

        builder.Entity<Payment>()
            .HasOne(x => x.Order)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Payment>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProductInventory>()
            .HasIndex(x => x.ProductId)
            .IsUnique();

        builder.Entity<ProductInventory>()
            .HasOne(x => x.Product)
            .WithOne()
            .HasForeignKey<ProductInventory>(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<InventoryReservation>()
            .HasIndex(x => new { x.OrderId, x.ProductId })
            .IsUnique();

        builder.Entity<InventoryReservation>()
            .HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<InventoryReservation>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InventoryAdjustment>()
            .HasOne(x => x.ProductInventory)
            .WithMany(x => x.Adjustments)
            .HasForeignKey(x => x.ProductInventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<InventoryAdjustment>()
            .HasIndex(x => new { x.ProductInventoryId, x.AdjustedAt });

        builder.Entity<WmcExportAudit>()
            .HasIndex(x => new { x.OrderId, x.ExportedAt });


        builder.Entity<Customer>().Property(x => x.MinimumOrder).HasPrecision(12, 2);
        builder.Entity<Customer>().Property(x => x.CreditLimit).HasPrecision(12, 2);
        builder.Entity<Customer>().Property(x => x.CreditUsed).HasPrecision(12, 2);
        builder.Entity<Product>().Property(x => x.Weight).HasPrecision(12, 3);
        builder.Entity<Product>().Property(x => x.BasePrice).HasPrecision(12, 2);
        builder.Entity<Product>().Property(x => x.PromotionalPrice).HasPrecision(12, 2);
        builder.Entity<PriceTableItem>().Property(x => x.Price).HasPrecision(12, 2);
        builder.Entity<PriceTableItem>().Property(x => x.PromotionalPrice).HasPrecision(12, 2);
        builder.Entity<Order>().Property(x => x.Subtotal).HasPrecision(12, 2);
        builder.Entity<Order>().Property(x => x.Freight).HasPrecision(12, 2);
        builder.Entity<Order>().Property(x => x.Total).HasPrecision(12, 2);
        builder.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        builder.Entity<OrderItem>().Property(x => x.Subtotal).HasPrecision(12, 2);
        builder.Entity<Payment>().Property(x => x.Amount).HasPrecision(12, 2);
    }
}
