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
        var creator = TestDbContextFactory.CreateOrderCreator();
        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, CreatedByUser = creator, Status = OrderStatus.Invoiced },
            new Order { CustomerId = customer.Id, CreatedByUser = creator, Status = OrderStatus.Delivered },
            new Order { CustomerId = customer.Id, CreatedByUser = creator, Status = OrderStatus.Received },
            new Order { CustomerId = customer.Id, CreatedByUser = creator, Status = OrderStatus.Cancelled });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.Equal(2, result.ValidPurchases);
        Assert.False(result.InvoiceCreditEnabled);
        Assert.Equal(0, result.MaximumTermDays);
        Assert.Equal(["PIX", "CASH", "CREDIT_CARD"], result.PaymentMethods.Select(term => term.Code));
    }

    [Fact]
    public async Task Wmc_authorized_condition_beyond_local_fourteen_day_limit_is_available()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await CreateCustomerAsync(db, wmcCode: "5264");
        await AddPaymentTermsAsync(db);
        var creator = TestDbContextFactory.CreateOrderCreator();
        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, CreatedByUser = creator, Status = OrderStatus.Invoiced },
            new Order { CustomerId = customer.Id, CreatedByUser = creator, Status = OrderStatus.Invoiced },
            new Order { CustomerId = customer.Id, CreatedByUser = creator, Status = OrderStatus.Delivered });
        await db.SaveChangesAsync();

        var result = await CreateService(db, reader: new TestWmcReader(new Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition(
            "5264", "5", "5", "21 DIAS", 1, 21, 0, "A", "WMC Firebird mirror", DateTimeOffset.UtcNow))).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.Equal(3, result.ValidPurchases);
        Assert.True(result.InvoiceCreditEnabled);
        Assert.Equal(21, result.MaximumTermDays);
        Assert.Equal(["PIX", "CASH", "CREDIT_CARD", "BOLETO_21D"], result.PaymentMethods.Select(term => term.Code));
    }

    [Fact]
    public async Task Legacy_local_credit_block_does_not_override_active_wmc_authorization()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await CreateCustomerAsync(db, creditBlocked: true);
        await AddPaymentTermsAsync(db);
        var creator = TestDbContextFactory.CreateOrderCreator();
        db.Orders.AddRange(Enumerable.Range(0, 3).Select(_ => new Order { CustomerId = customer.Id, CreatedByUser = creator, Status = OrderStatus.Invoiced }));
        await db.SaveChangesAsync();

        customer.WmcCode = "5264";
        await db.SaveChangesAsync();
        var result = await CreateService(db, reader: new TestWmcReader(new Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 1, 14, 0, "A", "WMC Firebird mirror", DateTimeOffset.UtcNow))).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.True(result.InvoiceCreditEnabled);
        Assert.Contains(result.PaymentMethods, term => term.DaysUntilDue == 14);
    }

    [Fact]
    public async Task Card_on_delivery_is_hidden_and_rejected_when_feature_is_disabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await CreateCustomerAsync(db);
        await AddPaymentTermsAsync(db);
        var term = await db.PaymentTerms.SingleAsync(x => x.Code == "CARD_ON_DELIVERY");

        var service = CreateService(db);
        var result = await service.GetAvailablePaymentOptionsAsync(customer.Id);
        var validation = await service.ValidateAsync(customer.Id, term.Id);

        Assert.DoesNotContain(result.PaymentMethods, x => x.Code == "CARD_ON_DELIVERY");
        Assert.False(validation.IsAllowed);
    }

    [Fact]
    public async Task Card_on_delivery_is_available_when_feature_is_enabled_and_cash_is_preserved()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = await CreateCustomerAsync(db);
        await AddPaymentTermsAsync(db);
        var term = await db.PaymentTerms.SingleAsync(x => x.Code == "CARD_ON_DELIVERY");

        var service = CreateService(db, cardOnDeliveryEnabled: true);
        var result = await service.GetAvailablePaymentOptionsAsync(customer.Id);
        var validation = await service.ValidateAsync(customer.Id, term.Id);

        Assert.Contains(result.PaymentMethods, x => x.Code == "CARD_ON_DELIVERY");
        Assert.Contains(result.PaymentMethods, x => x.Code == "CASH");
        Assert.Contains(result.PaymentMethods, x => x.Code == "CREDIT_CARD");
        Assert.True(validation.IsAllowed);
    }

    private static PaymentEligibilityService CreateService(ApplicationDbContext db, bool cardOnDeliveryEnabled = false, Orofoods.Web.Integrations.Erp.Wmc.IWmcCustomerPaymentConditionReader? reader = null) =>
        new(db, Options.Create(new PaymentEligibilityOptions { CardOnDeliveryEnabled = cardOnDeliveryEnabled }), reader);

    private static async Task<Customer> CreateCustomerAsync(ApplicationDbContext db, bool creditBlocked = false, string? wmcCode = null)
    {
        var customer = new Customer { LegalName = "Payment Test Ltda", TradeName = "Payment Test", Cnpj = Guid.NewGuid().ToString(), CreditBlocked = creditBlocked, WmcCode = wmcCode };
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
            new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega", DaysUntilDue = 0, SortOrder = 4 },
            new PaymentTerm { Code = "BOLETO_7D", Name = "Boleto bancário — 7 dias", DaysUntilDue = 7, SortOrder = 5 },
            new PaymentTerm { Code = "BOLETO_14D", Name = "Boleto bancário — 14 dias", DaysUntilDue = 14, SortOrder = 6 },
            new PaymentTerm { Code = "BOLETO_21D", Name = "Boleto bancário — 21 dias", DaysUntilDue = 21, SortOrder = 7 });
        await db.SaveChangesAsync();
    }

    private sealed class TestWmcReader(Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition condition)
        : Orofoods.Web.Integrations.Erp.Wmc.IWmcCustomerPaymentConditionReader
    {
        public Task<Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition?> GetForCustomerAsync(string customerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition?>(condition);
    }

}
