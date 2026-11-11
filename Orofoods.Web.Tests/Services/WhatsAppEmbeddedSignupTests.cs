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
            EmbeddedSignupPhoneNumberId = "phone-id",
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
            options => options.EmbeddedSignupPhoneNumberId = "",
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
            EmbeddedSignupPhoneNumberId = "coexistence-phone-number",
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
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public async Task Embedded_signup_rejects_wrong_phone_id_or_display_number_before_subscribing()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var meta = new FakeMetaSignupClient
        {
            PhoneNumbers = [new MetaWhatsAppPhoneNumber("another-number-id", "+5548920077785")]
        };
        var service = CreateConnectionService(db, meta);

        await Assert.ThrowsAsync<WhatsAppEmbeddedSignupValidationException>(() =>
            service.CompleteAsync(WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding, "code", "123456789"));

        Assert.Equal(0, meta.SubscribeCount);
        Assert.Empty(db.WhatsAppBusinessConnections);
    }

    [Fact]
    public async Task Embedded_signup_connects_configured_number_persists_only_protected_token_and_requests_history_only()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        const string accessToken = "connected-meta-token-private";
        var meta = new FakeMetaSignupClient { AccessToken = accessToken };
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
        Assert.Equal("1392450397288350", connection.PhoneNumberId);
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

    private static WhatsAppBusinessOptions ConfiguredOptions() => new()
    {
        Enabled = true,
        EmbeddedSignupEnabled = true,
        GraphApiVersion = "v23.0",
        PhoneNumberId = "legacy-phone-id",
        EmbeddedSignupPhoneNumberId = "1392450397288350",
        EmbeddedSignupDisplayPhoneNumber = "+5548920077785",
        EmbeddedSignupAppId = "public-app-id",
        EmbeddedSignupConfigId = "public-login-config-id",
        AppSecret = "app-secret-private",
        VerifyToken = "verify-token-private"
    };

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FakeMetaSignupClient : IMetaWhatsAppEmbeddedSignupClient
    {
        public int ExchangeCount { get; private set; }
        public int SubscribeCount { get; private set; }
        public string AccessToken { get; init; } = "fake-meta-token";
        public IReadOnlyList<MetaWhatsAppPhoneNumber> PhoneNumbers { get; init; } = [new("1392450397288350", "+55 48 92007-7785")];
        public List<string> RequestedSyncTypes { get; } = [];

        public Task<string> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            ExchangeCount++;
            return Task.FromResult(AccessToken);
        }

        public Task<IReadOnlyList<MetaWhatsAppPhoneNumber>> GetPhoneNumbersAsync(string businessAccountId, string accessToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(PhoneNumbers);

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
