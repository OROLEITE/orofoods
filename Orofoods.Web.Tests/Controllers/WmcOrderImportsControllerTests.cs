using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class WmcOrderImportsControllerTests
{
    [Fact]
    public async Task Operator_sees_a_newly_saved_received_order_in_the_pending_grid_without_an_import_action()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = new Order
        {
            Number = "ORO-2026-000901",
            Customer = new Customer
            {
                LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99",
                IsActive = true, Status = CustomerStatus.Approved
            },
            CreatedByUser = new ApplicationUser
            {
                Id = "order-creator", UserName = "creator@test.local", NormalizedUserName = "CREATOR@TEST.LOCAL",
                Email = "creator@test.local", NormalizedEmail = "CREATOR@TEST.LOCAL"
            },
            Status = OrderStatus.Received,
            Total = 100m,
            WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Pending }
        };
        db.Add(order);
        await db.SaveChangesAsync();

        var options = Options.Create(new WmcJsonOrderOptions { Enabled = true, ManualImportEnabled = true });
        var jsonIntegration = new WmcJsonOrderErpIntegration(
            new HttpClient(new NeverCalledHandler()), options, new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db), Mock.Of<IWmcTransmissionIdAllocator>());
        var controller = new WmcOrderImportsController(
            db,
            new WmcOrderImportAccessService(db, new SalesRepresentativeAccessService(db)),
            new OrderIntegrationService(db, jsonIntegration),
            jsonIntegration,
            options);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(ApplicationRoles.Operator, "operator-user")
            }
        };

        var view = Assert.IsType<ViewResult>(await controller.Index(default));
        var rows = Assert.IsAssignableFrom<IReadOnlyList<Order>>(view.Model);

        Assert.Contains(rows, item => item.Id == order.Id);
        var eligible = Assert.IsAssignableFrom<IReadOnlySet<int>>(view.ViewData["WmcImportEligibleOrderIds"]);
        Assert.DoesNotContain(order.Id, eligible);
    }

    [Fact]
    public async Task Reconcile_action_queries_only_nropedido_and_keeps_the_order_imported()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = new Order
        {
            Number = "ORO-2026-000902",
            Customer = new Customer
            {
                LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99",
                IsActive = true, Status = CustomerStatus.Approved
            },
            CreatedByUser = new ApplicationUser
            {
                Id = "order-creator-2", UserName = "creator2@test.local", NormalizedUserName = "CREATOR2@TEST.LOCAL",
                Email = "creator2@test.local", NormalizedEmail = "CREATOR2@TEST.LOCAL"
            },
            Status = OrderStatus.Approved,
            IntegrationStatus = IntegrationStatus.Succeeded,
            WmcTransmissionId = 9001,
            WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Imported },
            WmcExportAudits =
            [
                new WmcExportAudit
                {
                    AttemptId = Guid.NewGuid(), Source = WmcExportSource.Manual,
                    Outcome = WmcExportOutcome.Succeeded, Succeeded = true, HttpStatusCode = 200,
                    ResponseBody = "importado com sucesso", WmcTransmissionId = 9001,
                    WmcSalesRepresentativeCode = 804, WmcDestination = "https://wmc.example.invalid/integracao"
                }
            ]
        };
        db.Add(order);
        await db.SaveChangesAsync();

        var options = Options.Create(new WmcJsonOrderOptions { Enabled = true, ManualImportEnabled = true });
        var handler = new JsonResponseHandler();
        var jsonIntegration = new WmcJsonOrderErpIntegration(
            new HttpClient(handler), options, new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db), Mock.Of<IWmcTransmissionIdAllocator>());
        var controller = new WmcOrderImportsController(
            db,
            new WmcOrderImportAccessService(db, new SalesRepresentativeAccessService(db)),
            new OrderIntegrationService(db, jsonIntegration),
            jsonIntegration,
            options);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(ApplicationRoles.Operator, "operator-user")
            }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());

        var result = await controller.ReconcileNroPedido(order.Id, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("776655", order.ErpOrderNumber);
        Assert.Equal(WmcOrderExportStatus.Imported, order.WmcOrderExportState!.Status);
        var requestUri = Assert.Single(handler.RequestUris);
        Assert.EndsWith("/nropedido.php?idpedido=9001&codvendedor=804", requestUri, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal Principal(string role, string userId) => new(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "test"));

    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The pending grid must not transmit orders.");
    }

    private sealed class JsonResponseHandler : HttpMessageHandler
    {
        public List<string> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"NROPEDIDO\":776655}]")
            });
        }
    }
}
