using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Delivery;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Catalog;

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
    public DbSet<CustomerCnpjAudit> CustomerCnpjAudits => Set<CustomerCnpjAudit>();
    public DbSet<AdminPortalAccessAudit> AdminPortalAccessAudits => Set<AdminPortalAccessAudit>();
    public DbSet<AdminUserChangeAudit> AdminUserChangeAudits => Set<AdminUserChangeAudit>();
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
    public DbSet<WmcOrderExportState> WmcOrderExportStates => Set<WmcOrderExportState>();
    public DbSet<WmcManualExportArtifact> WmcManualExportArtifacts => Set<WmcManualExportArtifact>();
    public DbSet<WmcSyncRun> WmcSyncRuns => Set<WmcSyncRun>();
    public DbSet<WmcProductImportAudit> WmcProductImportAudits => Set<WmcProductImportAudit>();
    public DbSet<WmcProductUnit> WmcProductUnits => Set<WmcProductUnit>();
    public DbSet<WmcInitialLoadReleaseAudit> WmcInitialLoadReleaseAudits => Set<WmcInitialLoadReleaseAudit>();
    public DbSet<WmcProductImportRun> WmcProductImportRuns => Set<WmcProductImportRun>();
    public DbSet<SavedOrder> SavedOrders => Set<SavedOrder>();
    public DbSet<SavedOrderItem> SavedOrderItems => Set<SavedOrderItem>();
    public DbSet<SellerOrderDraft> SellerOrderDrafts => Set<SellerOrderDraft>();
    public DbSet<SellerOrderDraftItem> SellerOrderDraftItems => Set<SellerOrderDraftItem>();
    public DbSet<CommercialActivity> CommercialActivities => Set<CommercialActivity>();
    public DbSet<CrmOpportunity> CrmOpportunities => Set<CrmOpportunity>();
    public DbSet<CrmOpportunityHistory> CrmOpportunityHistories => Set<CrmOpportunityHistory>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<WhatsAppConversation> WhatsAppConversations => Set<WhatsAppConversation>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();
    public DbSet<OrderMinimumSettings> OrderMinimumSettings => Set<OrderMinimumSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AdminPortalAccessAudit>(entity =>
        {
            entity.Property(x => x.AccessType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.AdministratorUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.AdministratorEmail).HasMaxLength(256);
            entity.Property(x => x.TargetName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.HasIndex(x => new { x.AdministratorUserId, x.SelectedAt });
            entity.HasIndex(x => new { x.AccessType, x.TargetId, x.SelectedAt });
        });

        builder.Entity<AdminUserChangeAudit>(entity =>
        {
            entity.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.ActorName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.TargetUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.TargetName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.TargetUserId, x.ChangedAt });
            entity.HasIndex(x => new { x.ActorUserId, x.ChangedAt });
        });

        builder.Entity<ApplicationUser>().Property(x => x.FullName).HasMaxLength(256).IsRequired();

        builder.Entity<OrderMinimumSettings>(entity =>
        {
            entity.ToTable("OrderMinimumSettings", table => table.HasCheckConstraint("CK_OrderMinimumSettings_Singleton", "\"Id\" = 1"));
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.MinimumAmount).HasPrecision(12, 2);
            entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        });

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

        builder.Entity<CustomerCnpjAudit>().HasIndex(x => new { x.CustomerId, x.ChangedAt });
        builder.Entity<CustomerCnpjAudit>().HasIndex(x => new { x.OrderId, x.ChangedAt });
        builder.Entity<CustomerCnpjAudit>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CustomerCnpjAudit>()
            .HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.Entity<SellerOrderDraft>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
            entity.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
            entity.Property(x => x.Notes).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.OwnerUserId, x.CreateRequestKey })
                .IsUnique()
                .HasDatabaseName("IX_SellerOrderDrafts_OwnerUserId_CreateRequestKey");
            entity.HasIndex(x => new { x.OwnerUserId, x.Status, x.UpdatedAt });
            entity.HasIndex(x => new { x.SalesRepresentativeId, x.CustomerId, x.Status, x.UpdatedAt })
                .HasDatabaseName("IX_SellerDrafts_Rep_Customer_Status_Updated");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_SellerOrderDrafts_Status", "\"Status\" IN ('Draft', 'Submitted')");
                table.HasCheckConstraint("CK_SellerOrderDrafts_Version", "\"Version\" > 0");
            });
            entity.HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SalesRepresentative)
                .WithMany()
                .HasForeignKey(x => x.SalesRepresentativeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DeliveryAddress)
                .WithMany()
                .HasForeignKey(x => x.DeliveryAddressId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PaymentTerm)
                .WithMany()
                .HasForeignKey(x => x.PaymentTermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Items)
                .WithOne(x => x.SellerOrderDraft)
                .HasForeignKey(x => x.SellerOrderDraftId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SubmittedOrder)
                .WithOne(x => x.SellerOrderDraft)
                .HasForeignKey<Order>(x => x.SellerOrderDraftId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SellerOrderDraftItem>(entity =>
        {
            entity.HasIndex(x => new { x.SellerOrderDraftId, x.ProductId }).IsUnique();
            entity.Property(x => x.RequestedDiscountAmount).HasPrecision(12, 2);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_SellerOrderDraftItems_Quantity", "\"Quantity\" > 0");
                table.HasCheckConstraint("CK_SellerOrderDraftItems_RequestedDiscountAmount", "\"RequestedDiscountAmount\" >= 0");
            });
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Order>()
            .HasIndex(x => x.SellerOrderDraftId)
            .HasDatabaseName("IX_Orders_SellerOrderDraftId")
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

        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            builder.Entity<Order>()
                .HasOne(x => x.SalesRepresentative)
                .WithMany()
                .HasForeignKey(x => x.SalesRepresentativeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
        else
        {
            // SQLite cannot add this FK to the existing Orders table without rebuilding it.
            builder.Entity<Order>().Ignore(x => x.SalesRepresentative);
        }

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

        builder.Entity<WmcExportAudit>()
            .HasIndex(x => new { x.OrderId, x.AttemptId })
            .IsUnique();

        builder.Entity<WmcOrderExportState>()
            .HasKey(x => x.OrderId);
        builder.Entity<WmcOrderExportState>()
            .Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Entity<WmcOrderExportState>()
            .Property(x => x.Version)
            .IsConcurrencyToken();
        builder.Entity<WmcOrderExportState>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_WmcOrderExportStates_Status",
                "\"Status\" IN ('Pending', 'Processing', 'Exported', 'Rejected', 'Imported', 'Error')"));
        builder.Entity<WmcOrderExportState>()
            .HasOne(x => x.Order)
            .WithOne(x => x.WmcOrderExportState)
            .HasForeignKey<WmcOrderExportState>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<WmcOrderExportState>()
            .HasOne(x => x.CurrentArtifact)
            .WithMany()
            .HasForeignKey(x => x.CurrentArtifactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<WmcManualExportArtifact>()
            .HasIndex(x => x.AttemptId)
            .IsUnique();
        builder.Entity<WmcManualExportArtifact>()
            .HasIndex(x => new { x.OrderId, x.CreatedAt });
        builder.Entity<WmcManualExportArtifact>()
            .HasOne(x => x.Order)
            .WithMany(x => x.WmcManualExportArtifacts)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<WmcManualExportArtifact>()
            .HasOne(x => x.SupersedesArtifact)
            .WithMany()
            .HasForeignKey(x => x.SupersedesArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Customer>().Property(x => x.MinimumOrder).HasPrecision(12, 2);
        builder.Entity<Customer>().Property(x => x.CreditLimit).HasPrecision(12, 2);
        builder.Entity<Customer>().Property(x => x.CreditUsed).HasPrecision(12, 2);
        builder.Entity<Product>().Property(x => x.Weight).HasPrecision(12, 3);
        builder.Entity<Product>().Property(x => x.BasePrice).HasPrecision(12, 2);
        builder.Entity<Product>().Property(x => x.PromotionalPrice).HasPrecision(12, 2);
        builder.Entity<Product>().Property(x => x.WmcPackageQuantity).HasPrecision(12, 3);
        builder.Entity<Product>().Property(x => x.WmcConversionQuantity).HasPrecision(12, 3);
        builder.Entity<WmcSyncRun>().Property(x => x.Status).HasMaxLength(20);
        builder.Entity<WmcSyncRun>().Property(x => x.FailedStage).HasMaxLength(40);
        builder.Entity<WmcSyncRun>().Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Entity<WmcSyncRun>().HasIndex(x => new { x.Status, x.StartedAt });
        builder.Entity<WmcProductImportAudit>().Property(x => x.Status).HasMaxLength(24);
        builder.Entity<WmcProductImportAudit>().Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Entity<WmcProductImportAudit>().Property(x => x.ResultsJson).HasColumnType("TEXT");
        builder.Entity<WmcProductImportAudit>().HasIndex(x => x.CompletedAt);
        builder.Entity<WmcProductUnit>(entity =>
        {
            entity.HasKey(unit => new { unit.ProductId, unit.UnitCode });
            entity.Property(unit => unit.UnitCode).HasMaxLength(20);
            entity.Property(unit => unit.Description).HasMaxLength(120);
            entity.Property(unit => unit.Factor).HasPrecision(18, 6);
            entity.Property(unit => unit.Barcode).HasMaxLength(80);
            entity.HasOne(unit => unit.Product)
                .WithMany(product => product.WmcUnits)
                .HasForeignKey(unit => unit.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WmcProductImportRun>(entity =>
        {
            entity.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ImportedProductCodes).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.SkippedProductCodes).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ErrorSummary).HasMaxLength(2000);
            entity.HasIndex(x => new { x.BrandCode, x.StartedAtUtc });
        });
        builder.Entity<WmcInitialLoadReleaseAudit>().HasKey(x => x.Id);
        builder.Entity<WmcInitialLoadReleaseAudit>().Property(x => x.WmcCode).HasMaxLength(30).IsRequired();
        builder.Entity<WmcInitialLoadReleaseAudit>().Property(x => x.AdminUserId).HasMaxLength(450).IsRequired();
        builder.Entity<WmcInitialLoadReleaseAudit>().Property(x => x.Reason).HasMaxLength(300).IsRequired();
        builder.Entity<WmcInitialLoadReleaseAudit>().HasIndex(x => new { x.ProductId, x.ReleasedAtUtc });
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
