using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class AdminCustomerPaymentTermsPersistenceTests
{
    [Fact]
    public async Task Admin_displays_wmc_condition_without_creating_or_editing_local_payment_authorizations()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Orofoods Ltda",
            TradeName = "Orofoods",
            Cnpj = "12.345.678/0001-99",
            WmcCode = "5264",
            Status = CustomerStatus.Approved,
            IsActive = true
        };
        var terms = new[]
        {
            new PaymentTerm { Code = "CASH", Name = "À vista", DaysUntilDue = 0, SortOrder = 1 },
            new PaymentTerm { Code = "BOLETO_7D", Name = "7 dias", DaysUntilDue = 7, SortOrder = 2 },
            new PaymentTerm { Code = "BOLETO_14D", Name = "14 dias", DaysUntilDue = 14, SortOrder = 3 },
            new PaymentTerm { Code = "CREDIT_CARD", Name = "Cartão de crédito", DaysUntilDue = 0, SortOrder = 4 },
            new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega", DaysUntilDue = 0, SortOrder = 5 }
        };
        db.Add(customer);
        db.AddRange(terms);
        await db.SaveChangesAsync();

        var postResult = await CreateController(db).Edit(new AdminCustomerApprovalViewModel
        {
            Id = customer.Id,
            Cnpj = customer.Cnpj,
            WmcCode = customer.WmcCode,
            Status = customer.Status,
        });

        Assert.IsType<RedirectToActionResult>(postResult);
        Assert.Empty(await db.CustomerPaymentTerms.AsNoTracking().Where(link => link.CustomerId == customer.Id).ToListAsync());

        var reader = new FixedWmcPaymentConditionReader(new WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 1, 14, 0, "A", "WMC Firebird mirror", DateTimeOffset.Parse("2026-10-20T12:00:00Z")));
        var getResult = Assert.IsType<ViewResult>(await CreateController(db, reader).Edit(customer.Id));
        var model = Assert.IsType<AdminCustomerApprovalViewModel>(getResult.Model);
        Assert.Contains("14 DIAS", model.WmcPaymentConditionDescription, StringComparison.Ordinal);
        Assert.True(model.WmcPaymentConditionEligible);
        Assert.Contains("elegível para prazo", model.WmcPaymentConditionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("WMC Firebird mirror", model.WmcPaymentConditionSource);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T12:00:00Z"), model.WmcPaymentConditionReadAtUtc);

        var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Areas/Admin/Views/Customers/Edit.cshtml"));
        var view = await File.ReadAllTextAsync(viewPath);
        Assert.DoesNotContain("name=\"PaymentTermIds\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("CreditOverrideEnabled", view, StringComparison.Ordinal);
        Assert.DoesNotContain("MaximumPaymentTermDays", view, StringComparison.Ordinal);
        Assert.Contains("WmcPaymentConditionSource", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_distinguishes_a_wmc_condition_found_from_one_eligible_for_credit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Orofoods Ltda", TradeName = "Orofoods", Cnpj = "12.345.678/0001-99",
            WmcCode = "5264", Status = CustomerStatus.Approved, IsActive = true
        };
        db.Add(customer);
        db.Add(new PaymentTerm { Code = "BOLETO_14D", Name = "14 dias", DaysUntilDue = 14, IsActive = true });
        await db.SaveChangesAsync();

        var reader = new FixedWmcPaymentConditionReader(new WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 1, 14, 0, "I", "WMC Firebird mirror", DateTimeOffset.Parse("2026-10-20T12:00:00Z")));
        var result = Assert.IsType<ViewResult>(await CreateController(db, reader).Edit(customer.Id));
        var model = Assert.IsType<AdminCustomerApprovalViewModel>(result.Model);

        Assert.Contains("14 DIAS", model.WmcPaymentConditionDescription, StringComparison.Ordinal);
        Assert.False(model.WmcPaymentConditionEligible);
        Assert.Contains("não elegível", model.WmcPaymentConditionStatus, StringComparison.OrdinalIgnoreCase);

        var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Areas/Admin/Views/Customers/Edit.cshtml"));
        var view = File.ReadAllText(viewPath);
        Assert.Contains("WmcPaymentConditionEligible", view, StringComparison.Ordinal);
        Assert.Contains("Model.WmcPaymentConditionStatus", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Condição autorizada pelo WMC", view, StringComparison.Ordinal);
    }

    private static CustomersController CreateController(Orofoods.Web.Data.ApplicationDbContext db, IWmcCustomerPaymentConditionReader? reader = null)
    {
        var accessService = new SalesRepresentativeAccessService(db);
        var paymentOptions = Options.Create(new PaymentEligibilityOptions { CardOnDeliveryEnabled = true });
        var priceService = new PriceService(db);
        var eligibilityService = new PaymentEligibilityService(db, paymentOptions, reader);
        var controller = new CustomersController(
            db,
            new CustomerApprovalService(db),
            accessService,
            eligibilityService,
            priceService,
            new AssistedOrderService(db, priceService, eligibilityService, new OrderReservationService(db)),
            new CommercialAttentionService(db, accessService, Options.Create(new CrmOptions()), paymentOptions))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "admin-user"),
                        new Claim(ClaimTypes.Email, "admin@orofoods.local"),
                        new Claim(ClaimTypes.Role, "Administrador")
                    ], "test"))
                }
            }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());
        return controller;
    }

    private sealed class FixedWmcPaymentConditionReader(WmcCustomerPaymentCondition condition) : IWmcCustomerPaymentConditionReader
    {
        public Task<WmcCustomerPaymentCondition?> GetForCustomerAsync(string customerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<WmcCustomerPaymentCondition?>(condition);
    }
}
