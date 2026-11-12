using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Options;
using Jint;
using Options = Microsoft.Extensions.Options.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class WhatsAppEmbeddedSignupTests
{
    [Fact]
    public void Embedded_signup_is_not_ready_when_only_the_existing_cloud_api_switch_is_enabled()
    {
        var options = new WhatsAppBusinessOptions
        {
            Enabled = true,
            GraphApiVersion = "v23.0",
            EmbeddedSignupAppId = "public-app-id",
            EmbeddedSignupConfigId = "public-config-id",
            EmbeddedSignupDisplayPhoneNumber = "+5548920077785",
            AppSecret = "test-app-secret",
            VerifyToken = "test-verify-token"
        };

        Assert.True(options.Enabled);
        Assert.False(options.IsEmbeddedSignupReady);
    }

    [Fact]
    public async Task Embedded_signup_admin_routes_are_not_found_when_its_flag_is_disabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient();
        var options = Options.Create(new WhatsAppBusinessOptions { Enabled = true });
        var service = new WhatsAppBusinessConnectionService(
            db,
            meta,
            new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider()),
            options);
        var controller = new Orofoods.Web.Areas.Admin.Controllers.WhatsAppEmbeddedSignupController(service, options);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(controller.Connect());
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await controller.Complete(
            new Orofoods.Web.Areas.Admin.Models.WhatsAppEmbeddedSignupCompletionRequest
            {
                EventType = WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding,
                AuthorizationCode = "one-time-code",
                BusinessAccountId = "123456789"
            },
            CancellationToken.None));
        Assert.Equal(0, meta.ExchangeCount);
    }

    [Fact]
    public async Task Embedded_signup_admin_routes_are_not_found_when_global_whatsapp_feature_is_disabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient();
        var settings = ConfiguredOptions();
        settings.Enabled = false;
        settings.EmbeddedSignupEnabled = true;
        var options = Options.Create(settings);
        var service = new WhatsAppBusinessConnectionService(
            db,
            meta,
            new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider()),
            options);
        var controller = new Orofoods.Web.Areas.Admin.Controllers.WhatsAppEmbeddedSignupController(service, options)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        Assert.IsType<NotFoundResult>(controller.Connect());
        Assert.IsType<NotFoundResult>(await controller.Complete(
            new Orofoods.Web.Areas.Admin.Models.WhatsAppEmbeddedSignupCompletionRequest
            {
                EventType = WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding,
                AuthorizationCode = "one-time-code",
                BusinessAccountId = "123456789"
            },
            CancellationToken.None));
        Assert.Equal(0, meta.ExchangeCount);
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public void Embedded_signup_readiness_requires_all_runtime_and_webhook_configuration()
    {
        var mutations = new Action<WhatsAppBusinessOptions>[]
        {
            options => options.Enabled = false,
            options => options.EmbeddedSignupEnabled = false,
            options => options.GraphApiVersion = "",
            options => options.EmbeddedSignupAppId = "",
            options => options.EmbeddedSignupConfigId = "",
            options => options.EmbeddedSignupDisplayPhoneNumber = "",
            options => options.AppSecret = "",
            options => options.VerifyToken = ""
        };

        Assert.True(ConfiguredOptions().IsEmbeddedSignupReady);
        foreach (var mutation in mutations)
        {
            var options = ConfiguredOptions();
            mutation(options);
            Assert.False(options.IsEmbeddedSignupReady);
        }
    }

    [Fact]
    public void Embedded_signup_readiness_does_not_require_a_preconfigured_phone_number_id()
    {
        var options = ConfiguredOptions();

        Assert.True(options.IsEmbeddedSignupReady);
    }

    [Theory]
    [InlineData("")]
    [InlineData("public-login-config-id")]
    [InlineData("123 456")]
    public void Embedded_signup_readiness_rejects_invalid_meta_configuration_id(string configurationId)
    {
        var options = ConfiguredOptions();
        options.EmbeddedSignupConfigId = configurationId;

        Assert.False(options.IsEmbeddedSignupReady);
    }

    [Fact]
    public void Embedded_signup_v4_launch_preserves_coexistence_configuration()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Orofoods.Web", "wwwroot", "js", "whatsapp-embedded-signup.js"));

        Assert.Contains("setup: {}", script);
        Assert.Contains("featureType: 'whatsapp_business_app_onboarding'", script);
        Assert.DoesNotContain("sessionInfoVersion", script);
        Assert.Contains("FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING", script);
        Assert.Contains("waba_id", script);
    }

    [Fact]
    public async Task Embedded_signup_v4_accepts_json_string_finish_callback_without_requiring_version_three()
    {
        const string scenario = """
            const session = { type: 'WA_EMBEDDED_SIGNUP', event: 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING',
                version: 4, data: { waba_id: '1234567890' } };
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify(session) });
            JSON.stringify({
                submitCount: form.submitCount,
                eventType: inputElements['#whatsappSignupEventType']?.value,
                businessAccountId: inputElements['#whatsappSignupWabaId']?.value,
                authorizationCode: inputElements['#whatsappSignupCode']?.value,
                configId: window.loginOptions?.config_id,
                featureType: window.loginOptions?.extras?.featureType,
                setup: window.loginOptions?.extras?.setup
            });
            """;

        using var state = await EvaluateEmbeddedSignupScriptAsync(scenario);
        var root = state.RootElement;

        Assert.Equal(1, root.GetProperty("submitCount").GetInt32());
        Assert.Equal(WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding, root.GetProperty("eventType").GetString());
        Assert.Equal("1234567890", root.GetProperty("businessAccountId").GetString());
        Assert.Equal("one-time-code", root.GetProperty("authorizationCode").GetString());
        Assert.Equal("987654321", root.GetProperty("configId").GetString());
        Assert.Equal("whatsapp_business_app_onboarding", root.GetProperty("featureType").GetString());
        Assert.True(root.GetProperty("setup").ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public async Task Embedded_signup_v4_accepts_object_finish_callback_without_session_version()
    {
        const string scenario = """
            window.messageHandler({
                origin: 'https://business.facebook.com',
                data: { type: 'WA_EMBEDDED_SIGNUP', event: 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING',
                    data: { waba_id: '1234567890' } }
            });
            JSON.stringify({ submitCount: form.submitCount,
                businessAccountId: inputElements['#whatsappSignupWabaId']?.value });
            """;

        using var state = await EvaluateEmbeddedSignupScriptAsync(scenario);

        Assert.Equal(1, state.RootElement.GetProperty("submitCount").GetInt32());
        Assert.Equal("1234567890", state.RootElement.GetProperty("businessAccountId").GetString());
    }

    [Fact]
    public async Task Embedded_signup_v4_rejects_unknown_incomplete_or_malformed_callbacks()
    {
        const string scenario = """
            const finish = { type: 'WA_EMBEDDED_SIGNUP', event: 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING',
                version: 99, data: { waba_id: '1234567890' } };
            window.messageHandler({ origin: 'https://facebook.com.attacker.example', data: JSON.stringify(finish) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: '{malformed-json' });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify([]) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({ ...finish, type: 'UNKNOWN' }) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({ ...finish, event: 'FINISH' }) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({ ...finish, data: null }) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({ ...finish, data: {} }) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({ ...finish, data: { waba_id: 1234567890 } }) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({ ...finish, data: { waba_id: 'waba-test' } }) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({ ...finish,
                data: { waba_id: '1'.repeat(129) } }) });
            JSON.stringify({ submitCount: form.submitCount });
            """;

        using var state = await EvaluateEmbeddedSignupScriptAsync(scenario);

        Assert.Equal(0, state.RootElement.GetProperty("submitCount").GetInt32());
    }

    [Fact]
    public async Task Embedded_signup_v4_submits_only_once_for_duplicate_valid_callbacks()
    {
        const string scenario = """
            const finish = { type: 'WA_EMBEDDED_SIGNUP', event: 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING',
                version: 3, data: { waba_id: '1234567890' } };
            window.messageHandler({ origin: 'https://www.facebook.com', data: finish });
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({ ...finish, version: 4 }) });
            JSON.stringify({ submitCount: form.submitCount });
            """;

        using var state = await EvaluateEmbeddedSignupScriptAsync(scenario);

        Assert.Equal(1, state.RootElement.GetProperty("submitCount").GetInt32());
    }

    [Fact]
    public async Task Embedded_signup_v4_does_not_submit_without_oauth_code()
    {
        const string scenario = """
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({
                type: 'WA_EMBEDDED_SIGNUP', event: 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING',
                data: { waba_id: '1234567890' }
            }) });
            JSON.stringify({ submitCount: form.submitCount });
            """;

        using var state = await EvaluateEmbeddedSignupScriptAsync(scenario, "{ authResponse: {} }");

        Assert.Equal(0, state.RootElement.GetProperty("submitCount").GetInt32());
    }

    [Fact]
    public async Task Embedded_signup_v4_rejects_non_string_oauth_code()
    {
        const string scenario = """
            window.messageHandler({ origin: 'https://www.facebook.com', data: {
                type: 'WA_EMBEDDED_SIGNUP', event: 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING',
                version: 3, data: { waba_id: '1234567890' }
            } });
            JSON.stringify({ submitCount: form.submitCount });
            """;

        using var state = await EvaluateEmbeddedSignupScriptAsync(
            scenario,
            "{ authResponse: { code: 1234567890 } }");

        Assert.Equal(0, state.RootElement.GetProperty("submitCount").GetInt32());
    }

    [Fact]
    public async Task Embedded_signup_v4_cancel_callback_does_not_reuse_previous_oauth_code()
    {
        const string scenario = """
            const finish = { type: 'WA_EMBEDDED_SIGNUP', event: 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING',
                data: { waba_id: '1234567890' } };
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify({
                type: 'WA_EMBEDDED_SIGNUP', event: 'CANCEL', data: {}
            }) });
            window.messageHandler({ origin: 'https://www.facebook.com', data: finish });
            const staleCodeWasRejected = form.submitCount === 0;
            startButton.clickHandler();
            window.messageHandler({ origin: 'https://www.facebook.com', data: JSON.stringify(finish) });
            JSON.stringify({
                staleCodeWasRejected,
                submitCount: form.submitCount,
                authorizationCode: inputElements['#whatsappSignupCode']?.value
            });
            """;

        using var state = await EvaluateEmbeddedSignupScriptAsync(
            scenario,
            "loginCount === 1 ? { authResponse: { code: 'stale-code' } } : { authResponse: { code: 'fresh-code' } }");

        Assert.True(state.RootElement.GetProperty("staleCodeWasRejected").GetBoolean());
        Assert.Equal(1, state.RootElement.GetProperty("submitCount").GetInt32());
        Assert.Equal("fresh-code", state.RootElement.GetProperty("authorizationCode").GetString());
    }

    [Fact]
    public async Task Embedded_signup_rejects_missing_verify_token_before_meta_or_persistence()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient();
        var settings = ConfiguredOptions();
        settings.VerifyToken = "";
        var service = new WhatsAppBusinessConnectionService(
            db,
            meta,
            new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider()),
            Options.Create(settings));

        await Assert.ThrowsAsync<WhatsAppEmbeddedSignupValidationException>(() =>
            service.CompleteAsync(WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding, "code", "123456789"));

        Assert.Equal(0, meta.ExchangeCount);
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public async Task Embedded_signup_completion_is_blocked_when_its_independent_flag_is_disabled()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient();
        var settings = ConfiguredOptions();
        settings.EmbeddedSignupEnabled = false;
        var service = new WhatsAppBusinessConnectionService(
            db,
            meta,
            new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider()),
            Options.Create(settings));

        await Assert.ThrowsAsync<WhatsAppEmbeddedSignupValidationException>(() =>
            service.CompleteAsync(WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding, "code", "123456789"));

        Assert.Equal(0, meta.ExchangeCount);
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public async Task Embedded_signup_phone_provider_requires_the_configured_connection_and_preserves_legacy_options()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var settings = Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            EmbeddedSignupEnabled = true,
            PhoneNumberId = "legacy-phone-number",
            EmbeddedSignupDisplayPhoneNumber = "+5548920077785"
        });
        var provider = new WhatsAppPhoneNumberIdProvider(db, settings);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync());
        db.WhatsAppBusinessConnections.Add(new WhatsAppBusinessConnection
        {
            BusinessAccountId = "waba-connected",
            PhoneNumberId = "coexistence-phone-number",
            DisplayPhoneNumber = "+5548920077785",
            ProtectedAccessToken = "protected"
        });
        await db.SaveChangesAsync();

        Assert.Equal("coexistence-phone-number", await provider.GetAsync());
        Assert.Equal("legacy-phone-number", settings.Value.PhoneNumberId);
    }

    [Fact]
    public async Task Cloud_api_gateway_uses_embedded_signup_token_provider_over_legacy_config_token()
    {
        var handler = new AuthorizationRecordingHandler();
        var gateway = new MetaWhatsAppBusinessGateway(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-id", AccessToken = "legacy-config-token" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MetaWhatsAppBusinessGateway>.Instance,
            new FixedWhatsAppTokenProvider("protected-connection-token"),
            new FixedWhatsAppPhoneNumberIdProvider("connected-phone-id"));

        var result = await gateway.SendTextAsync("5548999999999", "mensagem");

        Assert.True(result.Succeeded);
        Assert.Equal("Bearer protected-connection-token", handler.Authorization);
        Assert.Contains("/connected-phone-id/messages", handler.RequestUri);
    }

    [Fact]
    public async Task Media_download_uses_embedded_signup_token_provider()
    {
        var handler = new AuthorizationRecordingHandler();
        var client = new MetaWhatsAppMediaClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(new WhatsAppBusinessOptions { Enabled = true, GraphApiVersion = "v23.0", PhoneNumberId = "phone-id", AccessToken = "legacy-config-token" }),
            new FixedWhatsAppTokenProvider("protected-connection-token"),
            new FixedWhatsAppPhoneNumberIdProvider("connected-phone-id"));

        _ = await client.DownloadAsync("media-id", "image/jpeg", 100);

        Assert.Equal("Bearer protected-connection-token", handler.Authorization);
        Assert.Contains(handler.RequestUris, uri => uri!.Contains("phone_number_id=connected-phone-id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Embedded_signup_client_uses_documented_meta_oauth_and_coexistence_resources()
    {
        var handler = new RecordingMetaHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/v23.0/oauth/access_token" => Json("""{"access_token":"token-private"}"""),
            "/v23.0/waba-test/phone_numbers" => Json("""{"data":[{"id":"1392450397288350","display_phone_number":"+55 48 92007-7785"}]}"""),
            "/v23.0/waba-test/subscribed_apps" => Json("""{"success":true}"""),
            "/v23.0/1392450397288350/smb_app_data" => Json("""{"request_id":"history-request"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var client = new MetaWhatsAppEmbeddedSignupClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(ConfiguredOptions()));

        var token = await client.ExchangeCodeAsync("one-time-code");
        var numbers = await client.GetPhoneNumbersAsync("waba-test", token);
        await client.SubscribeAppAsync("waba-test", token);
        await client.RequestCoexistenceSyncAsync("1392450397288350", token, "history");

        Assert.Equal("token-private", token);
        Assert.Equal("1392450397288350", Assert.Single(numbers).Id);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Contains("/v23.0/oauth/access_token", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("/v23.0/waba-test/phone_numbers", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal("/v23.0/waba-test/subscribed_apps", handler.Requests[2].RequestUri!.AbsolutePath);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal("/v23.0/1392450397288350/smb_app_data", handler.Requests[3].RequestUri!.AbsolutePath);
        Assert.Equal("Bearer token-private", handler.Requests[1].Authorization);
        Assert.Equal("history", JsonDocument.Parse(handler.Requests[3].Body!).RootElement.GetProperty("sync_type").GetString());
        Assert.Contains("client_secret=app-secret-private", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("code=one-time-code", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task Embedded_signup_client_reads_all_pages_of_meta_phone_numbers()
    {
        var handler = new RecordingMetaHandler(request => request.RequestUri!.Query.Contains("after=cursor", StringComparison.Ordinal)
            ? Json("""{"data":[{"id":"555000000005678","display_phone_number":"+55 48 99999-0000"}]}""")
            : Json("""{"data":[{"id":"555000000001234","display_phone_number":"+55 48 92007-7785"}],"paging":{"next":"https://graph.facebook.com/v23.0/waba-test/phone_numbers?after=cursor&access_token=must-not-be-forwarded&appsecret_proof=must-not-be-forwarded"}}"""));
        var client = new MetaWhatsAppEmbeddedSignupClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(ConfiguredOptions()));

        var numbers = await client.GetPhoneNumbersAsync("waba-test", "authorized-token");

        Assert.Equal(2, numbers.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("555000000001234", numbers[0].Id);
        Assert.Equal("555000000005678", numbers[1].Id);
        Assert.Contains("after=cursor", handler.Requests[1].RequestUri.Query);
        Assert.DoesNotContain("access_token", handler.Requests[1].RequestUri.Query);
        Assert.DoesNotContain("appsecret_proof", handler.Requests[1].RequestUri.Query);
        Assert.All(handler.Requests, request => Assert.Equal("Bearer authorized-token", request.Authorization));
    }

    [Fact]
    public async Task Embedded_signup_client_rejects_phone_number_pagination_to_another_host()
    {
        var handler = new RecordingMetaHandler(_ => Json("""{"data":[],"paging":{"next":"https://attacker.example/v23.0/waba-test/phone_numbers?after=cursor"}}"""));
        var client = new MetaWhatsAppEmbeddedSignupClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(ConfiguredOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetPhoneNumbersAsync("waba-test", "authorized-token"));

        Assert.Single(handler.Requests);
        Assert.Equal("graph.facebook.com", handler.Requests[0].RequestUri.Host);
    }

    [Fact]
    public async Task Embedded_signup_client_blocks_state_sync_request_until_the_official_contract_is_validated()
    {
        var handler = new RecordingMetaHandler(_ => Json("{}"));
        var client = new MetaWhatsAppEmbeddedSignupClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(ConfiguredOptions()));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.RequestCoexistenceSyncAsync("1392450397288350", "token-private", "smb_app_state_sync"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Embedded_signup_rejects_wrong_finish_event_before_meta_or_persistence()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient();
        var service = CreateConnectionService(db, meta);

        await Assert.ThrowsAsync<WhatsAppEmbeddedSignupValidationException>(() =>
            service.CompleteAsync("FINISH", "code", "123456789"));

        Assert.Equal(0, meta.ExchangeCount);
        Assert.Equal(0, meta.PhoneNumbersRequestCount);
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public async Task Embedded_signup_rejects_when_meta_returns_no_matching_display_number()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient
        {
            PhoneNumbers = [new MetaWhatsAppPhoneNumber("1392450397288350", "+5548999999999")]
        };
        var service = CreateConnectionService(db, meta);

        await Assert.ThrowsAsync<WhatsAppEmbeddedSignupValidationException>(() =>
            service.CompleteAsync(WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding, "code", "123456789"));

        Assert.Equal(1, meta.PhoneNumbersRequestCount);
        Assert.Equal(0, meta.SubscribeCount);
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public async Task Embedded_signup_rejects_multiple_normalized_display_number_matches_before_subscribing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient
        {
            PhoneNumbers =
            [
                new MetaWhatsAppPhoneNumber("1392450397288350", "+55 48 92007-7785"),
                new MetaWhatsAppPhoneNumber("1425306064001765", "+5548920077785")
            ]
        };
        var service = CreateConnectionService(db, meta);

        await Assert.ThrowsAsync<WhatsAppEmbeddedSignupValidationException>(() =>
            service.CompleteAsync(WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding, "code", "123456789"));

        Assert.Equal(1, meta.PhoneNumbersRequestCount);
        Assert.Equal(0, meta.SubscribeCount);
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public async Task Embedded_signup_rejects_non_numeric_meta_phone_number_id_before_subscribing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient
        {
            PhoneNumbers = [new MetaWhatsAppPhoneNumber("invalid-id", "+55 48 92007-7785")]
        };
        var service = CreateConnectionService(db, meta);

        await Assert.ThrowsAsync<WhatsAppEmbeddedSignupValidationException>(() =>
            service.CompleteAsync(WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding, "code", "123456789"));

        Assert.Equal(1, meta.PhoneNumbersRequestCount);
        Assert.Equal(0, meta.SubscribeCount);
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public async Task Embedded_signup_connects_uniquely_matched_number_persists_only_protected_token_and_requests_history_only()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        const string accessToken = "connected-meta-token-private";
        var meta = new FakeMetaSignupClient
        {
            AccessToken = accessToken,
            PhoneNumbers =
            [
                new MetaWhatsAppPhoneNumber("555000000001234", "+55 48 92007-7785"),
                new MetaWhatsAppPhoneNumber("555000000005678", "+55 48 99999-0000")
            ]
        };
        var protector = new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider());
        var settings = Options.Create(ConfiguredOptions());
        var service = new WhatsAppBusinessConnectionService(db, meta, protector, settings);

        var result = await service.CompleteAsync(
            WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding,
            "one-time-code",
            "123456789");
        var connection = await db.WhatsAppBusinessConnections.SingleAsync();

        Assert.True(result.Connected);
        Assert.False(result.ContactsSyncRequested);
        Assert.True(result.HistorySyncRequested);
        Assert.Equal("555000000001234", connection.PhoneNumberId);
        Assert.Equal("+55 48 92007-7785", connection.DisplayPhoneNumber);
        Assert.Equal("legacy-phone-id", settings.Value.PhoneNumberId);
        Assert.NotEqual(accessToken, connection.ProtectedAccessToken);
        Assert.Equal(accessToken, protector.Unprotect(connection.ProtectedAccessToken));
        Assert.Equal(["history"], meta.RequestedSyncTypes);
        Assert.Equal(1, meta.SubscribeCount);
    }

    [Fact]
    public async Task Embedded_signup_http_failure_does_not_expose_meta_error_body()
    {
        var handler = new RecordingMetaHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("app-secret-private token-private rejected")
        });
        var client = new MetaWhatsAppEmbeddedSignupClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Options.Create(ConfiguredOptions()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeCodeAsync("one-time-code"));

        Assert.DoesNotContain("app-secret-private", error.Message);
        Assert.DoesNotContain("token-private", error.Message);
        Assert.DoesNotContain("one-time-code", error.Message);
    }

    private static WhatsAppBusinessConnectionService CreateConnectionService(ApplicationDbContext db, FakeMetaSignupClient meta) =>
        new(db, meta, new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider()), Options.Create(ConfiguredOptions()));

    private static async Task<JsonDocument> EvaluateEmbeddedSignupScriptAsync(
        string scenario,
        string authorizationResponse = "{ authResponse: { code: 'one-time-code' } }")
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Orofoods.Web",
            "wwwroot",
            "js",
            "whatsapp-embedded-signup.js"));
        const string harness = """
            const inputElements = {};
            const form = {
                submitCount: 0,
                querySelector(selector) { return inputElements[selector] ??= { value: '' }; },
                requestSubmit() { this.submitCount++; }
            };
            const startButton = {
                disabled: false,
                addEventListener(name, callback) { this.clickHandler = callback; }
            };
            const status = { textContent: '' };
            const configNode = { textContent: '{"appId":"123456789","configurationId":"987654321","graphApiVersion":"v26.0"}' };
            globalThis.window = globalThis;
            globalThis.URL = class {
                constructor(value) {
                    const separator = value.indexOf('://');
                    if (separator <= 0) throw new Error('Invalid URL');
                    this.protocol = `${value.substring(0, separator)}:`;
                    this.hostname = value.substring(separator + 3).split('/')[0].split(':')[0];
                }
            };
            window.addEventListener = (name, callback) => { window.messageHandler = callback; };
            globalThis.document = {
                getElementById(id) {
                    return ({ whatsappEmbeddedSignupConfig: configNode, whatsappEmbeddedSignupForm: form,
                        whatsappEmbeddedSignupStart: startButton, whatsappEmbeddedSignupStatus: status })[id] ?? null;
                },
                createElement() { return {}; },
                head: { appendChild() {} }
            };
            let loginCount = 0;
            globalThis.FB = {
                init() {},
                login(callback, options) {
                    loginCount++;
                    window.loginOptions = options;
                    callback(__AUTH_RESPONSE__);
                }
            };
            __SCRIPT__
            window.fbAsyncInit();
            startButton.clickHandler();
            __SCENARIO__
            """;
        var testScript = harness
            .Replace("__AUTH_RESPONSE__", authorizationResponse, StringComparison.Ordinal)
            .Replace("__SCRIPT__", script, StringComparison.Ordinal)
            .Replace("__SCENARIO__", scenario, StringComparison.Ordinal);
        var result = await new Engine().EvaluateAsync(testScript);
        return JsonDocument.Parse(result.AsString());
    }

    private static WhatsAppBusinessOptions ConfiguredOptions() => new()
    {
        Enabled = true,
        EmbeddedSignupEnabled = true,
        GraphApiVersion = "v23.0",
        PhoneNumberId = "legacy-phone-id",
        EmbeddedSignupDisplayPhoneNumber = "+5548920077785",
        EmbeddedSignupAppId = "123456789012345",
        EmbeddedSignupConfigId = "987654321098765",
        AppSecret = "app-secret-private",
        VerifyToken = "verify-token-private"
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Orofoods.Web", "Orofoods.Web.csproj")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Orofoods repository root was not found.");
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FakeMetaSignupClient : IMetaWhatsAppEmbeddedSignupClient
    {
        public int ExchangeCount { get; private set; }
        public int PhoneNumbersRequestCount { get; private set; }
        public int SubscribeCount { get; private set; }
        public string AccessToken { get; init; } = "fake-meta-token";
        public IReadOnlyList<MetaWhatsAppPhoneNumber> PhoneNumbers { get; init; } = [new("1392450397288350", "+55 48 92007-7785")];
        public List<string> RequestedSyncTypes { get; } = [];

        public Task<string> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            ExchangeCount++;
            return Task.FromResult(AccessToken);
        }

        public Task<IReadOnlyList<MetaWhatsAppPhoneNumber>> GetPhoneNumbersAsync(string businessAccountId, string accessToken, CancellationToken cancellationToken = default)
        {
            PhoneNumbersRequestCount++;
            return Task.FromResult(PhoneNumbers);
        }

        public Task SubscribeAppAsync(string businessAccountId, string accessToken, CancellationToken cancellationToken = default)
        {
            SubscribeCount++;
            return Task.CompletedTask;
        }

        public Task RequestCoexistenceSyncAsync(string phoneNumberId, string accessToken, string syncType, CancellationToken cancellationToken = default)
        {
            RequestedSyncTypes.Add(syncType);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMetaHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));
            return respond(request);
        }
    }

    private sealed record RequestSnapshot(HttpMethod Method, Uri RequestUri, string? Authorization, string? Body);

    private sealed class AuthorizationRecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? RequestUri { get; private set; }
        public List<string?> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri?.ToString();
            RequestUris.Add(RequestUri);
            HttpResponseMessage response = request.RequestUri!.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal)
                ? Json("""{"messages":[{"id":"wamid.sent"}]}""")
                : Json("""{"url":"https://lookaside.fbsbx.com/media","mime_type":"image/jpeg","file_size":5}""");
            return Task.FromResult(response);
        }
    }

    private sealed class FixedWhatsAppTokenProvider(string token) : IWhatsAppAccessTokenProvider
    {
        public Task<string> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
    }

    private sealed class FixedWhatsAppPhoneNumberIdProvider(string phoneNumberId) : IWhatsAppPhoneNumberIdProvider
    {
        public Task<string> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(phoneNumberId);
    }
}
