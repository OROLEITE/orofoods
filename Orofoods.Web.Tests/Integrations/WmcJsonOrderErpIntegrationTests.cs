using System.Net;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Integrations;

public class WmcJsonOrderErpIntegrationTests
{
    [Fact]
    public async Task Uses_the_serial_assigned_to_the_order_sales_representative()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(
            (HttpStatusCode.OK, "importado com sucesso"),
            (HttpStatusCode.OK, "[{\"NROPEDIDO\":776655}]"));
        using var client = new HttpClient(handler);
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(CreateOptions(order.Id, "https://wmc.example.invalid/integracao/", "device-test", "global-serial", sellerSerial: "seller-27-serial")),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var result = await integration.SendOrderAsync(order);

        Assert.True(result.Succeeded);
        Assert.Equal("https://wmc.example.invalid/integracao/exportar.php?imei=device-test&serial=seller-27-serial", handler.RequestUri!.AbsoluteUri);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal("https://wmc.example.invalid/integracao/nropedido.php?idpedido=9001&codvendedor=804", handler.Requests[1].Uri!.AbsoluteUri);
        Assert.Equal(9001, Assert.Single(db.WmcExportAudits).WmcTransmissionId);
        Assert.Equal(804, db.WmcExportAudits.Single().WmcSalesRepresentativeCode);
    }

    [Fact]
    public async Task Does_not_send_when_the_order_sales_representative_has_no_serial()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler((HttpStatusCode.OK, "importado com sucesso"));
        using var client = new HttpClient(handler);
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(CreateOptions(order.Id, sellerSerial: "")),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(WmcExportOutcome.Failed, Assert.Single(db.WmcExportAudits).Outcome);
    }

    [Fact]
    public async Task Does_not_send_to_an_insecure_or_credential_bearing_base_url()
    {
        var credentialBearingBaseUrl = new UriBuilder(Uri.UriSchemeHttps, "wmc.example.invalid")
        {
            UserName = "test-user",
            Password = "test-value",
            Path = "integracao/"
        }.Uri.ToString();
        foreach (var baseUrl in new[]
        {
            "http://wmc.example.invalid/integracao/",
            credentialBearingBaseUrl,
            "https://wmc.example.invalid/integracao/?imei=unexpected"
        })
        {
            await using var db = await TestDbContextFactory.CreateAsync();
            var order = CreateOrder();
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var handler = new RecordingHandler((HttpStatusCode.OK, "importado com sucesso"));
            using var client = new HttpClient(handler);
            var integration = new WmcJsonOrderErpIntegration(
                client,
                Options.Create(CreateOptions(order.Id, baseUrl)),
                new WmcMobileOrderPayloadBuilder(),
                new WmcExportAuditService(db),
                new FixedWmcTransmissionIdAllocator(9001));

            var result = await integration.SendOrderAsync(order);

            Assert.False(result.Succeeded);
            Assert.Equal(0, handler.CallCount);
            Assert.Equal(WmcExportOutcome.Failed, Assert.Single(db.WmcExportAudits).Outcome);
        }
    }

    [Fact]
    public async Task Posts_the_mobile_json_contract_and_accepts_only_the_trimmed_success_response()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(
            (HttpStatusCode.OK, "  importado com sucesso\r\n"),
            (HttpStatusCode.OK, "[{\"NROPEDIDO\":776655}]"));
        using var client = new HttpClient(handler);
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(CreateOptions(order.Id, "https://wmc.example.invalid/integracao/", "device id+17", "serial/29?")),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var result = await integration.SendOrderAsync(order);

        Assert.True(result.Succeeded);
        Assert.Equal("9001", result.ExternalOrderId);
        Assert.Equal("776655", result.ErpOrderNumber);
        Assert.NotEqual(9001, order.Id);
        Assert.Equal("776655", order.ErpOrderNumber);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://wmc.example.invalid/integracao/exportar.php?imei=device%20id%2B17&serial=serial%2F29%3F",
            handler.RequestUri!.AbsoluteUri);
        Assert.Equal("application/x-www-form-urlencoded", handler.ContentType);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("https://wmc.example.invalid/integracao/nropedido.php?idpedido=9001&codvendedor=804", handler.Requests[1].Uri!.AbsoluteUri);
        Assert.Null(handler.Requests[1].Body);

        var form = ParseForm(handler.Body!);
        Assert.Equal(new[] { "HTTP_JSON" }, form.Keys);
        using var payload = JsonDocument.Parse(form["HTTP_JSON"]);
        var rows = payload.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal("pedidos", rows[0].GetProperty("tabela").GetString());
        Assert.Equal("ped_prods", rows[1].GetProperty("tabela").GetString());
        Assert.Equal(9001, rows[0].GetProperty("id").GetInt32());
        Assert.Equal(0, rows[0].GetProperty("nropedido").GetInt32());
        Assert.Equal(9001, rows[1].GetProperty("id").GetInt32());
        Assert.Equal(0, rows[1].GetProperty("nropedido").GetInt32());
        AssertKeys(rows[0],
            "tabela", "id", "nropedido", "data", "idcliente", "codcliente", "totqtde", "dataentrega",
            "valprods", "valtotal", "obs", "datahora", "horaped", "codvendedor", "nrodocumento", "nroserie",
            "valorfrete");
        AssertKeys(rows[1],
            "tabela", "codvendedor", "id", "nropedido", "nroitem", "codproduto", "descricao", "qtde", "preco",
            "valtotal", "preco_s_desc", "un", "undig");
        Assert.All(rows, row => Assert.All(row.EnumerateObject(), property => Assert.NotEqual(JsonValueKind.Null, property.Value.ValueKind)));
        Assert.False(rows[0].TryGetProperty("nromanifesto", out _));
        Assert.False(rows[0].TryGetProperty("nroest", out _));
        Assert.False(rows[0].TryGetProperty("datacontingencia", out _));
        Assert.Equal("107072", rows[0].GetProperty("codcliente").GetString());
        Assert.Equal(804, rows[0].GetProperty("codvendedor").GetInt32());
        Assert.Equal(0, rows[0].GetProperty("nrodocumento").GetInt32());
        Assert.Equal(0, rows[0].GetProperty("nroserie").GetInt32());
        Assert.Equal("610601552", rows[1].GetProperty("codproduto").GetString());
        Assert.Equal(2, rows[1].GetProperty("qtde").GetInt32());
        Assert.Equal("Pedido de homologação & ação + 50% ~*", rows[0].GetProperty("obs").GetString());
        Assert.Contains("%26", handler.Body!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%2B", handler.Body!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%25", handler.Body!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%C3%A7%C3%A3o+%26+a%C3%A7%C3%A3o+%2B+50%25+%7E*", handler.Body!, StringComparison.OrdinalIgnoreCase);

        var audit = Assert.Single(db.WmcExportAudits);
        Assert.Equal(WmcExportAudit.AutomatedAttemptMarker, audit.ExportedByEmail);
        Assert.Equal(WmcExportSource.Automatic, audit.Source);
        Assert.Equal((int)HttpStatusCode.OK, audit.HttpStatusCode);
        Assert.Equal("  importado com sucesso\r\n", audit.ResponseBody);
        Assert.Equal(WmcExportOutcome.Succeeded, audit.Outcome);
        Assert.Equal(9001, audit.WmcTransmissionId);
        Assert.Equal(804, audit.WmcSalesRepresentativeCode);
        Assert.Equal("https://wmc.example.invalid/integracao", audit.WmcDestination);
        Assert.Equal((int)HttpStatusCode.OK, audit.NroPedidoHttpStatusCode);
        Assert.Equal("[{\"NROPEDIDO\":776655}]", audit.NroPedidoResponseBody);
        Assert.Null(audit.NroPedidoError);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Does_not_persist_a_non_positive_nropedido_as_the_erp_order_number(int nropedido)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(
            (HttpStatusCode.OK, "importado com sucesso"),
            (HttpStatusCode.OK, $"[{{\"NROPEDIDO\":{nropedido}}}]"));
        using var client = new HttpClient(handler);
        var integration = CreateIntegration(client, db);

        var result = await integration.SendOrderAsync(order);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErpOrderNumber);
        Assert.Null(order.ErpOrderNumber);
        Assert.Equal(2, handler.CallCount);
        Assert.NotNull(Assert.Single(db.WmcExportAudits).NroPedidoError);
    }

    [Fact]
    public async Task Does_not_send_when_the_wmc_seller_code_exceeds_the_apk_short_range()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(
            (HttpStatusCode.OK, "importado com sucesso"),
            (HttpStatusCode.OK, "[{\"NROPEDIDO\":776655}]"));
        using var client = new HttpClient(handler);
        var options = CreateOptions(order.Id);
        options.SalesRepresentativeCodes[27] = short.MaxValue + 1;
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(options),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(WmcExportOutcome.Failed, Assert.Single(db.WmcExportAudits).Outcome);
    }

    [Fact]
    public async Task Reconciles_the_wmc_number_from_the_persisted_destination_without_exporting_again()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await CreateConfirmedExportAwaitingNumberAsync(db);
        var handler = new RecordingHandler((HttpStatusCode.OK, "[{\"NROPEDIDO\":776655}]"));
        using var client = new HttpClient(handler);
        var integration = CreateIntegration(client, db);

        await integration.ReconcileNroPedidoAsync(order.Id);

        Assert.Equal("776655", order.ErpOrderNumber);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://wmc-original.example.invalid/integracao/nropedido.php?idpedido=9001&codvendedor=804",
            request.Uri!.AbsoluteUri);
        Assert.Null(request.Body);
        var audit = Assert.Single(db.WmcExportAudits);
        Assert.Equal(9001, audit.WmcTransmissionId);
        Assert.Equal(804, audit.WmcSalesRepresentativeCode);
        Assert.Equal("https://wmc-original.example.invalid/integracao", audit.WmcDestination);
        Assert.Equal("[{\"NROPEDIDO\":776655}]", audit.NroPedidoResponseBody);
        Assert.Null(audit.NroPedidoError);
    }

    [Fact]
    public async Task Keeps_failed_number_lookup_pending_and_recovers_on_a_later_lookup_only_call()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await CreateConfirmedExportAwaitingNumberAsync(db);
        var handler = new RecordingHandler(
            (HttpStatusCode.ServiceUnavailable, "lookup unavailable"),
            (HttpStatusCode.OK, "[{\"NROPEDIDO\":776655}]"));
        using var client = new HttpClient(handler);
        var integration = CreateIntegration(client, db);

        await integration.ReconcileNroPedidoAsync(order.Id);

        Assert.Null(order.ErpOrderNumber);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, Assert.Single(db.WmcExportAudits).NroPedidoHttpStatusCode);
        Assert.NotNull((await db.WmcExportAudits.SingleAsync()).NroPedidoError);

        await integration.ReconcileNroPedidoAsync(order.Id);

        Assert.Equal("776655", order.ErpOrderNumber);
        Assert.Equal(2, handler.CallCount);
        Assert.All(handler.Requests, request => Assert.EndsWith("/nropedido.php?idpedido=9001&codvendedor=804", request.Uri!.AbsoluteUri));
    }

    [Fact]
    public async Task Does_not_query_again_after_the_wmc_number_has_been_recovered()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await CreateConfirmedExportAwaitingNumberAsync(db);
        order.ErpOrderNumber = "776655";
        await db.SaveChangesAsync();
        var handler = new RecordingHandler((HttpStatusCode.OK, "[{\"NROPEDIDO\":776655}]"));
        using var client = new HttpClient(handler);
        var integration = CreateIntegration(client, db);

        await integration.ReconcileNroPedidoAsync(order.Id);

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Keeps_a_confirmed_post_succeeded_when_nropedido_lookup_fails()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(
            (HttpStatusCode.OK, "importado com sucesso"),
            (HttpStatusCode.ServiceUnavailable, "lookup unavailable"));
        using var client = new HttpClient(handler);
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(CreateOptions(order.Id)),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns("Test");
        var service = new OrderIntegrationService(db, integration, environment.Object);
        await service.SendAsync(order.Id);
        await service.SendAsync(order.Id);

        Assert.Equal(2, handler.CallCount);
        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Succeeded, persisted.IntegrationStatus);
        Assert.Equal(9001, persisted.WmcTransmissionId);
        Assert.Equal("9001", persisted.ExternalOrderId);
        Assert.Null(persisted.ErpOrderNumber);
        Assert.Single(await db.WmcExportAudits.ToListAsync());
        var audit = Assert.Single(db.WmcExportAudits);
        Assert.Equal(WmcExportOutcome.Succeeded, audit.Outcome);
        Assert.Equal((int)HttpStatusCode.OK, audit.HttpStatusCode);
        Assert.Equal("importado com sucesso", audit.ResponseBody);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, audit.NroPedidoHttpStatusCode);
        Assert.Equal("lookup unavailable", audit.NroPedidoResponseBody);
        Assert.NotNull(audit.NroPedidoError);
    }

    [Fact]
    public async Task Reuses_the_persisted_transmission_id_after_a_pre_post_mapping_failure()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        order.Items.Single().Product!.WmcCode = null;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(
            (HttpStatusCode.OK, "importado com sucesso"),
            (HttpStatusCode.OK, "[{\"NROPEDIDO\":776655}]"));
        using var client = new HttpClient(handler);
        var integration = CreateIntegration(client, db);

        var first = await integration.SendOrderAsync(order);
        Assert.False(first.Succeeded);
        Assert.False(first.OutcomeUnknown);
        Assert.Equal(9001, order.WmcTransmissionId);
        Assert.Equal(0, handler.CallCount);

        order.Items.Single().Product!.WmcCode = "610601552";
        var second = await integration.SendOrderAsync(order);

        Assert.True(second.Succeeded);
        Assert.Equal("9001", second.ExternalOrderId);
        Assert.Equal(9001, order.WmcTransmissionId);
        Assert.Equal(2, handler.CallCount);
        var attempts = await db.WmcExportAudits.OrderBy(audit => audit.Id).ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.All(attempts, attempt => Assert.Equal(9001, attempt.WmcTransmissionId));
        Assert.Equal(WmcExportOutcome.Failed, attempts[0].Outcome);
        Assert.Equal(WmcExportOutcome.Succeeded, attempts[1].Outcome);
    }

    [Fact]
    public async Task Refuses_to_post_an_order_that_is_not_the_configured_homologation_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(HttpStatusCode.OK, "importado com sucesso");
        using var client = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WmcJsonOrder:Enabled"] = "true",
            ["WmcJsonOrder:BaseUrl"] = "https://wmc.example.invalid/",
            ["WmcJsonOrder:DeviceId"] = "device-test",
            ["WmcJsonOrder:HomologationOrderId"] = (order.Id + 1).ToString(CultureInfo.InvariantCulture),
            ["WmcJsonOrder:HomologationOrderIdConfirmedUnique"] = "true",
            ["WmcJsonOrder:SalesRepresentativeCodes:27"] = "804",
            ["WmcJsonOrder:SalesRepresentativeSerials:27"] = "serial-test"
        }).Build();
        var options = configuration.GetSection(WmcJsonOrderOptions.SectionName).Get<WmcJsonOrderOptions>()!;
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(options),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(WmcExportOutcome.Failed, Assert.Single(db.WmcExportAudits).Outcome);
    }

    [Fact]
    public async Task Refuses_to_post_until_the_selected_id_is_confirmed_absent_from_wmc()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(HttpStatusCode.OK, "importado com sucesso");
        using var client = new HttpClient(handler);
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(CreateOptions(order.Id, homologationOrderIdConfirmedUnique: false)),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(WmcExportOutcome.Failed, Assert.Single(db.WmcExportAudits).Outcome);
    }

    [Fact]
    public async Task Does_not_treat_an_unexpected_response_as_success_and_records_delivery_as_unknown()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable, "pedido já processado");
        using var client = new HttpClient(handler);
        var integration = CreateIntegration(client, db);

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal(1, handler.CallCount);
        var audit = Assert.Single(db.WmcExportAudits);
        Assert.Equal(WmcExportOutcome.Unknown, audit.Outcome);
        Assert.Equal("pedido já processado", audit.ResponseBody);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, audit.HttpStatusCode);
        Assert.Equal(9001, audit.WmcTransmissionId);
        Assert.Equal(9001, order.WmcTransmissionId);
    }

    [Fact]
    public async Task Does_not_accept_a_non_200_status_even_when_the_body_matches_the_success_text()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(HttpStatusCode.Created, "importado com sucesso");
        using var client = new HttpClient(handler);
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(CreateOptions(order.Id)),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal(WmcExportOutcome.Unknown, Assert.Single(db.WmcExportAudits).Outcome);
    }

    [Fact]
    public async Task Does_not_send_when_a_real_sales_representative_code_is_not_configured()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(HttpStatusCode.OK, "importado com sucesso");
        using var client = new HttpClient(handler);
        var integration = new WmcJsonOrderErpIntegration(
            client,
            Options.Create(new WmcJsonOrderOptions
            {
                Enabled = true,
                BaseUrl = "https://wmc.example.invalid/",
                DeviceId = "device-test",
                SalesRepresentativeSerials = new Dictionary<int, string> { [27] = "serial-test" }
            }),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(WmcExportOutcome.Failed, Assert.Single(db.WmcExportAudits).Outcome);
    }

    [Fact]
    public async Task Records_a_network_failure_as_unknown_without_retrying()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new ThrowingHandler();
        using var client = new HttpClient(handler);
        var integration = CreateIntegration(client, db);

        var result = await integration.SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal(1, handler.CallCount);
        var audit = Assert.Single(db.WmcExportAudits);
        Assert.Equal(WmcExportOutcome.Unknown, audit.Outcome);
        Assert.Null(audit.HttpStatusCode);
        Assert.Null(audit.ResponseBody);
        Assert.Contains("simulated network interruption", audit.Error);
    }

    private static WmcJsonOrderErpIntegration CreateIntegration(HttpClient client, Orofoods.Web.Data.ApplicationDbContext db) =>
        new(
            client,
            Options.Create(CreateOptions(1)),
            new WmcMobileOrderPayloadBuilder(),
            new WmcExportAuditService(db),
            new FixedWmcTransmissionIdAllocator(9001));

    private static async Task<Order> CreateConfirmedExportAwaitingNumberAsync(Orofoods.Web.Data.ApplicationDbContext db)
    {
        var order = CreateOrder();
        order.WmcTransmissionId = 9001;
        order.ExternalOrderId = "9001";
        order.IntegrationStatus = IntegrationStatus.Succeeded;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            AttemptId = Guid.NewGuid(),
            Outcome = WmcExportOutcome.Succeeded,
            Succeeded = true,
            HttpStatusCode = (int)HttpStatusCode.OK,
            ResponseBody = "importado com sucesso",
            WmcTransmissionId = 9001,
            WmcSalesRepresentativeCode = 804,
            WmcDestination = "https://wmc-original.example.invalid/integracao"
        });
        await db.SaveChangesAsync();
        return order;
    }

    private static WmcJsonOrderOptions CreateOptions(
        int homologationOrderId,
        string baseUrl = "https://wmc.example.invalid/",
        string deviceId = "device-test",
        string serial = "serial-test",
        bool homologationOrderIdConfirmedUnique = true,
        string? sellerSerial = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WmcJsonOrder:Enabled"] = "true",
            ["WmcJsonOrder:BaseUrl"] = baseUrl,
            ["WmcJsonOrder:DeviceId"] = deviceId,
            ["WmcJsonOrder:SalesRepresentativeSerials:27"] = sellerSerial ?? serial,
            ["WmcJsonOrder:HomologationOrderId"] = homologationOrderId.ToString(CultureInfo.InvariantCulture),
            ["WmcJsonOrder:HomologationOrderIdConfirmedUnique"] = homologationOrderIdConfirmedUnique.ToString(CultureInfo.InvariantCulture),
            ["WmcJsonOrder:SalesRepresentativeCodes:27"] = "804"
        }).Build().GetSection(WmcJsonOrderOptions.SectionName).Get<WmcJsonOrderOptions>()!;

    private static Order CreateOrder() => new()
    {
        Number = "ORO-2026-000017",
        ConfirmedAt = new DateTime(2026, 10, 21, 10, 30, 0, DateTimeKind.Utc),
        CustomerId = 8,
        Customer = new Customer
        {
            Id = 8,
            LegalName = "Cliente de teste",
            TradeName = "Cliente",
            Cnpj = "00000000000000",
            WmcCode = "107072",
            SalesRepresentativeId = 27,
            SalesRepresentative = new SalesRepresentative { Id = 27, Name = "Vendedor teste", Email = "seller@example.invalid" }
        },
        CreatedByUser = TestDbContextFactory.CreateOrderCreator(),
        CreatedAt = new DateTime(2026, 10, 21, 10, 30, 0, DateTimeKind.Utc),
        RequestedDeliveryDate = new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc),
        Subtotal = 100m,
        Freight = 5m,
        Total = 105m,
        Notes = "Pedido de homologação & ação + 50% ~*",
        Items =
        [
            new OrderItem
            {
                Product = new Product { Sku = "TEST-1", WmcCode = "610601552", Name = "Produto de teste", Unit = "caixa", Brand = "Teste", ProductCategory = new ProductCategory { Name = "Teste", Slug = "teste" } },
                ProductNameSnapshot = "Produto de teste",
                SkuSnapshot = "TEST-1",
                Quantity = 2,
                UnitPrice = 50m,
                Subtotal = 100m
            }
        ]
    };

    private static Dictionary<string, string> ParseForm(string body) => body
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .ToDictionary(
            pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
            pair => Uri.UnescapeDataString(pair.Length > 1 ? pair[1].Replace('+', ' ') : string.Empty));

    private static void AssertKeys(JsonElement row, params string[] expected)
    {
        var actual = row.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
    }

    private sealed class RecordingHandler(params (HttpStatusCode StatusCode, string ResponseBody)[] responses) : HttpMessageHandler
    {
        public RecordingHandler(HttpStatusCode statusCode, string responseBody)
            : this((statusCode, responseBody))
        {
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ContentType { get; private set; }
        public string? Body { get; private set; }
        public int CallCount { get; private set; }
        public List<(HttpMethod Method, Uri? Uri, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri, body));
            if (CallCount == 1)
            {
                Method = request.Method;
                RequestUri = request.RequestUri;
                ContentType = request.Content?.Headers.ContentType?.ToString();
                Body = body;
            }

            var response = responses[Math.Min(CallCount - 1, responses.Length - 1)];
            return new HttpResponseMessage(response.StatusCode) { Content = new StringContent(response.ResponseBody) };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new HttpRequestException("simulated network interruption");
        }
    }

    private sealed class FixedWmcTransmissionIdAllocator(int transmissionId) : IWmcTransmissionIdAllocator
    {
        public Task<int> GetOrAllocateAsync(Order order, CancellationToken cancellationToken = default)
        {
            order.WmcTransmissionId ??= transmissionId;
            return Task.FromResult(order.WmcTransmissionId.Value);
        }
    }
}
