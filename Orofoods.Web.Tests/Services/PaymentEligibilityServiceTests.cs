using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class PaymentEligibilityServiceTests
{
    [Fact]
    public async Task Customers_with_fewer_than_three_valid_purchases_only_receive_immediate_payment_options()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await CreateCustomerAsync(db);
        await AddPaymentTermsAsync(db);
        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Invoiced },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Delivered },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Received },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Cancelled });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.Equal(2, result.ValidPurchases);
        Assert.False(result.InvoiceCreditEnabled);
        Assert.Equal(0, result.MaximumTermDays);
        Assert.Equal(["PIX", "CASH", "CREDIT_CARD"], result.PaymentMethods.Select(term => term.Code));
    }

    [Fact]
    public async Task Third_valid_purchase_releases_only_boleto_terms_up_to_fourteen_days()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await CreateCustomerAsync(db);
        await AddPaymentTermsAsync(db);
        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Invoiced },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Invoiced },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Delivered });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.Equal(3, result.ValidPurchases);
        Assert.True(result.InvoiceCreditEnabled);
        Assert.Equal(14, result.MaximumTermDays);
        Assert.Equal(["PIX", "CASH", "CREDIT_CARD", "BOLETO_7D", "BOLETO_14D"], result.PaymentMethods.Select(term => term.Code));
    }

    [Fact]
    public async Task Credit_block_overrides_purchase_history()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await CreateCustomerAsync(db, creditBlocked: true);
        await AddPaymentTermsAsync(db);
        db.Orders.AddRange(Enumerable.Range(0, 3).Select(_ => new Order { CustomerId = customer.Id, Status = OrderStatus.Invoiced }));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.False(result.InvoiceCreditEnabled);
        Assert.Equal(0, result.MaximumTermDays);
        Assert.DoesNotContain(result.PaymentMethods, term => term.DaysUntilDue > 0);
    }

    private static PaymentEligibilityService CreateService(ApplicationDbContext db) =>
        new(db, Options.Create(new PaymentEligibilityOptions()));

    private static async Task<Customer> CreateCustomerAsync(ApplicationDbContext db, bool creditBlocked = false)
    {
        var customer = new Customer { LegalName = "Payment Test Ltda", TradeName = "Payment Test", Cnpj = Guid.NewGuid().ToString(), CreditBlocked = creditBlocked };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static async Task AddPaymentTermsAsync(ApplicationDbContext db)
    {
        db.PaymentTerms.AddRange(
            new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, SortOrder = 1 },
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0, SortOrder = 2 },
            new PaymentTerm { Code = "CREDIT_CARD", Name = "Cartão de crédito", DaysUntilDue = 0, SortOrder = 3 },
            new PaymentTerm { Code = "BOLETO_7D", Name = "Boleto bancário — 7 dias", DaysUntilDue = 7, SortOrder = 4 },
            new PaymentTerm { Code = "BOLETO_14D", Name = "Boleto bancário — 14 dias", DaysUntilDue = 14, SortOrder = 5 },
            new PaymentTerm { Code = "BOLETO_21D", Name = "Boleto bancário — 21 dias", DaysUntilDue = 21, SortOrder = 6 });
        await db.SaveChangesAsync();
    }
}