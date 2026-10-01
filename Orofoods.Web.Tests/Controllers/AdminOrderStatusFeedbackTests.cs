using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminOrderStatusFeedbackTests
{
    [Fact]
    public async Task RejectedCardOnDeliveryCompletion_StoresFriendlyFeedback()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Teste Ltda", TradeName = "Teste", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "admin-1", UserName = "admin@test", Email = "admin@test" };
        var paymentTerm = new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega", IsActive = true };
        var order = new Order { Customer = customer, CreatedByUser = user, PaymentTerm = paymentTerm, Number = "ORO-1", Status = OrderStatus.Received };
        db.Add(order);
        await db.SaveChangesAsync();
        var eligibility = Microsoft.Extensions.Options.Options.Create(new PaymentEligibilityOptions());
        var service = new AdminOrderService(
            db,
            TimeProvider.System,
            new OrderReservationService(db),
            new PaymentEligibilityService(db, eligibility),
            new PaymentService(db, eligibility, new PendingBoletoProvider()));
        var controller = new OrdersController(db, service, integrationService: null!, pointPaymentService: null!, Options.Create(new MercadoPagoPointOptions()), Options.Create(new PaymentEligibilityOptions()), hostEnvironment: null!);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new TestTempDataProvider());

        var result = await controller.UpdateStatus(order.Id, OrderStatus.Delivered);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("O pedido utiliza Cartão na Entrega e o pagamento ainda não foi aprovado.", controller.TempData["OrderStatusError"]);
        Assert.Equal(OrderStatus.Received, order.Status);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
