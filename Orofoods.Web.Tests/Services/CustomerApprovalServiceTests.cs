using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CustomerApprovalServiceTests
{
    [Fact]
    public async Task Correcting_cnpj_saves_normalized_value_and_audit_atomically()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Cliente Ltda",
            TradeName = "Cliente",
            Cnpj = "03.932.292/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var result = await new CustomerApprovalService(db).ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = customer.Id,
            Cnpj = "00.000.000/E08G-12",
            CnpjChangeReason = "Correção documental conferida",
            ChangedByUserId = "admin-user",
            ChangedByEmail = "admin@orofoods.local"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("00.000.000/E08G-12", (await db.Customers.SingleAsync(x => x.Id == customer.Id)).Cnpj);
        var audit = await db.CustomerCnpjAudits.SingleAsync();
        Assert.Equal(customer.Id, audit.CustomerId);
        Assert.Equal("03.932.292/0001-99", audit.PreviousCnpj);
        Assert.Equal("00.000.000/E08G-12", audit.NewCnpj);
        Assert.Equal("Correção documental conferida", audit.Reason);
        Assert.Equal("admin-user", audit.ChangedByUserId);
        Assert.Equal("admin@orofoods.local", audit.ChangedByEmail);
        Assert.NotEqual(default, audit.ChangedAt);
    }

    [Fact]
    public async Task Invalid_cnpj_is_rejected_without_changing_customer_or_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "03.932.292/0001-99" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var result = await new CustomerApprovalService(db).ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = customer.Id,
            Cnpj = "00.000.000/E08G-13",
            CnpjChangeReason = "Correção documental conferida",
            ChangedByUserId = "admin-user"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(nameof(CustomerApprovalRequest.Cnpj), result.ErrorField);
        Assert.Equal("03.932.292/0001-99", (await db.Customers.SingleAsync(x => x.Id == customer.Id)).Cnpj);
        Assert.Empty(await db.CustomerCnpjAudits.ToListAsync());
    }

    [Fact]
    public async Task Duplicate_cnpj_is_detected_after_normalization()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "03.932.292/0001-99" };
        var duplicate = new Customer { LegalName = "Outro", TradeName = "Outro", Cnpj = "00000000E08G12" };
        db.Customers.AddRange(customer, duplicate);
        await db.SaveChangesAsync();

        var result = await new CustomerApprovalService(db).ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = customer.Id,
            Cnpj = "00.000.000/E08G-12",
            CnpjChangeReason = "Correção documental conferida",
            ChangedByUserId = "admin-user"
        });

        Assert.False(result.Succeeded);
        Assert.Contains("já está cadastrado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("03.932.292/0001-99", (await db.Customers.SingleAsync(x => x.Id == customer.Id)).Cnpj);
        Assert.Empty(await db.CustomerCnpjAudits.ToListAsync());
    }

    [Fact]
    public async Task Equivalent_cnpj_format_does_not_create_a_false_correction_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "00.000.000/E08G-12" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var result = await new CustomerApprovalService(db).ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = customer.Id,
            Cnpj = "00000000e08g12",
        });

        Assert.True(result.Succeeded);
        Assert.Equal("00.000.000/E08G-12", (await db.Customers.SingleAsync(x => x.Id == customer.Id)).Cnpj);
        Assert.Empty(await db.CustomerCnpjAudits.ToListAsync());
    }

    [Fact]
    public async Task Unchanged_legacy_invalid_cnpj_does_not_block_other_admin_updates()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = " bad-cnpj " };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var result = await new CustomerApprovalService(db).ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = customer.Id,
            Cnpj = "bad-cnpj",
            Status = CustomerStatus.Blocked
        });

        Assert.True(result.Succeeded);
        var updated = await db.Customers.SingleAsync(x => x.Id == customer.Id);
        Assert.Equal(" bad-cnpj ", updated.Cnpj);
        Assert.Equal(CustomerStatus.Blocked, updated.Status);
        Assert.Empty(await db.CustomerCnpjAudits.ToListAsync());
    }

    [Fact]
    public async Task Approving_customer_assigns_commercial_configuration()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var paymentTerm = new PaymentTerm { Name = "14 dias", SortOrder = 1, IsActive = true };
        var priceTable = new PriceTable { Name = "Tabela Hamburgueria", IsActive = true };
        var salesRepresentative = new SalesRepresentative { Name = "Consultor", Email = "consultor@orofoods.local", Phone = "(19) 99999-0000", IsActive = true };
        var internalUser = new Orofoods.Web.Models.Identity.ApplicationUser { Id = "internal-user", UserName = "jefferson", Email = "jefferson@orofoods.local", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Pending,
            IsActive = true
        };

        db.AddRange(paymentTerm, priceTable, salesRepresentative, internalUser, customer);
        await db.SaveChangesAsync();

        var sut = new CustomerApprovalService(db);
        await sut.ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = customer.Id,
            Status = CustomerStatus.Approved,
            MinimumOrder = 300m,
            CreditLimit = 5000m,
            PriceTableId = priceTable.Id,
            SalesRepresentativeId = salesRepresentative.Id,
            InternalSalesUserId = internalUser.Id,
            WmcCode = "107072",
        });

        var updatedCustomer = await db.Customers
            .Include(x => x.CustomerPaymentTerms)
            .SingleAsync(x => x.Id == customer.Id);

        Assert.Equal(CustomerStatus.Approved, updatedCustomer.Status);
        Assert.NotNull(updatedCustomer.ApprovedAt);
        Assert.Equal(300m, updatedCustomer.MinimumOrder);
        Assert.Equal(5000m, updatedCustomer.CreditLimit);
        Assert.Equal(priceTable.Id, updatedCustomer.PriceTableId);
        Assert.Equal(salesRepresentative.Id, updatedCustomer.SalesRepresentativeId);
        Assert.Equal(internalUser.Id, updatedCustomer.InternalSalesUserId);
        Assert.Equal("107072", updatedCustomer.WmcCode);
        Assert.Empty(updatedCustomer.CustomerPaymentTerms);
    }
}
