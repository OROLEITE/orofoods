using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class WmcPaymentEligibilityTests
{
    [Fact]
    public async Task Active_primary_wmc_condition_authorizes_only_its_matching_credit_term()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = Guid.NewGuid().ToString(), WmcCode = "5264", CreditBlocked = true };
        db.AddRange(customer,
            new PaymentTerm { Code = "PIX", Name = "Pix", DaysUntilDue = 0, SortOrder = 1 },
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0, SortOrder = 2 },
            new PaymentTerm { Code = "CREDIT_CARD", Name = "Cartão", DaysUntilDue = 0, SortOrder = 3 },
            new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega", DaysUntilDue = 0, SortOrder = 4 },
            new PaymentTerm { Code = "BOLETO_7D", Name = "7 dias", DaysUntilDue = 7, SortOrder = 5 },
            new PaymentTerm { Code = "BOLETO_14D", Name = "14 dias", DaysUntilDue = 14, SortOrder = 6 },
            new PaymentTerm { Code = "BOLETO_21D", Name = "21 dias", DaysUntilDue = 21, SortOrder = 7 });
        await db.SaveChangesAsync();
        var reader = new FakeWmcPaymentConditionReader(new WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 1, 14, 0, "A", "WMC Firebird mirror", DateTimeOffset.UtcNow));

        var result = await CreateService(db, reader).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.Equal(["PIX", "CASH", "CREDIT_CARD", "BOLETO_14D"], result.PaymentMethods.Select(term => term.Code));
        Assert.Equal("3", result.WmcPaymentCondition?.ConditionCode);
        Assert.Equal("14 DIAS", result.WmcPaymentCondition?.Description);
        Assert.Equal("WMC Firebird mirror", result.WmcPaymentCondition?.Source);
        Assert.NotNull(result.WmcPaymentCondition?.ReadAtUtc);
    }

    [Theory]
    [InlineData("I")]
    [InlineData("?")]
    public async Task Inactive_or_unknown_wmc_condition_never_authorizes_credit(string situation)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = Guid.NewGuid().ToString(), WmcCode = "5264" };
        db.AddRange(customer,
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0 },
            new PaymentTerm { Code = "BOLETO_14D", Name = "14 dias", DaysUntilDue = 14 });
        await db.SaveChangesAsync();
        var reader = new FakeWmcPaymentConditionReader(new WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 1, 14, 0, situation, "WMC Firebird mirror", DateTimeOffset.UtcNow));

        var result = await CreateService(db, reader).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.DoesNotContain(result.PaymentMethods, term => term.DaysUntilDue > 0);
        Assert.Contains(result.PaymentMethods, term => term.Code == "CASH");
    }

    [Fact]
    public async Task Missing_wmc_condition_fails_closed_for_credit_but_keeps_immediate_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = Guid.NewGuid().ToString(), WmcCode = "5264" };
        db.AddRange(customer,
            new PaymentTerm { Code = "PIX", Name = "Pix", DaysUntilDue = 0 },
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0 },
            new PaymentTerm { Code = "CREDIT_CARD", Name = "Cartão", DaysUntilDue = 0 },
            new PaymentTerm { Code = "BOLETO_14D", Name = "14 dias", DaysUntilDue = 14 });
        await db.SaveChangesAsync();

        var result = await CreateService(db, new FakeWmcPaymentConditionReader(null)).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.Equal(["PIX", "CASH", "CREDIT_CARD"], result.PaymentMethods.Select(term => term.Code));
        Assert.Null(result.WmcPaymentCondition);
    }

    [Fact]
    public async Task Valid_wmc_condition_without_matching_local_payment_term_fails_closed()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = Guid.NewGuid().ToString(), WmcCode = "5264" };
        db.AddRange(customer,
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0 },
            new PaymentTerm { Code = "BOLETO_7D", Name = "7 dias", DaysUntilDue = 7 });
        await db.SaveChangesAsync();
        var reader = new FakeWmcPaymentConditionReader(new WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 1, 14, 0, "A", "WMC Firebird mirror", DateTimeOffset.UtcNow));

        var result = await CreateService(db, reader).GetAvailablePaymentOptionsAsync(customer.Id);
        var selectedLocalTerm = await db.PaymentTerms.SingleAsync(term => term.Code == "BOLETO_7D");
        var validation = await CreateService(db, reader).ValidateAsync(customer.Id, selectedLocalTerm.Id);

        Assert.False(result.InvoiceCreditEnabled);
        Assert.DoesNotContain(result.PaymentMethods, term => term.DaysUntilDue > 0);
        Assert.False(validation.IsAllowed);
    }

    [Theory]
    [InlineData("BOLETO_14D", false)]
    [InlineData("CREDIT_CARD", true)]
    public async Task Wmc_condition_requires_an_active_txt_compatible_local_term(string code, bool active)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = Guid.NewGuid().ToString(), WmcCode = "5264" };
        db.AddRange(customer,
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0 },
            new PaymentTerm { Code = code, Name = "Condição 14", DaysUntilDue = 14, IsActive = active });
        await db.SaveChangesAsync();
        var reader = new FakeWmcPaymentConditionReader(new WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 1, 14, 0, "A", "WMC Firebird mirror", DateTimeOffset.UtcNow));

        var result = await CreateService(db, reader).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.DoesNotContain(result.PaymentMethods, term => term.DaysUntilDue > 0);
        Assert.False(result.InvoiceCreditEnabled);
    }

    [Fact]
    public async Task Wmc_read_failure_fails_closed_for_credit_but_keeps_immediate_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = Guid.NewGuid().ToString(), WmcCode = "5264" };
        db.AddRange(customer,
            new PaymentTerm { Code = "PIX", Name = "Pix", DaysUntilDue = 0 },
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0 },
            new PaymentTerm { Code = "BOLETO_14D", Name = "14 dias", DaysUntilDue = 14 });
        await db.SaveChangesAsync();

        var result = await CreateService(db, new FailingWmcPaymentConditionReader()).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.Equal(["PIX", "CASH"], result.PaymentMethods.Select(term => term.Code));
        Assert.Null(result.WmcPaymentCondition);
    }

    [Fact]
    public async Task Multiple_installments_are_not_offered_when_wmc_txt_contract_has_one_payment_record()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = Guid.NewGuid().ToString(), WmcCode = "5264" };
        db.AddRange(customer,
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0 },
            new PaymentTerm { Code = "BOLETO_14D", Name = "14 dias", DaysUntilDue = 14 });
        await db.SaveChangesAsync();
        var reader = new FakeWmcPaymentConditionReader(new WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 2, 14, 14, "A", "WMC Firebird mirror", DateTimeOffset.UtcNow));

        var result = await CreateService(db, reader).GetAvailablePaymentOptionsAsync(customer.Id);

        Assert.DoesNotContain(result.PaymentMethods, term => term.DaysUntilDue > 0);
        Assert.Contains(result.PaymentMethods, term => term.Code == "CASH");
    }

    private static PaymentEligibilityService CreateService(ApplicationDbContext db, IWmcCustomerPaymentConditionReader reader) =>
        new(db, Options.Create(new PaymentEligibilityOptions()), reader);

    private sealed class FakeWmcPaymentConditionReader(WmcCustomerPaymentCondition? condition) : IWmcCustomerPaymentConditionReader
    {
        public Task<WmcCustomerPaymentCondition?> GetForCustomerAsync(string customerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(condition);
    }

    private sealed class FailingWmcPaymentConditionReader : IWmcCustomerPaymentConditionReader
    {
        public Task<WmcCustomerPaymentCondition?> GetForCustomerAsync(string customerCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Firebird indisponível.");
    }
}
