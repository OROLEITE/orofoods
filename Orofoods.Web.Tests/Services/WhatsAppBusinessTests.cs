using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Controllers.Api.V1;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Storage;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class WhatsAppBusinessTests
{
    [Fact]
    public void WhatsApp_access_token_protector_round_trips_without_persisting_plaintext()
    {
        const string accessToken = "coexistence-access-token-test-value";
        var protector = new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider());

        var protectedToken = protector.Protect(accessToken);

        Assert.NotEqual(accessToken, protectedToken);
        Assert.Equal(accessToken, protector.Unprotect(protectedToken));
    }

    [Fact]
    public async Task WhatsApp_access_token_provider_prefers_decrypted_connected_account_token()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var protector = new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider());
        db.WhatsAppBusinessConnections.Add(new WhatsAppBusinessConnection
        {
            Id = 1,
            BusinessAccountId = "waba-test",
            PhoneNumberId = "phone-test",
            DisplayPhoneNumber = "+5548920077785",
            ProtectedAccessToken = protector.Protect("connected-token-private"),
            ConnectedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var provider = new WhatsAppAccessTokenProvider(db, protector, Options.Create(new WhatsAppBusinessOptions
        {
            EmbeddedSignupEnabled = true,
            AccessToken = "legacy-token",
            EmbeddedSignupPhoneNumberId = "phone-test",
            EmbeddedSignupDisplayPhoneNumber = "+55 (48) 92007-7785"
        }));
        var phoneProvider = new WhatsAppPhoneNumberIdProvider(db, Options.Create(new WhatsAppBusinessOptions
        {
            EmbeddedSignupEnabled = true,
            PhoneNumberId = "legacy-phone-id",
            EmbeddedSignupPhoneNumberId = "phone-test",
            EmbeddedSignupDisplayPhoneNumber = "+55 (48) 92007-7785"
        }));

        var token = await provider.GetAsync();

        Assert.Equal("connected-token-private", token);
        Assert.Equal("phone-test", await phoneProvider.GetAsync());
        Assert.DoesNotContain("connected-token-private", db.ChangeTracker.Entries<WhatsAppBusinessConnection>().Single().Entity.ProtectedAccessToken);
    }

    [Fact]
    public async Task Legacy_cloud_api_options_take_precedence_over_a_persisted_connection_when_coexistence_is_not_opted_in()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var protector = new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider());
        db.WhatsAppBusinessConnections.Add(new WhatsAppBusinessConnection
        {
            Id = WhatsAppBusinessConnection.SingletonId,
            BusinessAccountId = "waba-connected",
            PhoneNumberId = "coexistence-phone-id",
            DisplayPhoneNumber = "+5548920077785",
            ProtectedAccessToken = protector.Protect("coexistence-token")
        });
        await db.SaveChangesAsync();
        var options = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            PhoneNumberId = "legacy-phone-id",
            AccessToken = "legacy-access-token"
        });
        var tokenProvider = new WhatsAppAccessTokenProvider(db, protector, options);
        var phoneProvider = new WhatsAppPhoneNumberIdProvider(db, options);

        Assert.Equal("legacy-access-token", await tokenProvider.GetAsync());
        Assert.Equal("legacy-phone-id", await phoneProvider.GetAsync());
    }

    [Fact]
    public async Task Legacy_cloud_api_providers_do_not_query_coexistence_tables_when_the_feature_is_disabled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(dbOptions);
        var settings = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            AccessToken = "legacy-access-token",
            PhoneNumberId = "legacy-phone-id"
        });
        var protector = new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider());
        var tokenProvider = new WhatsAppAccessTokenProvider(db, protector, settings);
        var phoneProvider = new WhatsAppPhoneNumberIdProvider(db, settings);

        Assert.Equal("legacy-access-token", await tokenProvider.GetAsync());
        Assert.Equal("legacy-phone-id", await phoneProvider.GetAsync());
    }

    [Fact]
    public async Task Embedded_signup_providers_reject_missing_connection_without_legacy_fallback()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var protector = new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider());
        var settings = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            GraphApiVersion = "v23.0",
            EmbeddedSignupEnabled = true,
            AccessToken = "legacy-token",
            PhoneNumberId = "legacy-phone-id",
            EmbeddedSignupPhoneNumberId = "configured-phone-id",
            EmbeddedSignupDisplayPhoneNumber = "+5548920077785"
        });
        var tokenProvider = new WhatsAppAccessTokenProvider(db, protector, settings);
        var phoneProvider = new WhatsAppPhoneNumberIdProvider(db, settings);

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokenProvider.GetAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => phoneProvider.GetAsync());

        var handler = new FakeHandler();
        var gateway = new MetaWhatsAppBusinessGateway(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            settings,
            NullLogger<MetaWhatsAppBusinessGateway>.Instance,
            tokenProvider,
            phoneProvider);
        var sendResult = await gateway.SendTextAsync("5511999990000", "Teste");

        Assert.False(sendResult.Succeeded);
        Assert.Equal("NOT_CONFIGURED", sendResult.ErrorCode);
        Assert.Null(handler.Method);
    }

    [Theory]
    [InlineData("different-phone-id", "+5548920077785")]
    [InlineData("phone-test", "+5548999999999")]
    public async Task Embedded_signup_providers_reject_connection_for_a_different_configured_number(
        string connectedPhoneNumberId,
        string connectedDisplayPhoneNumber)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var protector = new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider());
        db.WhatsAppBusinessConnections.Add(new WhatsAppBusinessConnection
        {
            Id = WhatsAppBusinessConnection.SingletonId,
            BusinessAccountId = "waba-test",
            PhoneNumberId = connectedPhoneNumberId,
            DisplayPhoneNumber = connectedDisplayPhoneNumber,
            ProtectedAccessToken = protector.Protect("connected-token-private")
        });
        await db.SaveChangesAsync();
        var settings = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            GraphApiVersion = "v23.0",
            EmbeddedSignupEnabled = true,
            AccessToken = "legacy-token",
            PhoneNumberId = "legacy-phone-id",
            EmbeddedSignupPhoneNumberId = "phone-test",
            EmbeddedSignupDisplayPhoneNumber = "+55 (48) 92007-7785"
        });
        var tokenProvider = new WhatsAppAccessTokenProvider(db, protector, settings);
        var phoneProvider = new WhatsAppPhoneNumberIdProvider(db, settings);

        await Assert.ThrowsAsync<InvalidOperationException>(() => tokenProvider.GetAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => phoneProvider.GetAsync());

        var handler = new FakeHandler();
        var gateway = new MetaWhatsAppBusinessGateway(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            settings,
            NullLogger<MetaWhatsAppBusinessGateway>.Instance,
            tokenProvider,
            phoneProvider);
        var sendResult = await gateway.SendTextAsync("5511999990000", "Teste");

        Assert.False(sendResult.Succeeded);
        Assert.Equal("NOT_CONFIGURED", sendResult.ErrorCode);
        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task WhatsApp_access_token_provider_fails_closed_for_unreadable_connected_token()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppBusinessConnections.Add(new WhatsAppBusinessConnection
        {
            Id = 1,
            BusinessAccountId = "waba-test",
            PhoneNumberId = "phone-test",
            DisplayPhoneNumber = "+5548920077785",
            ProtectedAccessToken = "not-a-valid-protected-token",
            ConnectedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var protector = new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider());
        var settings = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            GraphApiVersion = "v23.0",
            EmbeddedSignupEnabled = true,
            AccessToken = "legacy-token",
            PhoneNumberId = "legacy-phone-id",
            EmbeddedSignupPhoneNumberId = "phone-test",
            EmbeddedSignupDisplayPhoneNumber = "+5548920077785"
        });
        var provider = new WhatsAppAccessTokenProvider(db, protector, settings);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync());

        Assert.DoesNotContain("not-a-valid-protected-token", exception.Message);
        Assert.DoesNotContain("legacy-token", exception.Message);

        var handler = new FakeHandler();
        var gateway = new MetaWhatsAppBusinessGateway(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            settings,
            NullLogger<MetaWhatsAppBusinessGateway>.Instance,
            provider,
            new WhatsAppPhoneNumberIdProvider(db, settings));
        var sendResult = await gateway.SendTextAsync("5511999990000", "Teste");

        Assert.False(sendResult.Succeeded);
        Assert.Equal("NOT_CONFIGURED", sendResult.ErrorCode);
        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task Webhook_get_verification_returns_challenge_for_matching_token()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = CreateWebhookController(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, VerifyToken = "test-verify-token" }));

        var result = controller.Verify("subscribe", "test-verify-token", "challenge-value");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("challenge-value", content.Content);
    }

    [Fact]
    public async Task Webhook_get_verification_rejects_invalid_token_and_missing_challenge()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = CreateWebhookController(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, VerifyToken = "test-verify-token" }));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(controller.Verify("subscribe", "wrong-token", "challenge-value")).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(controller.Verify("subscribe", "test-verify-token", null)).StatusCode);
    }

    [Fact]
    public async Task Webhook_get_verification_is_disabled_without_revealing_configuration()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = CreateWebhookController(db, Options.Create(new WhatsAppBusinessOptions { Enabled = false, VerifyToken = "test-verify-token" }));

        Assert.IsType<NotFoundResult>(controller.Verify("subscribe", "wrong-token", "challenge-value"));
    }

    [Fact]
    public async Task Webhook_post_accepts_valid_signature_and_configured_phone_and_waba()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var body = BuildWebhookMessageJson("message-valid");
        var controller = CreateWebhookController(db, Options.Create(TestWebhookOptions()), body);
        SignWebhookRequest(controller, body);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Single(db.WhatsAppConversations);
        Assert.Single(db.WhatsAppMessages);
    }

    [Fact]
    public async Task Webhook_post_acknowledges_sequential_duplicate_as_successful_replay()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var body = BuildWebhookMessageJson("sequential-replay");
        var first = CreateWebhookController(db, Options.Create(TestWebhookOptions()), body);
        SignWebhookRequest(first, body);
        var second = CreateWebhookController(db, Options.Create(TestWebhookOptions()), body);
        SignWebhookRequest(second, body);

        Assert.IsType<OkResult>(await first.Receive(CancellationToken.None));
        Assert.IsType<OkResult>(await second.Receive(CancellationToken.None));
        Assert.Single(await db.WhatsAppMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Webhook_post_rejects_invalid_signature_without_processing_body()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payload = BuildWebhookMessageJson("message-invalid-signature");
        var controller = CreateWebhookController(db, Options.Create(TestWebhookOptions()), payload);
        controller.HttpContext.Request.Headers["X-Hub-Signature-256"] = "sha256=invalid";

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(db.WhatsAppConversations);
        Assert.Empty(db.WhatsAppMessages);
        Assert.Empty(db.WhatsAppWebhookDeliveries);
    }

    [Fact]
    public async Task Webhook_post_rejects_invalid_json_and_unexpected_structure_without_partial_writes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        foreach (var body in new[]
        {
            "{",
            "{\"object\":\"whatsapp_business_account\",\"entry\":[{\"id\":\"waba-test\",\"changes\":[{\"value\":{\"metadata\":{\"phone_number_id\":\"phone-test\"},\"messages\":[{\"id\":\"missing-fields\"}]}}]}]}"
        })
        {
            var controller = CreateWebhookController(db, Options.Create(TestWebhookOptions()), body);
            SignWebhookRequest(controller, body);

            Assert.IsType<BadRequestResult>(await controller.Receive(CancellationToken.None));
        }

        Assert.Empty(db.WhatsAppConversations);
        Assert.Empty(db.WhatsAppMessages);
        Assert.Empty(db.WhatsAppWebhookDeliveries);
    }

    [Fact]
    public async Task Webhook_post_rejects_mismatched_phone_number_id_and_waba_without_persisting()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        foreach (var body in new[]
        {
            BuildWebhookMessageJson("wrong-phone", phoneNumberId: "other-phone"),
            BuildWebhookMessageJson("wrong-waba", wabaId: "other-waba")
        })
        {
            var controller = CreateWebhookController(db, Options.Create(TestWebhookOptions()), body);
            SignWebhookRequest(controller, body);

            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await controller.Receive(CancellationToken.None)).StatusCode);
        }

        Assert.Empty(db.WhatsAppConversations);
        Assert.Empty(db.WhatsAppMessages);
        Assert.Empty(db.WhatsAppWebhookDeliveries);
    }

    [Fact]
    public async Task Webhook_post_rejects_oversized_body_before_signature_or_processing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = CreateWebhookController(db, Options.Create(TestWebhookOptions()));
        controller.HttpContext.Request.Body = new MemoryStream(new byte[WhatsAppWebhookController.MaxPayloadBytes + 1]);
        controller.HttpContext.Request.ContentLength = null;

        var result = await controller.Receive(CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, status.StatusCode);
        Assert.Empty(db.WhatsAppMessages);
    }

    [Fact]
    public async Task Webhook_post_is_disabled_without_processing_payload()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var payload = BuildWebhookMessageJson("disabled-message");
        var controller = CreateWebhookController(db, Options.Create(new WhatsAppBusinessOptions { Enabled = false }), payload);

        Assert.IsType<NotFoundResult>(await controller.Receive(CancellationToken.None));
        Assert.Empty(db.WhatsAppMessages);
    }

    [Fact]
    public async Task Webhook_post_acknowledges_unsupported_event_without_writing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var body = "{\"object\":\"whatsapp_business_account\",\"entry\":[{\"id\":\"waba-test\",\"changes\":[{\"field\":\"account_update\",\"value\":{}}]}]}";
        var controller = CreateWebhookController(db, Options.Create(TestWebhookOptions()), body);
        SignWebhookRequest(controller, body);

        Assert.IsType<OkResult>(await controller.Receive(CancellationToken.None));
        Assert.Empty(db.WhatsAppConversations);
        Assert.Empty(db.WhatsAppMessages);
    }

    [Fact]
    public async Task Smb_message_echo_is_saved_as_outbound_in_the_recipient_conversation_and_deduplicated()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var body = BuildCoexistenceWebhookJson("smb_message_echoes", new
        {
            messages = new[] { new { id = "wamid.echo.1", from = "5548920077785", to = "5548999999999", timestamp = "1730000000", type = "text", text = new { body = "Resposta pelo celular" } } }
        });

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var controller = CreateWebhookController(db, Options.Create(TestWebhookOptions()), body);
            SignWebhookRequest(controller, body);
            Assert.IsType<OkResult>(await controller.Receive(CancellationToken.None));
        }

        var conversation = Assert.Single(await db.WhatsAppConversations.AsNoTracking().ToListAsync());
        var message = Assert.Single(await db.WhatsAppMessages.AsNoTracking().ToListAsync());
        Assert.Equal("5548999999999", conversation.PhoneNumber);
        Assert.Equal(WhatsAppMessageDirection.Outbound, message.Direction);
        Assert.Equal("Resposta pelo celular", message.TextBody);
        Assert.Equal(0, conversation.UnreadCount);
    }

    [Fact]
    public async Task Coexistence_history_is_ignored_by_the_legacy_webhook_path_when_not_opted_in()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var body = BuildCoexistenceWebhookJson("history", new
        {
            history = new[] { new { messages = new[] { new { id = "wamid.disabled-history", from = "5548999999999", type = "text", text = new { body = "Não importar" } } } } }
        });
        var options = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            PhoneNumberId = "phone-test",
            BusinessAccountId = "waba-test",
            AppSecret = "test-only-app-secret"
        });
        var controller = CreateWebhookController(db, options, body);
        SignWebhookRequest(controller, body);

        Assert.IsType<OkResult>(await controller.Receive(CancellationToken.None));
        Assert.Empty(await db.WhatsAppWebhookDeliveries.ToListAsync());
        Assert.Empty(await db.WhatsAppMessages.ToListAsync());
    }

    [Fact]
    public async Task Legacy_webhook_acknowledges_non_message_events_without_coexistence_tables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(dbOptions);
        var body = "{\"object\":\"whatsapp_business_account\",\"entry\":[{\"id\":\"waba-test\",\"changes\":[{\"field\":\"account_update\",\"value\":{}}]}]}";
        var options = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            BusinessAccountId = "waba-test",
            PhoneNumberId = "phone-test",
            AppSecret = "test-only-app-secret"
        });
        var controller = CreateWebhookController(db, options, body);
        SignWebhookRequest(controller, body);

        Assert.IsType<OkResult>(await controller.Receive(CancellationToken.None));
    }

    [Fact]
    public async Task Coexistence_echo_uses_the_legacy_inbound_interpretation_when_not_opted_in()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var body = BuildCoexistenceWebhookJson("smb_message_echoes", new
        {
            messages = new[] { new { id = "wamid.echo.legacy", from = "5548920077785", to = "5548999999999", type = "text", text = new { body = "Eco ignorado como recurso novo" } } }
        });
        var options = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            PhoneNumberId = "phone-test",
            BusinessAccountId = "waba-test",
            AppSecret = "test-only-app-secret"
        });
        var controller = CreateWebhookController(db, options, body);
        SignWebhookRequest(controller, body);

        Assert.IsType<OkResult>(await controller.Receive(CancellationToken.None));
        var message = await db.WhatsAppMessages.AsNoTracking().SingleAsync();
        Assert.Equal(WhatsAppMessageDirection.Inbound, message.Direction);
        Assert.Equal("5548920077785", (await db.WhatsAppConversations.AsNoTracking().SingleAsync()).PhoneNumber);
    }

    [Fact]
    public async Task History_event_imports_message_using_customer_phone_and_message_id_deduplication()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var body = BuildCoexistenceWebhookJson("history", new
        {
            history = new[] { new { messages = new[] { new { id = "wamid.history.1", from = "5548999999999", timestamp = "1730000000", type = "text", text = new { body = "Histórico sincronizado" } } } } }
        });

        var protection = new EphemeralDataProtectionProvider();
        var options = Options.Create(TestWebhookOptions());
        var service = new WhatsAppConversationService(db, options, new UserNotificationService(db));
        var queue = new WhatsAppWebhookDeliveryQueue(db, protection);
        var controller = CreateWebhookController(service, options, queue, body);
        SignWebhookRequest(controller, body);
        Assert.IsType<OkResult>(await controller.Receive(CancellationToken.None));
        var replay = CreateWebhookController(service, options, queue, body);
        SignWebhookRequest(replay, body);
        Assert.IsType<OkResult>(await replay.Receive(CancellationToken.None));
        Assert.Single(await db.WhatsAppWebhookDeliveries.ToListAsync());
        Assert.True(await new WhatsAppWebhookDeliveryProcessor(db, service, protection).ProcessNextAsync());

        var message = Assert.Single(await db.WhatsAppMessages.AsNoTracking().ToListAsync());
        Assert.Equal(WhatsAppMessageDirection.Inbound, message.Direction);
        var conversation = await db.WhatsAppConversations.AsNoTracking().SingleAsync();
        Assert.Equal("5548999999999", conversation.PhoneNumber);
        Assert.Equal(0, conversation.UnreadCount);
        Assert.Equal("Histórico sincronizado", message.TextBody);
    }

    [Fact]
    public async Task Smb_app_state_sync_event_is_acknowledged_without_creating_a_customer_message()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var body = BuildCoexistenceWebhookJson("smb_app_state_sync", new { });
        var options = Options.Create(TestWebhookOptions());
        var logger = new CapturingLogger<WhatsAppConversationService>();
        var service = new WhatsAppConversationService(db, options, new UserNotificationService(db), logger: logger);
        var protection = new EphemeralDataProtectionProvider();
        var queue = new WhatsAppWebhookDeliveryQueue(db, protection);
        var controller = new WhatsAppWebhookController(service, options, queue)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        SignWebhookRequest(controller, body);

        Assert.IsType<OkResult>(await controller.Receive(CancellationToken.None));
        Assert.Empty(await db.WhatsAppConversations.ToListAsync());
        Assert.Empty(await db.WhatsAppMessages.ToListAsync());
        Assert.Contains(logger.Entries, entry => entry.EventId.Name == "WhatsApp.MetaStateSyncDisabled" && entry.Message.Contains("HTTP 429", StringComparison.Ordinal));
        var deferred = await db.WhatsAppWebhookDeliveries.SingleAsync();
        Assert.Equal(WhatsAppWebhookDeliveryStatus.Blocked, deferred.Status);
        Assert.Equal("SMB_STATE_SYNC_DOCS_UNAVAILABLE", deferred.LastErrorCode);
        Assert.Equal(body, Encoding.UTF8.GetString(protection.CreateProtector("Orofoods.WhatsAppBusiness.HistoryPayload.v1").Unprotect(deferred.ProtectedPayload!)));
        Assert.False(await new WhatsAppWebhookDeliveryProcessor(db, service, protection).ProcessNextAsync());
    }

    [Fact]
    public async Task Coexistence_webhook_must_match_business_account_saved_by_embedded_signup()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppBusinessConnections.Add(new WhatsAppBusinessConnection
        {
            BusinessAccountId = "connected-waba",
            PhoneNumberId = "phone-test",
            DisplayPhoneNumber = "+5548920077785",
            ProtectedAccessToken = "protected"
        });
        await db.SaveChangesAsync();
        var body = BuildWebhookMessageJson("wrong-connected-waba", wabaId: "waba-test");
        var controller = CreateWebhookController(db, Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            EmbeddedSignupEnabled = true,
            PhoneNumberId = "phone-test",
            AppSecret = "test-only-app-secret"
        }), body);
        SignWebhookRequest(controller, body);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await controller.Receive(CancellationToken.None)).StatusCode);
        Assert.Empty(await db.WhatsAppMessages.ToListAsync());
    }

    [Theory]
    [InlineData("+55 48 99999-9999", "5548999999999")]
    [InlineData("5548999999999", "5548999999999")]
    [InlineData("(48) 99999-9999", "5548999999999")]
    [InlineData("48 3333-9999", "554833339999")]
    [InlineData("+1 (202) 555-0100", "12025550100")]
    [InlineData("0 48 99999-9999", "5548999999999")]
    public void Phone_normalization_handles_supported_explicit_and_brazilian_national_forms(string input, string expected)
    {
        Assert.Equal(expected, WhatsAppConversationService.TryNormalizePhone(input));
    }

    [Theory]
    [InlineData("99999-9999")]
    [InlineData("not-a-phone")]
    [InlineData("+000 1234 5678")]
    [InlineData("48+3333-9999")]
    public void Phone_normalization_does_not_invent_missing_ddd_or_country(string input)
    {
        Assert.Null(WhatsAppConversationService.TryNormalizePhone(input));
    }

    [Fact]
    public async Task Formatted_customer_phone_is_normalized_for_unique_association()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.Add(new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", WhatsApp = "+55 48 99999-9999", IsActive = true });
        await db.SaveChangesAsync();
        var service = CreateConversationService(db);
        using var document = JsonDocument.Parse(BuildWebhookMessageJson("formatted-customer", from: "5548999999999"));

        await service.ProcessAsync(document);

        Assert.Equal(db.Customers.Single().Id, Assert.Single(db.WhatsAppConversations).CustomerId);
    }

    [Fact]
    public async Task Formatted_customer_phone_field_is_normalized_for_unique_association()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.Add(new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", Phone = "(48) 99999-9999", IsActive = true });
        await db.SaveChangesAsync();
        var service = CreateConversationService(db);
        using var document = JsonDocument.Parse(BuildWebhookMessageJson("formatted-customer-phone", from: "+55 48 99999-9999"));

        await service.ProcessAsync(document);

        Assert.Equal(db.Customers.Single().Id, Assert.Single(db.WhatsAppConversations).CustomerId);
    }

    [Fact]
    public async Task Concurrent_replay_creates_one_message_and_returns_without_duplicate_exception()
    {
        var connection = new SqliteConnection("Data Source=whatsapp-concurrency;Mode=Memory;Cache=Shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var firstDb = new ApplicationDbContext(options);
        await firstDb.Database.EnsureCreatedAsync();
        await using var secondDb = new ApplicationDbContext(options);
        var payload = BuildWebhookMessageJson("same-concurrent-id");
        var firstController = CreateWebhookController(firstDb, Options.Create(TestWebhookOptions()), payload);
        var secondController = CreateWebhookController(secondDb, Options.Create(TestWebhookOptions()), payload);
        SignWebhookRequest(firstController, payload);
        SignWebhookRequest(secondController, payload);

        var responses = await Task.WhenAll(firstController.Receive(CancellationToken.None), secondController.Receive(CancellationToken.None));

        Assert.All(responses, result => Assert.IsType<OkResult>(result));
        Assert.Single(await firstDb.WhatsAppMessages.AsNoTracking().ToListAsync());
        Assert.Single(await firstDb.WhatsAppConversations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Status_updates_are_monotonic_and_duplicate_statuses_are_idempotent()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var message = new WhatsAppMessage
        {
            Conversation = new WhatsAppConversation { PhoneNumber = "5548999999999" },
            ExternalMessageId = "status-message",
            Direction = WhatsAppMessageDirection.Outbound,
            Type = WhatsAppMessageType.Text,
            Status = WhatsAppMessageStatus.Sent
        };
        db.WhatsAppMessages.Add(message);
        await db.SaveChangesAsync();
        var service = CreateConversationService(db);

        await ProcessStatusAsync(service, "status-message", "delivered", "1750000000");
        var deliveredAt = await db.WhatsAppMessages.AsNoTracking().Select(x => x.DeliveredAt).SingleAsync();
        await ProcessStatusAsync(service, "status-message", "delivered", "1750001000");
        await ProcessStatusAsync(service, "status-message", "sent", "1749999000");
        Assert.Equal(WhatsAppMessageStatus.Delivered, await db.WhatsAppMessages.AsNoTracking().Select(x => x.Status).SingleAsync());
        Assert.Equal(deliveredAt, await db.WhatsAppMessages.AsNoTracking().Select(x => x.DeliveredAt).SingleAsync());

        await ProcessStatusAsync(service, "status-message", "read", "1750002000");
        await ProcessStatusAsync(service, "status-message", "delivered", "1750001000");
        Assert.Equal(WhatsAppMessageStatus.Read, await db.WhatsAppMessages.AsNoTracking().Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Sent_status_is_applied_only_from_the_meta_status_event()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppMessages.Add(new WhatsAppMessage
        {
            Conversation = new WhatsAppConversation { PhoneNumber = "5548999999999" },
            ExternalMessageId = "pending-status-message",
            Direction = WhatsAppMessageDirection.Outbound,
            Type = WhatsAppMessageType.Text,
            Status = WhatsAppMessageStatus.Pending
        });
        await db.SaveChangesAsync();
        var service = CreateConversationService(db);

        await ProcessStatusAsync(service, "pending-status-message", "sent", "1750000000");

        var message = await db.WhatsAppMessages.AsNoTracking().SingleAsync();
        Assert.Equal(WhatsAppMessageStatus.Sent, message.Status);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1750000000).UtcDateTime, message.SentAt);
        Assert.Null(message.DeliveredAt);
        Assert.Null(message.ReadAt);
    }

    [Fact]
    public async Task Failed_status_is_terminal_and_duplicate_failed_event_does_not_change_timestamp()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppMessages.Add(new WhatsAppMessage
        {
            Conversation = new WhatsAppConversation { PhoneNumber = "5548999999999" },
            ExternalMessageId = "failed-status-message",
            Direction = WhatsAppMessageDirection.Outbound,
            Type = WhatsAppMessageType.Text,
            Status = WhatsAppMessageStatus.Sent
        });
        await db.SaveChangesAsync();
        var service = CreateConversationService(db);

        await ProcessStatusAsync(service, "failed-status-message", "failed", "1750003000");
        var failedAt = await db.WhatsAppMessages.AsNoTracking().Select(x => x.FailedAt).SingleAsync();
        await ProcessStatusAsync(service, "failed-status-message", "failed", "1750004000");
        await ProcessStatusAsync(service, "failed-status-message", "read", "1750005000");

        Assert.Equal(WhatsAppMessageStatus.Failed, await db.WhatsAppMessages.AsNoTracking().Select(x => x.Status).SingleAsync());
        Assert.Equal(failedAt, await db.WhatsAppMessages.AsNoTracking().Select(x => x.FailedAt).SingleAsync());
    }

    [Fact]
    public async Task Failed_webhook_status_persists_primary_meta_error_with_sensitive_values_redacted()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppMessages.Add(new WhatsAppMessage
        {
            Conversation = new WhatsAppConversation { PhoneNumber = "5548999999999" },
            ExternalMessageId = "failed-details-message",
            Direction = WhatsAppMessageDirection.Outbound,
            Type = WhatsAppMessageType.Text,
            Status = WhatsAppMessageStatus.Sent
        });
        await db.SaveChangesAsync();
        var logger = new CapturingLogger<WhatsAppConversationService>();
        var service = new WhatsAppConversationService(db, Options.Create(TestWebhookOptions()), new UserNotificationService(db), logger: logger);
        var errors = new object[]
        {
            new
            {
                code = 131047,
                title = "Re-engagement message",
                message = "Recipient +55 (11) 99999-9999 rejected token=private-value",
                error_data = new { details = "Free-form message outside the window" }
            },
            new { code = 999001, title = "Secondary error", message = "Do not persist this error" }
        };

        await ProcessStatusAsync(service, "failed-details-message", "failed", "1750003000", errors);

        var message = await db.WhatsAppMessages.AsNoTracking().SingleAsync();
        Assert.Equal(WhatsAppMessageStatus.Failed, message.Status);
        Assert.Equal("131047", message.ErrorCode);
        Assert.Contains("Re-engagement message", message.ErrorMessage);
        Assert.Contains("Free-form message outside the window", message.ErrorMessage);
        Assert.DoesNotContain("99999-9999", message.ErrorMessage);
        Assert.DoesNotContain("private-value", message.ErrorMessage);
        Assert.DoesNotContain("Secondary error", message.ErrorMessage);
        var log = Assert.Single(logger.Entries);
        Assert.Equal(4301, log.EventId.Id);
        Assert.Contains("ExternalMessageId=failed-details-message", log.Message);
        Assert.Contains("MetaErrorCode=131047", log.Message);
        Assert.Contains("ConversationId=", log.Message);
        Assert.Contains("MessageId=", log.Message);
        Assert.DoesNotContain("99999-9999", log.Message);
        Assert.DoesNotContain("private-value", log.Message);
        Assert.DoesNotContain("Mensagem de teste", log.Message);
    }

    [Fact]
    public async Task Failed_webhook_status_without_errors_keeps_error_fields_empty()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppMessages.Add(new WhatsAppMessage
        {
            Conversation = new WhatsAppConversation { PhoneNumber = "5548999999999" },
            ExternalMessageId = "failed-no-details-message",
            Direction = WhatsAppMessageDirection.Outbound,
            Type = WhatsAppMessageType.Text,
            Status = WhatsAppMessageStatus.Sent
        });
        await db.SaveChangesAsync();
        var service = CreateConversationService(db);

        await ProcessStatusAsync(service, "failed-no-details-message", "failed", "1750003000");

        var message = await db.WhatsAppMessages.AsNoTracking().SingleAsync();
        Assert.Equal(WhatsAppMessageStatus.Failed, message.Status);
        Assert.Null(message.ErrorCode);
        Assert.Null(message.ErrorMessage);
    }

    [Fact]
    public async Task Duplicate_failed_webhook_does_not_replace_saved_error_details()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.WhatsAppMessages.Add(new WhatsAppMessage
        {
            Conversation = new WhatsAppConversation { PhoneNumber = "5548999999999" },
            ExternalMessageId = "failed-idempotent-details-message",
            Direction = WhatsAppMessageDirection.Outbound,
            Type = WhatsAppMessageType.Text,
            Status = WhatsAppMessageStatus.Sent
        });
        await db.SaveChangesAsync();
        var service = CreateConversationService(db);

        await ProcessStatusAsync(service, "failed-idempotent-details-message", "failed", "1750003000", [new { code = 131047, title = "First failure", message = "First reason" }]);
        await ProcessStatusAsync(service, "failed-idempotent-details-message", "failed", "1750004000", [new { code = 999001, title = "Duplicate failure", message = "Replacement reason" }]);
        await ProcessStatusAsync(service, "failed-idempotent-details-message", "read", "1750005000");

        var message = await db.WhatsAppMessages.AsNoTracking().SingleAsync();
        Assert.Equal(WhatsAppMessageStatus.Failed, message.Status);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1750003000).UtcDateTime, message.FailedAt);
        Assert.Equal("131047", message.ErrorCode);
        Assert.Contains("First reason", message.ErrorMessage);
        Assert.DoesNotContain("Replacement reason", message.ErrorMessage);
    }

    [Fact]
    public async Task Conversation_and_message_roll_back_together_when_message_save_fails()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new FailMessageInsertInterceptor())
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();
        var service = CreateConversationService(db);
        using var document = JsonDocument.Parse(BuildWebhookMessageJson("atomic-message"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(document));

        Assert.Empty(await db.WhatsAppConversations.AsNoTracking().ToListAsync());
        Assert.Empty(await db.WhatsAppMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Signed_text_webhook_creates_known_conversation_message_and_notification_once()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = new ApplicationUser { Id = "internal", UserName = "internal", Email = "internal@test.local", IsActive = true };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", WhatsApp = "5511999990000", Status = CustomerStatus.Approved, IsActive = true, InternalSalesUserId = user.Id };
        db.AddRange(user, customer);
        await db.SaveChangesAsync();
        var options = Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "test-app-secret", VerifyToken = "test-verify" });
        var service = new WhatsAppConversationService(db, options, new UserNotificationService(db));
        var body = "{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"id\":\"wamid.test.1\",\"from\":\"+55 (11) 99999-0000\",\"type\":\"text\",\"text\":{\"body\":\"Bom dia\"}}]}}]}]}";
        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.AppSecret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        Assert.True(service.IsValidSignature(body, signature));
        using var document = JsonDocument.Parse(body);
        await service.ProcessAsync(document);
        await service.ProcessAsync(document);

        Assert.Single(db.WhatsAppConversations);
        Assert.Single(db.WhatsAppMessages);
        Assert.Single(db.UserNotifications);
        var conversation = db.WhatsAppConversations.Single();
        Assert.Equal(customer.Id, conversation.CustomerId);
        Assert.Equal(user.Id, conversation.AssignedUserId);
        Assert.Equal(1, conversation.UnreadCount);
        Assert.Equal("5511999990000", conversation.PhoneNumber);
    }

    [Fact]
    public async Task Invalid_signature_does_not_process_payload()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = new WhatsAppConversationService(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "secret" }), new UserNotificationService(db));
        Assert.False(service.IsValidSignature("{}", "sha256=invalid"));
        Assert.Empty(db.WhatsAppConversations);
        Assert.Empty(db.WhatsAppMessages);
    }

    [Fact]
    public async Task Message_status_does_not_regress_from_read()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999990000" };
        var message = new WhatsAppMessage { Conversation = conversation, ExternalMessageId = "wamid.out.1", Direction = WhatsAppMessageDirection.Outbound, Type = WhatsAppMessageType.Text, Status = WhatsAppMessageStatus.Read };
        db.Add(message);
        await db.SaveChangesAsync();
        var service = new WhatsAppConversationService(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "secret" }), new UserNotificationService(db));
        using var document = JsonDocument.Parse("{\"entry\":[{\"changes\":[{\"value\":{\"statuses\":[{\"id\":\"wamid.out.1\",\"status\":\"delivered\",\"timestamp\":\"1750000000\"}]}}]}]}");
        await service.ProcessAsync(document);
        Assert.Equal(WhatsAppMessageStatus.Read, db.WhatsAppMessages.Single().Status);
    }

    [Fact]
    public async Task Gateway_uses_fake_http_and_parses_external_message_id_without_real_credentials()
    {
        var handler = new FakeHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var gateway = new MetaWhatsAppBusinessGateway(client, Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-test", AccessToken = "fake-token" }), NullLogger<MetaWhatsAppBusinessGateway>.Instance);
        var result = await gateway.SendTextAsync("5511999990000", "Olá");
        Assert.True(result.Succeeded);
        Assert.Equal("wamid.sent.1", result.ExternalMessageId);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Contains("v23.0/phone-test/messages", handler.RequestUri);
        Assert.DoesNotContain("fake-token", handler.Body);
        Assert.DoesNotContain("cvv", handler.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_phone_creates_unidentified_conversation_without_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = new WhatsAppConversationService(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "secret" }), new UserNotificationService(db));
        using var document = JsonDocument.Parse("{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"id\":\"wamid.unknown\",\"from\":\"5511888880000\",\"type\":\"text\",\"text\":{\"body\":\"Olá\"}}]}}]}]}");

        await service.ProcessAsync(document);

        var conversation = Assert.Single(db.WhatsAppConversations);
        Assert.Null(conversation.CustomerId);
        Assert.Null(conversation.AssignedUserId);
        Assert.Empty(db.Customers);
    }

    [Fact]
    public async Task Ambiguous_phone_does_not_auto_link_customer()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.AddRange(
            new Customer { LegalName = "A", TradeName = "A", Cnpj = "11.111.111/0001-11", WhatsApp = "5511777770000", Status = CustomerStatus.Approved, IsActive = true },
            new Customer { LegalName = "B", TradeName = "B", Cnpj = "22.222.222/0001-22", Phone = "5511777770000", Status = CustomerStatus.Approved, IsActive = true });
        await db.SaveChangesAsync();
        var service = new WhatsAppConversationService(db, Options.Create(new WhatsAppBusinessOptions { Enabled = true, AppSecret = "secret" }), new UserNotificationService(db));
        using var document = JsonDocument.Parse("{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"id\":\"wamid.ambiguous\",\"from\":\"5511777770000\",\"type\":\"text\",\"text\":{\"body\":\"Olá\"}}]}}]}]}");

        await service.ProcessAsync(document);

        Assert.Null(Assert.Single(db.WhatsAppConversations).CustomerId);
    }

    [Fact]
    public async Task Index_marks_an_authorized_conversation_as_read()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (user, conversation) = await CreateAssignedConversationAsync(db, unreadCount: 2);
        var controller = CreateController(db, user.Id, new TrackingWhatsAppGateway());

        var result = await controller.Index(conversation.Id, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, db.WhatsAppConversations.Single(x => x.Id == conversation.Id).UnreadCount);
    }

    [Fact]
    public async Task Updates_returns_each_recent_message_once_without_rendering_a_full_page()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (user, conversation) = await CreateAssignedConversationAsync(db, unreadCount: 0);
        db.WhatsAppMessages.AddRange(
            new WhatsAppMessage { ConversationId = conversation.Id, ExternalMessageId = "poll-1", Direction = WhatsAppMessageDirection.Inbound, Type = WhatsAppMessageType.Text, TextBody = "Um", CreatedAt = DateTime.UtcNow.AddSeconds(-1) },
            new WhatsAppMessage { ConversationId = conversation.Id, ExternalMessageId = "poll-2", Direction = WhatsAppMessageDirection.Inbound, Type = WhatsAppMessageType.Text, TextBody = "Dois", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = CreateController(db, user.Id, new TrackingWhatsAppGateway());

        var result = Assert.IsType<JsonResult>(await controller.Updates(conversation.Id, CancellationToken.None));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var messages = json.RootElement.GetProperty("messages").EnumerateArray().ToList();
        var conversations = json.RootElement.GetProperty("conversations").EnumerateArray().ToList();

        Assert.Equal(2, messages.Count);
        Assert.Equal(2, messages.Select(item => item.GetProperty("id").GetInt64()).Distinct().Count());
        Assert.Equal(conversation.PhoneNumber, conversations.Single().GetProperty("phoneNumber").GetString());
        Assert.Equal("no-store, no-cache, must-revalidate", controller.Response.Headers.CacheControl.ToString());
        Assert.EndsWith("Z", messages[0].GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task Send_rejects_manipulated_conversation_id_without_calling_gateway_or_persisting_message()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (userA, _) = await CreateAssignedConversationAsync(db, userId: "user-a");
        var (_, conversationB) = await CreateAssignedConversationAsync(db, userId: "user-b", phoneNumber: "5511000000002");
        var gateway = new TrackingWhatsAppGateway();
        var controller = CreateController(db, userA.Id, gateway);

        var result = await controller.Send(conversationB.Id, "Mensagem forjada", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, gateway.Calls);
        Assert.Empty(db.WhatsAppMessages);
        Assert.Equal(2, db.WhatsAppConversations.Single(x => x.Id == conversationB.Id).UnreadCount);
    }

    [Fact]
    public async Task Media_endpoint_does_not_open_storage_for_an_unauthorized_conversation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (userA, _) = await CreateAssignedConversationAsync(db, userId: "user-a");
        var (_, conversationB) = await CreateAssignedConversationAsync(db, userId: "user-b", phoneNumber: "5511000000099");
        var message = new WhatsAppMessage
        {
            ConversationId = conversationB.Id,
            ExternalMessageId = "private-media",
            Direction = WhatsAppMessageDirection.Inbound,
            Type = WhatsAppMessageType.Image,
            MimeType = "image/jpeg",
            MediaState = WhatsAppMediaState.Available,
            MediaStorageReference = "private-reference"
        };
        db.Add(message);
        await db.SaveChangesAsync();
        var storage = new TrackingMediaStorage();
        var controller = CreateController(db, userA.Id, new TrackingWhatsAppGateway(), storage);

        Assert.IsType<NotFoundResult>(await controller.Media(message.Id, CancellationToken.None));
        Assert.Equal(0, storage.OpenCalls);
    }

    [Fact]
    public async Task Send_rejects_missing_closed_and_whitespace_conversations_before_calling_gateway()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (user, openConversation) = await CreateAssignedConversationAsync(db);
        var closedConversation = new WhatsAppConversation
        {
            PhoneNumber = "5511000000003",
            AssignedUserId = user.Id,
            Status = WhatsAppConversationStatus.Closed
        };
        db.Add(closedConversation);
        await db.SaveChangesAsync();
        var gateway = new TrackingWhatsAppGateway();
        var controller = CreateController(db, user.Id, gateway);

        Assert.IsType<ForbidResult>(await controller.Send(9999, "Mensagem", CancellationToken.None));
        Assert.IsType<RedirectToActionResult>(await controller.Send(closedConversation.Id, "Mensagem", CancellationToken.None));
        Assert.IsType<RedirectToActionResult>(await controller.Send(openConversation.Id, "  ", CancellationToken.None));

        Assert.Equal(0, gateway.Calls);
        Assert.Empty(db.WhatsAppMessages);
    }

    private static async Task<(ApplicationUser User, WhatsAppConversation Conversation)> CreateAssignedConversationAsync(
        ApplicationDbContext db,
        string userId = "user-a",
        string phoneNumber = "5511000000001",
        int unreadCount = 2)
    {
        var user = new ApplicationUser { Id = userId, UserName = userId, Email = $"{userId}@test.local", IsActive = true };
        var customer = new Customer
        {
            LegalName = userId,
            TradeName = userId,
            Cnpj = userId == "user-a" ? "11.111.111/0001-11" : "22.222.222/0001-22",
            WhatsApp = phoneNumber,
            Status = CustomerStatus.Approved,
            IsActive = true,
            InternalSalesUserId = user.Id
        };
        var conversation = new WhatsAppConversation { PhoneNumber = phoneNumber, Customer = customer, AssignedUserId = user.Id, UnreadCount = unreadCount };
        db.AddRange(user, customer, conversation);
        await db.SaveChangesAsync();
        return (user, conversation);
    }

    private static WhatsAppController CreateController(ApplicationDbContext db, string userId, IWhatsAppBusinessGateway gateway, IWhatsAppMediaStorage? mediaStorage = null)
    {
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")], "TestAuth"))
        };

        return new WhatsAppController(db, new SalesRepresentativeAccessService(db), gateway,
            Options.Create(new WhatsAppBusinessOptions { Enabled = true }), mediaStorage)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object)
        };
    }

    private static WhatsAppWebhookController CreateWebhookController(
        ApplicationDbContext db,
        IOptions<WhatsAppBusinessOptions> options,
        string? body = null)
    {
        var httpContext = new DefaultHttpContext();
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentLength = bytes.Length;
        }

        var service = new WhatsAppConversationService(db, options, new UserNotificationService(db));
        return new WhatsAppWebhookController(service, options, new WhatsAppWebhookDeliveryQueue(db, new EphemeralDataProtectionProvider()))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static WhatsAppWebhookController CreateWebhookController(
        WhatsAppConversationService service,
        IOptions<WhatsAppBusinessOptions> options,
        IWhatsAppWebhookDeliveryQueue queue,
        string? body = null)
    {
        var httpContext = new DefaultHttpContext();
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentLength = bytes.Length;
        }

        return new WhatsAppWebhookController(service, options, queue)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static WhatsAppBusinessOptions TestWebhookOptions() => new()
    {
        Enabled = true,
        EmbeddedSignupEnabled = true,
        PhoneNumberId = "phone-test",
        BusinessAccountId = "waba-test",
        AppSecret = "test-only-app-secret",
        VerifyToken = "test-only-verify-token"
    };

    private static string BuildWebhookMessageJson(
        string externalId,
        string from = "5548999999999",
        string phoneNumberId = "phone-test",
        string wabaId = "waba-test") => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[]
        {
            new
            {
                id = wabaId,
                changes = new[]
                {
                    new
                    {
                        field = "messages",
                        value = new
                        {
                            metadata = new { phone_number_id = phoneNumberId },
                            messages = new[]
                            {
                                new { id = externalId, from, type = "text", text = new { body = "Mensagem de teste" } }
                            }
                        }
                    }
                }
            }
        }
    });

    private static string BuildWebhookStatusJson(string externalId, string status, string timestamp, object[]? errors = null) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[]
        {
            new
            {
                id = "waba-test",
                changes = new[]
                {
                    new
                    {
                        field = "messages",
                        value = new
                        {
                            metadata = new { phone_number_id = "phone-test" },
                            statuses = new[] { new { id = externalId, status, timestamp, errors } }
                        }
                    }
                }
            }
        }
    });

    private static string BuildCoexistenceWebhookJson(string field, object value)
    {
        var eventValue = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(value))!.AsObject();
        eventValue["metadata"] = new System.Text.Json.Nodes.JsonObject { ["phone_number_id"] = "phone-test" };
        return JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-test", changes = new[] { new { field, value = eventValue } } } }
        });
    }

    private static void SignWebhookRequest(WhatsAppWebhookController controller, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestWebhookOptions().AppSecret), bytes)).ToLowerInvariant();
        controller.HttpContext.Request.Body = new MemoryStream(bytes);
        controller.HttpContext.Request.ContentLength = bytes.Length;
        controller.HttpContext.Request.Headers["X-Hub-Signature-256"] = signature;
    }

    private static WhatsAppConversationService CreateConversationService(ApplicationDbContext db) =>
        new(db, Options.Create(TestWebhookOptions()), new UserNotificationService(db));

    private static async Task ProcessStatusAsync(WhatsAppConversationService service, string externalId, string status, string timestamp, object[]? errors = null)
    {
        using var document = JsonDocument.Parse(BuildWebhookStatusJson(externalId, status, timestamp, errors));
        await service.ProcessAsync(document);
    }

    [Fact]
    public async Task Gateway_returns_sanitized_error_for_meta_failure_and_disabled_mode()
    {
        var failingClient = new HttpClient(new FakeHandler(HttpStatusCode.Unauthorized)) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var gateway = new MetaWhatsAppBusinessGateway(failingClient, Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-test", AccessToken = "fake-token" }), NullLogger<MetaWhatsAppBusinessGateway>.Instance);
        var failed = await gateway.SendTextAsync("5511999990000", "Olá");
        Assert.False(failed.Succeeded);
        Assert.Equal("401", failed.ErrorCode);
        Assert.DoesNotContain("fake-token", failed.ErrorMessage ?? "");

        var disabled = new MetaWhatsAppBusinessGateway(new HttpClient(new FakeHandler()), Options.Create(new WhatsAppBusinessOptions { Enabled = false }), NullLogger<MetaWhatsAppBusinessGateway>.Instance);
        var disabledResult = await disabled.SendTextAsync("5511999990000", "Olá");
        Assert.Equal("DISABLED", disabledResult.ErrorCode);
    }

    [Fact]
    public async Task Gateway_logs_sanitized_meta_error_details_and_masks_request_identifiers()
    {
        const string recipient = "5511999992273";
        const string message = "texto confidencial do pedido";
        const string accessToken = "access-token-private-value";
        const string appSecret = "app-secret-private-value";
        const string verifyToken = "verify-token-private-value";
        const string phoneNumberId = "1234567890128243";
        var log = new CapturingLogger<MetaWhatsAppBusinessGateway>();
        var responseJson = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = 131047,
                type = "OAuthException",
                message = $"Rejected {message} for {recipient}; token={accessToken}; app_secret={appSecret}; verify_token={verifyToken}; Authorization: Bearer {accessToken}; phone_number_id={phoneNumberId}",
                error_subcode = 2494010,
                fbtrace_id = "AbCdEf123456"
            }
        });
        var handler = new FakeHandler(HttpStatusCode.BadRequest, responseJson);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var settings = new WhatsAppBusinessOptions
        {
            Enabled = true,
            GraphApiVersion = "v23.0",
            PhoneNumberId = phoneNumberId,
            AccessToken = accessToken,
            AppSecret = appSecret,
            VerifyToken = verifyToken
        };
        var gateway = new MetaWhatsAppBusinessGateway(client, Options.Create(settings), log);

        var result = await gateway.SendTextAsync(recipient, message);

        Assert.False(result.Succeeded);
        Assert.Equal("400", result.ErrorCode);
        Assert.Equal("Falha ao enviar mensagem WhatsApp.", result.ErrorMessage);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(4201, entry.EventId.Id);
        Assert.Equal("WhatsApp.MetaSendRejected", entry.EventId.Name);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("HttpStatus=400", entry.Message);
        Assert.Contains("MetaErrorCode=131047", entry.Message);
        Assert.Contains("MetaErrorSubcode=2494010", entry.Message);
        Assert.Contains("MetaErrorType=OAuthException", entry.Message);
        Assert.Contains("FbtraceId=AbCdEf123456", entry.Message);
        Assert.Contains("PhoneNumberId=************8243", entry.Message);
        Assert.Contains("Recipient=*********2273", entry.Message);
        Assert.Contains("[REDACTED]", entry.Message);
        Assert.DoesNotContain(accessToken, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(appSecret, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(verifyToken, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(message, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(recipient, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(phoneNumberId, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("messaging_product", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(handler.Body, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gateway_logs_optional_meta_error_fields_as_null_when_absent()
    {
        var log = new CapturingLogger<MetaWhatsAppBusinessGateway>();
        var handler = new FakeHandler(HttpStatusCode.BadRequest, "{\"error\":{\"code\":100,\"type\":\"OAuthException\",\"message\":\"Invalid parameter\"}}");
        var gateway = new MetaWhatsAppBusinessGateway(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-id-8243", AccessToken = "test-token" }),
            log);

        var result = await gateway.SendTextAsync("5511999992273", "Teste");

        Assert.False(result.Succeeded);
        var entry = Assert.Single(log.Entries);
        Assert.Contains("MetaErrorCode=100", entry.Message);
        Assert.Contains("MetaErrorSubcode=", entry.Message);
        Assert.Contains("FbtraceId=", entry.Message);
    }

    [Fact]
    public async Task Gateway_does_not_log_meta_rejection_event_for_successful_response()
    {
        var log = new CapturingLogger<MetaWhatsAppBusinessGateway>();
        var gateway = new MetaWhatsAppBusinessGateway(
            new HttpClient(new FakeHandler()) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-id-8243", AccessToken = "test-token" }),
            log);

        var result = await gateway.SendTextAsync("5511999992273", "Teste");

        Assert.True(result.Succeeded);
        Assert.Equal("wamid.sent.1", result.ExternalMessageId);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task Gateway_propagates_cancellation_from_timeout_handler()
    {
        var gateway = new MetaWhatsAppBusinessGateway(new HttpClient(new TimeoutHandler()) { BaseAddress = new Uri("https://graph.facebook.com/") }, Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-test", AccessToken = "fake-token" }), NullLogger<MetaWhatsAppBusinessGateway>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.SendTextAsync("5511999990000", "Olá", new CancellationToken(true)));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string RequestUri { get; private set; } = "";
        public string Body { get; private set; } = "";
        private readonly HttpStatusCode statusCode;
        private readonly string? responseJson;
        public FakeHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string? responseJson = null)
        {
            this.statusCode = statusCode;
            this.responseJson = responseJson;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri?.ToString() ?? "";
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            HttpContent content = responseJson is null
                ? JsonContent.Create(new { messages = new[] { new { id = "wamid.sent.1" } } })
                : new StringContent(responseJson, Encoding.UTF8, "application/json");
            return new HttpResponseMessage(statusCode) { Content = content };
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<CapturedLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullLoggerScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }

    private sealed record CapturedLogEntry(LogLevel Level, EventId EventId, string Message);

    private sealed class NullLoggerScope : IDisposable
    {
        public static NullLoggerScope Instance { get; } = new();
        public void Dispose() { }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }

    private sealed class FailMessageInsertInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<WhatsAppMessage>().Any(x => x.State == EntityState.Added) == true)
                throw new InvalidOperationException("Injected persistence failure.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TrackingWhatsAppGateway : IWhatsAppBusinessGateway
    {
        public int Calls { get; private set; }

        public Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new WhatsAppSendResult(true, "wamid.test", null, null));
        }
    }

    private sealed class TrackingMediaStorage : IWhatsAppMediaStorage
    {
        public int OpenCalls { get; private set; }
        public Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default) =>
            Task.FromResult("private-reference");
        public Task<WhatsAppMediaReadResult?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            return Task.FromResult<WhatsAppMediaReadResult?>(new(new MemoryStream([1]), "image/jpeg", "image.jpg", 1));
        }
    }
}
