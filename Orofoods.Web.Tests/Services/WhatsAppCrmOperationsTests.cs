using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WhatsAppCrmOperationsTests
{
    [Theory]
    [InlineData(nameof(WhatsAppController.Index))]
    [InlineData(nameof(WhatsAppController.Updates))]
    [InlineData(nameof(WhatsAppController.CustomerSearch))]
    [InlineData(nameof(WhatsAppController.StartConversation))]
    [InlineData(nameof(WhatsAppController.Templates))]
    [InlineData(nameof(WhatsAppController.Media))]
    [InlineData(nameof(WhatsAppController.Send))]
    [InlineData(nameof(WhatsAppController.SendTemplate))]
    [InlineData(nameof(WhatsAppController.Claim))]
    [InlineData(nameof(WhatsAppController.Transfer))]
    [InlineData(nameof(WhatsAppController.Close))]
    [InlineData(nameof(WhatsAppController.Reopen))]
    [InlineData(nameof(WhatsAppController.LinkCustomer))]
    [InlineData(nameof(WhatsAppController.UnlinkCustomer))]
    public async Task WhatsApp_actions_return_not_found_before_execution_when_feature_is_disabled(string actionName)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var gateway = new TrackingGateway();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(new SalesRepresentativeAccessService(db));
        services.AddSingleton<IWhatsAppBusinessGateway>(gateway);
        services.AddSingleton<IOptions<WhatsAppBusinessOptions>>(Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = false
        }));
        using var serviceProvider = services.BuildServiceProvider();
        var controller = ActivatorUtilities.CreateInstance<WhatsAppController>(serviceProvider);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "operator"), new Claim(ClaimTypes.Role, ApplicationRoles.Operator)],
                "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object);
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor { ActionName = actionName, ControllerName = "WhatsApp" },
            new ModelStateDictionary());
        var executingContext = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller);

        controller.OnActionExecuting(executingContext);

        Assert.IsType<NotFoundResult>(executingContext.Result);
        Assert.Empty(db.WhatsAppConversations);
        Assert.Empty(db.WhatsAppMessages);
        Assert.Equal(0, gateway.TextCalls);
        Assert.Equal(0, gateway.TemplateCalls);
        Assert.Equal(0, gateway.TemplateCatalogCalls);
    }

    [Fact]
    public void Service_window_is_open_until_but_not_including_24_hours_after_inbound()
    {
        var inboundAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(WhatsAppServiceWindow.IsOpen(inboundAt, inboundAt.AddHours(23).AddMinutes(59)));
        Assert.False(WhatsAppServiceWindow.IsOpen(inboundAt, inboundAt.AddHours(24)));
        Assert.False(WhatsAppServiceWindow.IsOpen(inboundAt, inboundAt.AddHours(25)));
        Assert.False(WhatsAppServiceWindow.IsOpen(null, inboundAt));
    }

    [Fact]
    public async Task Embedded_signup_business_account_provider_uses_the_singleton_connection()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppBusinessConnections.Add(new WhatsAppBusinessConnection
        {
            Id = WhatsAppBusinessConnection.SingletonId,
            BusinessAccountId = "1234567890123",
            PhoneNumberId = "phone-123",
            DisplayPhoneNumber = "+55 48 92007-7785",
            ProtectedAccessToken = "protected-token"
        });
        await db.SaveChangesAsync();
        var provider = new WhatsAppBusinessAccountIdProvider(db, Options.Create(new WhatsAppBusinessOptions
        {
            EmbeddedSignupEnabled = true,
            BusinessAccountId = "legacy-account",
            EmbeddedSignupPhoneNumberId = "phone-123",
            EmbeddedSignupDisplayPhoneNumber = "+5548920077785"
        }));

        Assert.Equal("1234567890123", await provider.GetAsync());
    }

    [Fact]
    public async Task Embedded_signup_business_account_provider_does_not_fall_back_when_connection_is_missing_or_divergent()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var settings = Options.Create(new WhatsAppBusinessOptions
        {
            EmbeddedSignupEnabled = true,
            BusinessAccountId = "legacy-account",
            EmbeddedSignupPhoneNumberId = "configured-phone",
            EmbeddedSignupDisplayPhoneNumber = "+5548920077785"
        });
        var provider = new WhatsAppBusinessAccountIdProvider(db, settings);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync());
        db.WhatsAppBusinessConnections.Add(new WhatsAppBusinessConnection
        {
            Id = WhatsAppBusinessConnection.SingletonId,
            BusinessAccountId = "divergent-account",
            PhoneNumberId = "other-phone",
            DisplayPhoneNumber = "+5511999990000",
            ProtectedAccessToken = "protected-token"
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync());
    }

    [Fact]
    public async Task Legacy_business_account_provider_uses_configured_account_when_embedded_signup_is_disabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var provider = new WhatsAppBusinessAccountIdProvider(db, Options.Create(new WhatsAppBusinessOptions
        {
            EmbeddedSignupEnabled = false,
            BusinessAccountId = "legacy-account"
        }));

        Assert.Equal("legacy-account", await provider.GetAsync());
    }

    [Fact]
    public async Task Conversation_phone_and_outbound_request_id_are_unique_in_the_database_model()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversationType = db.Model.FindEntityType(typeof(WhatsAppConversation))!;
        var messageType = db.Model.FindEntityType(typeof(WhatsAppMessage))!;
        var phoneIndex = conversationType.GetIndexes().Single(index => index.Properties.Single().Name == nameof(WhatsAppConversation.PhoneNumber));
        var clientRequestProperty = messageType.FindProperty("ClientRequestId");

        Assert.True(phoneIndex.IsUnique);
        Assert.NotNull(clientRequestProperty);
        Assert.Contains(messageType.GetIndexes(), index => index.IsUnique && index.Properties.Contains(clientRequestProperty));
    }

    [Fact]
    public async Task Database_rejects_duplicate_conversations_for_one_normalized_phone()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppConversations.AddRange(
            new WhatsAppConversation { PhoneNumber = "5511999990000" },
            new WhatsAppConversation { PhoneNumber = "5511999990000" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void PostgreSql_migration_stops_on_legacy_duplicate_phones_before_creating_unique_index()
    {
        using var db = new PostgreSqlApplicationDbContextDesignTimeFactory().CreateDbContext([]);
        var script = db.GetService<IMigrator>().GenerateScript(
            "20261106120359_AddWhatsAppWebhookDelivery",
            "20261110151548_AddWhatsAppCrmOperationGuards");

        var duplicateGuard = script.IndexOf("GROUP BY \"PhoneNumber\"", StringComparison.Ordinal);
        var uniqueIndex = script.IndexOf("CREATE UNIQUE INDEX \"IX_WhatsAppConversations_PhoneNumber\"", StringComparison.Ordinal);
        Assert.True(duplicateGuard >= 0, "The migration must stop transactionally when old conversation duplicates need review.");
        Assert.True(uniqueIndex > duplicateGuard, "The duplicate check must run before the unique index is created.");
    }

    [Fact]
    public async Task Customer_search_matches_cnpj_with_or_without_formatting()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.Add(new Customer
        {
            LegalName = "Mercado do Sul",
            TradeName = "Mercado do Sul",
            Cnpj = "11.222.333/0001-44",
            Phone = "(48) 3200-0000",
            WhatsApp = "5548920077785",
            IsActive = true
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway());

        var result = Assert.IsType<JsonResult>(await controller.CustomerSearch("11222333000144", CancellationToken.None));
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));

        Assert.Equal(1, payload.RootElement.GetProperty("results").GetArrayLength());
    }

    [Fact]
    public async Task Updates_exposes_the_current_service_window_state_for_the_selected_conversation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var inboundAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = inboundAt };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway(), timeProvider: new FixedTimeProvider(inboundAt.AddHours(24)));

        var result = Assert.IsType<JsonResult>(await controller.Updates(conversation.Id, CancellationToken.None));
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));

        Assert.False(payload.RootElement.GetProperty("serviceWindowOpen").GetBoolean());
    }

    [Fact]
    public async Task Start_conversation_normalizes_an_ad_hoc_number_and_reuses_the_existing_history()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var existing = new WhatsAppConversation { PhoneNumber = "5548920077785", Status = WhatsAppConversationStatus.Open };
        db.WhatsAppConversations.Add(existing);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway());

        var result = Assert.IsType<RedirectToActionResult>(await controller.StartConversation(null, "+55 (48) 92007-7785", CancellationToken.None));

        Assert.Equal(nameof(WhatsAppController.Index), result.ActionName);
        Assert.Equal(existing.Id, result.RouteValues!["id"]);
        Assert.Equal("5548920077785", (await db.WhatsAppConversations.SingleAsync()).PhoneNumber);
    }

    [Fact]
    public async Task Seller_starting_an_ad_hoc_conversation_owns_it_and_can_access_it()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Users.Add(new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway(), ApplicationRoles.Seller, "seller");

        var result = Assert.IsType<RedirectToActionResult>(await controller.StartConversation(null, "+55 (48) 92007-7785", CancellationToken.None));
        var conversation = await db.WhatsAppConversations.SingleAsync();

        Assert.Equal(conversation.Id, result.RouteValues!["id"]);
        Assert.Equal("seller", conversation.AssignedUserId);
        Assert.Equal(WhatsAppConversationStatus.Open, conversation.Status);
        Assert.True(await new SalesRepresentativeAccessService(db).CanAccessConversationAsync(controller.User, conversation.Id));
    }

    [Fact]
    public async Task Start_conversation_links_an_in_scope_customer_to_a_new_normalized_number()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Mercado do Sul",
            TradeName = "Mercado do Sul",
            Cnpj = "11.222.333/0001-44",
            WhatsApp = "+55 (48) 92007-7785",
            IsActive = true
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway());

        var result = Assert.IsType<RedirectToActionResult>(await controller.StartConversation(customer.Id, null, CancellationToken.None));
        var conversation = await db.WhatsAppConversations.SingleAsync();

        Assert.Equal("5548920077785", conversation.PhoneNumber);
        Assert.Equal(customer.Id, conversation.CustomerId);
        Assert.Equal(conversation.Id, result.RouteValues!["id"]);
        Assert.Equal(WhatsAppConversationStatus.Pending, conversation.Status);
    }

    [Fact]
    public async Task Start_conversation_rejects_customers_outside_the_current_seller_scope()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer
        {
            LegalName = "Cliente de outro vendedor",
            TradeName = "Cliente de outro vendedor",
            Cnpj = "11.222.333/0001-44",
            WhatsApp = "5548920077785",
            IsActive = true,
            InternalSalesUserId = "other-seller"
        };
        db.Users.Add(new ApplicationUser { Id = "other-seller", UserName = "other-seller", IsActive = true });
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway(), ApplicationRoles.Seller, "seller");

        Assert.IsType<ForbidResult>(await controller.StartConversation(customer.Id, null, CancellationToken.None));
        Assert.Empty(await db.WhatsAppConversations.ToListAsync());
    }

    [Fact]
    public async Task Free_text_send_is_blocked_at_the_24_hour_boundary()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var inboundAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = inboundAt };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway();
        var controller = CreateController(db, gateway, timeProvider: new FixedTimeProvider(inboundAt.AddHours(24)));

        await controller.Send(conversation.Id, "Livre", CancellationToken.None, Guid.NewGuid().ToString("N"));

        Assert.Equal(0, gateway.TextCalls);
        Assert.Empty(await db.WhatsAppMessages.ToListAsync());
    }

    [Fact]
    public async Task Repeated_send_with_same_request_id_calls_gateway_once_and_keeps_confirmed_status()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddMinutes(-5) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway();
        var controller = CreateController(db, gateway);
        var requestId = Guid.NewGuid().ToString("N");

        await controller.Send(conversation.Id, "Olá", CancellationToken.None, requestId);
        await controller.Send(conversation.Id, "Olá", CancellationToken.None, requestId);

        var message = await db.WhatsAppMessages.SingleAsync();
        Assert.Equal(1, gateway.TextCalls);
        Assert.Equal(requestId, message.ClientRequestId);
        Assert.Equal(WhatsAppMessageStatus.Sent, message.Status);
        Assert.Equal("wamid.test-1", message.ExternalMessageId);
    }

    [Fact]
    public async Task Ambiguous_send_retry_keeps_one_pending_message_and_does_not_call_gateway_twice()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddMinutes(-5) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway { NextException = new HttpRequestException("simulated timeout") };
        var controller = CreateController(db, gateway);
        var requestId = Guid.NewGuid().ToString("N");

        await controller.Send(conversation.Id, "OlÃ¡", CancellationToken.None, requestId);
        await controller.Send(conversation.Id, "OlÃ¡", CancellationToken.None, requestId);

        var message = await db.WhatsAppMessages.SingleAsync();
        Assert.Equal(1, gateway.TextCalls);
        Assert.Equal(WhatsAppMessageStatus.Pending, message.Status);
        Assert.Equal("UNKNOWN", message.ErrorCode);
        Assert.Null(message.ExternalMessageId);
        Assert.NotNull(controller.TempData["WhatsAppError"]);
    }

    [Fact]
    public async Task Failed_send_is_persisted_with_error_status()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddMinutes(-5) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway { NextResult = new WhatsAppSendResult(false, null, "131047", "Falha controlada") };
        var controller = CreateController(db, gateway);

        await controller.Send(conversation.Id, "Olá", CancellationToken.None, Guid.NewGuid().ToString("N"));

        var message = await db.WhatsAppMessages.SingleAsync();
        Assert.Equal(1, gateway.TextCalls);
        Assert.Equal(WhatsAppMessageStatus.Failed, message.Status);
        Assert.Equal("131047", message.ErrorCode);
        Assert.NotNull(message.FailedAt);
    }

    [Fact]
    public async Task Replaying_a_confirmed_failure_returns_an_error_without_looking_like_success()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddMinutes(-5) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway { NextResult = new WhatsAppSendResult(false, null, "131047", "Falha controlada") };
        var controller = CreateController(db, gateway);
        var requestId = Guid.NewGuid().ToString("N");

        await controller.Send(conversation.Id, "OlÃ¡", CancellationToken.None, requestId);
        controller.TempData.Clear();
        await controller.Send(conversation.Id, "OlÃ¡", CancellationToken.None, requestId);

        Assert.Equal(1, gateway.TextCalls);
        Assert.Equal(WhatsAppMessageStatus.Failed, (await db.WhatsAppMessages.SingleAsync()).Status);
        Assert.NotNull(controller.TempData["WhatsAppError"]);
    }

    [Fact]
    public async Task Successful_gateway_response_without_message_id_stays_pending_and_cannot_be_replayed_as_success()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddMinutes(-5) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway { NextResult = new WhatsAppSendResult(true, null, null, null) };
        var controller = CreateController(db, gateway);
        var requestId = Guid.NewGuid().ToString("N");

        await controller.Send(conversation.Id, "OlÃ¡", CancellationToken.None, requestId);
        controller.TempData.Clear();
        await controller.Send(conversation.Id, "OlÃ¡", CancellationToken.None, requestId);

        Assert.Equal(1, gateway.TextCalls);
        var message = await db.WhatsAppMessages.SingleAsync();
        Assert.Equal(WhatsAppMessageStatus.Pending, message.Status);
        Assert.Equal("UNKNOWN", message.ErrorCode);
        Assert.NotNull(controller.TempData["WhatsAppError"]);
    }

    [Fact]
    public async Task Server_error_response_stays_pending_and_does_not_release_a_retry()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddMinutes(-5) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway { NextResult = new WhatsAppSendResult(false, null, "UNKNOWN", "Falha de servidor") };
        var controller = CreateController(db, gateway);
        var requestId = Guid.NewGuid().ToString("N");

        await controller.Send(conversation.Id, "OlÃ¡", CancellationToken.None, requestId);
        controller.TempData.Clear();
        await controller.Send(conversation.Id, "OlÃ¡", CancellationToken.None, requestId);

        Assert.Equal(1, gateway.TextCalls);
        var message = await db.WhatsAppMessages.SingleAsync();
        Assert.Equal(WhatsAppMessageStatus.Pending, message.Status);
        Assert.Equal("UNKNOWN", message.ErrorCode);
        Assert.NotNull(controller.TempData["WhatsAppError"]);
    }

    [Fact]
    public async Task Template_send_rechecks_approval_and_preserves_rendered_history()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddHours(-25) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway
        {
            Templates = [new WhatsAppApprovedTemplate("order_ready", "pt_BR", "UTILITY", null, "Pedido {{1}} pronto", 0, 1)]
        };
        var controller = CreateController(db, gateway);

        await controller.SendTemplate(conversation.Id, "order_ready", "pt_BR", ["12345"], [], Guid.NewGuid().ToString("N"), CancellationToken.None);

        var message = await db.WhatsAppMessages.SingleAsync();
        Assert.Equal(1, gateway.TemplateCalls);
        Assert.Equal("Pedido 12345 pronto", message.TextBody);
        Assert.Equal(WhatsAppMessageStatus.Sent, message.Status);
    }

    [Fact]
    public async Task Named_template_parameters_keep_their_names_in_gateway_payload_and_rendered_history()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddHours(-25) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway
        {
            Templates =
            [
                new WhatsAppApprovedTemplate("named_order", "pt_BR", "UTILITY", null,
                    "Pedido {{order_number}} de {{customer_name}}", 0, 2, null, ["order_number", "customer_name"])
            ]
        };
        var controller = CreateController(db, gateway);

        await controller.SendTemplate(conversation.Id, "named_order", "pt_BR", ["12345", "Mercado Sul"], [], Guid.NewGuid().ToString("N"), CancellationToken.None);

        var message = await db.WhatsAppMessages.SingleAsync();
        Assert.Equal("Pedido 12345 de Mercado Sul", message.TextBody);
        Assert.Equal(new[] { "order_number", "customer_name" }, gateway.LastBodyParameterNames);
        Assert.Equal(1, gateway.TemplateCalls);
    }

    [Fact]
    public async Task Template_send_rejects_template_that_is_not_currently_approved()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddHours(-25) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway();
        var controller = CreateController(db, gateway);

        await controller.SendTemplate(conversation.Id, "draft", "pt_BR", [], [], Guid.NewGuid().ToString("N"), CancellationToken.None);

        Assert.Equal(0, gateway.TemplateCalls);
        Assert.Empty(await db.WhatsAppMessages.ToListAsync());
    }

    [Fact]
    public async Task Transfer_requires_manager_permission_and_preserves_messages()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var targetRole = new IdentityRole(ApplicationRoles.Seller) { Id = "seller-role", NormalizedName = ApplicationRoles.Seller.ToUpperInvariant() };
        var target = new ApplicationUser { Id = "target-seller", UserName = "target-seller", Email = "target@test.local", IsActive = true };
        db.Roles.Add(targetRole);
        db.Users.Add(target);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = target.Id, RoleId = targetRole.Id });
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", Status = WhatsAppConversationStatus.Open };
        var message = new WhatsAppMessage { Conversation = conversation, Direction = WhatsAppMessageDirection.Inbound, Type = WhatsAppMessageType.Text, TextBody = "Histórico" };
        db.AddRange(conversation, message);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway(), ApplicationRoles.CommercialManager);

        await controller.Transfer(conversation.Id, target.Id, CancellationToken.None);

        var savedConversation = await db.WhatsAppConversations.AsNoTracking().SingleAsync();
        Assert.Equal(target.Id, savedConversation.AssignedUserId);
        Assert.Equal(WhatsAppConversationStatus.Open, savedConversation.Status);
        Assert.Equal("Histórico", (await db.WhatsAppMessages.AsNoTracking().SingleAsync()).TextBody);
    }

    [Fact]
    public async Task Seller_cannot_transfer_a_conversation_even_when_it_is_in_scope()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", Status = WhatsAppConversationStatus.Open };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway(), ApplicationRoles.Seller, "seller");

        Assert.IsType<ForbidResult>(await controller.Transfer(conversation.Id, "target", CancellationToken.None));
    }

    [Fact]
    public async Task Template_catalog_is_not_returned_for_a_conversation_outside_seller_scope()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", LastInboundAt = DateTime.UtcNow.AddHours(-25) };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingGateway();
        var controller = CreateController(db, gateway, ApplicationRoles.Seller, "seller");

        Assert.IsType<ForbidResult>(await controller.Templates(conversation.Id, CancellationToken.None));
        Assert.Equal(0, gateway.TemplateCatalogCalls);
    }

    [Fact]
    public async Task Closing_and_reopening_keeps_the_existing_message_history()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000", Status = WhatsAppConversationStatus.Open };
        db.WhatsAppMessages.Add(new WhatsAppMessage
        {
            Conversation = conversation,
            Direction = WhatsAppMessageDirection.Inbound,
            Type = WhatsAppMessageType.Text,
            TextBody = "Histórico preservado"
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new TrackingGateway());

        await controller.Close(conversation.Id, CancellationToken.None);
        await controller.Reopen(conversation.Id, CancellationToken.None);

        Assert.Equal(WhatsAppConversationStatus.Pending, (await db.WhatsAppConversations.AsNoTracking().SingleAsync()).Status);
        Assert.Equal("Histórico preservado", (await db.WhatsAppMessages.AsNoTracking().SingleAsync()).TextBody);
    }

    [Fact]
    public async Task Meta_template_catalog_uses_official_endpoint_and_returns_only_approved_templates()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
                {"data":[
                  {"name":"order_ready","status":"APPROVED","category":"UTILITY","language":"pt_BR","components":[{"type":"BODY","text":"Pedido {{1}} pronto"}]},
                  {"name":"draft_template","status":"PENDING","category":"UTILITY","language":"pt_BR","components":[{"type":"BODY","text":"Rascunho"}]}
                ]}
                """)
        });
        var gateway = CreateGateway(handler, embeddedSignup: false);

        var templates = await gateway.GetApprovedTemplatesAsync();

        Assert.Single(templates);
        Assert.Equal("order_ready", templates[0].Name);
        Assert.Equal("pt_BR", templates[0].Language);
        Assert.Equal(1, templates[0].BodyParameterCount);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Contains("v23.0/waba-test/message_templates", handler.Request.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("fields=name,status,category,language,components", handler.Request.RequestUri.ToString(), StringComparison.Ordinal);
        Assert.Equal("Bearer test-token", handler.Request.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Meta_template_catalog_preserves_named_parameter_order_for_sending()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
                {"data":[{"name":"named_order","status":"APPROVED","category":"UTILITY","language":"pt_BR","components":[{"type":"BODY","text":"Pedido {{order_number}} de {{customer_name}}"}]}]}
                """)
        });
        var gateway = CreateGateway(handler, embeddedSignup: false);

        var template = Assert.Single(await gateway.GetApprovedTemplatesAsync());
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(template));

        Assert.Equal(new[] { "order_number", "customer_name" }, payload.RootElement.GetProperty("BodyParameterNames").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task Meta_template_send_uses_phone_endpoint_and_supplied_body_parameters()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"messages":[{"id":"wamid.template-test"}]}""")
        });
        var gateway = CreateGateway(handler, embeddedSignup: false);

        var result = await gateway.SendTemplateAsync(
            "5511999990000",
            "order_ready",
            "pt_BR",
            ["12345"],
            []);

        Assert.True(result.Succeeded);
        Assert.Equal("wamid.template-test", result.ExternalMessageId);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/v23.0/phone-test/messages", handler.Request.RequestUri!.AbsolutePath);
        using var payload = JsonDocument.Parse(handler.Body!);
        var template = payload.RootElement.GetProperty("template");
        Assert.Equal("order_ready", template.GetProperty("name").GetString());
        Assert.Equal("pt_BR", template.GetProperty("language").GetProperty("code").GetString());
        Assert.Equal("12345", template.GetProperty("components")[0].GetProperty("parameters")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Meta_gateway_marks_server_error_as_unknown_delivery_outcome()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = JsonContent("""{"error":{"code":1,"message":"temporary failure"}}""")
        });
        var gateway = CreateGateway(handler, embeddedSignup: false);

        var result = await gateway.SendTextAsync("5511999990000", "Teste");

        Assert.False(result.Succeeded);
        Assert.Equal("UNKNOWN", result.ErrorCode);
    }

    [Fact]
    public async Task Meta_template_send_includes_names_for_named_text_parameters()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"messages":[{"id":"wamid.named-template-test"}]}""")
        });
        var gateway = CreateGateway(handler, embeddedSignup: false);

        var result = await gateway.SendTemplateAsync(
            "5511999990000",
            "named_order",
            "pt_BR",
            ["12345"],
            [],
            bodyParameterNames: ["order_number"]);

        Assert.True(result.Succeeded);
        using var payload = JsonDocument.Parse(handler.Body!);
        var parameter = payload.RootElement.GetProperty("template").GetProperty("components")[0].GetProperty("parameters")[0];
        Assert.Equal("order_number", parameter.GetProperty("parameter_name").GetString());
        Assert.Equal("12345", parameter.GetProperty("text").GetString());
    }

    private static MetaWhatsAppBusinessGateway CreateGateway(RecordingHandler handler, bool embeddedSignup)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var options = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            EmbeddedSignupEnabled = embeddedSignup,
            GraphApiVersion = "v23.0",
            BusinessAccountId = "waba-test",
            PhoneNumberId = "phone-test",
            AccessToken = "test-token"
        });
        return new MetaWhatsAppBusinessGateway(
            http,
            options,
            NullLogger<MetaWhatsAppBusinessGateway>.Instance,
            new FixedAccessTokenProvider(),
            new FixedPhoneNumberIdProvider(),
            new FixedBusinessAccountIdProvider());
    }

    private static WhatsAppController CreateController(
        ApplicationDbContext db,
        TrackingGateway gateway,
        string role = ApplicationRoles.Operator,
        string userId = "operator",
        TimeProvider? timeProvider = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "TestAuth"))
        };

        return new WhatsAppController(db, new SalesRepresentativeAccessService(db), gateway,
            Options.Create(new WhatsAppBusinessOptions { Enabled = true }), timeProvider: timeProvider)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object)
        };
    }

    private static StringContent JsonContent(string value) => new(value, Encoding.UTF8, "application/json");

    private sealed class FixedAccessTokenProvider : IWhatsAppAccessTokenProvider
    {
        public Task<string> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-token");
    }

    private sealed class FixedPhoneNumberIdProvider : IWhatsAppPhoneNumberIdProvider
    {
        public Task<string> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult("phone-test");
    }

    private sealed class FixedBusinessAccountIdProvider : IWhatsAppBusinessAccountIdProvider
    {
        public Task<string> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult("waba-test");
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    private sealed class TrackingGateway : IWhatsAppBusinessGateway
    {
        public int TextCalls { get; private set; }
        public int TemplateCalls { get; private set; }
        public int TemplateCatalogCalls { get; private set; }
        public IReadOnlyList<string>? LastBodyParameterNames { get; private set; }
        public IReadOnlyList<WhatsAppApprovedTemplate> Templates { get; init; } = [];
        public WhatsAppSendResult NextResult { get; init; } = new(true, "wamid.test-1", null, null);
        public Exception? NextException { get; init; }

        public Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken = default)
        {
            TextCalls++;
            return NextException is null
                ? Task.FromResult(NextResult)
                : Task.FromException<WhatsAppSendResult>(NextException);
        }

        public Task<IReadOnlyList<WhatsAppApprovedTemplate>> GetApprovedTemplatesAsync(CancellationToken cancellationToken = default)
        {
            TemplateCatalogCalls++;
            return Task.FromResult(Templates);
        }

        public Task<WhatsAppSendResult> SendTemplateAsync(
            string phoneNumber,
            string templateName,
            string language,
            IReadOnlyList<string> bodyParameters,
            IReadOnlyList<string> headerParameters,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? bodyParameterNames = null,
            IReadOnlyList<string>? headerParameterNames = null)
        {
            TemplateCalls++;
            LastBodyParameterNames = bodyParameterNames;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
