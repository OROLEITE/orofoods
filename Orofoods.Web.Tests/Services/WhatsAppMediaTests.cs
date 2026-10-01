using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Storage;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class WhatsAppMediaTests
{
    [Theory]
    [InlineData("image", "image/jpeg", null, "Foto", false, WhatsAppMessageType.Image)]
    [InlineData("audio", "audio/ogg; codecs=opus", null, null, true, WhatsAppMessageType.Audio)]
    [InlineData("audio", "audio/mpeg", null, null, false, WhatsAppMessageType.Audio)]
    [InlineData("video", "video/mp4", null, "Vídeo", false, WhatsAppMessageType.Video)]
    [InlineData("document", "application/pdf", "../../pedido.pdf", "Pedido", false, WhatsAppMessageType.Document)]
    [InlineData("sticker", "image/webp", null, null, false, WhatsAppMessageType.Sticker)]
    public async Task Supported_inbound_media_is_downloaded_once_and_metadata_is_persisted(
        string type,
        string mimeType,
        string? fileName,
        string? caption,
        bool voice,
        WhatsAppMessageType expectedType)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var client = new FakeMediaClient(mimeType);
        var storage = new FakeMediaStorage();
        var service = CreateService(db, client, storage);
        using var payload = JsonDocument.Parse(BuildMediaPayload($"media-{type}-{voice}", type, mimeType, fileName, caption, voice));

        await service.ProcessAsync(payload);

        var message = await db.WhatsAppMessages.AsNoTracking().SingleAsync();
        Assert.Equal(expectedType, message.Type);
        Assert.Equal("meta-media-id", message.MediaId);
        Assert.Equal(WhatsAppMediaPolicy.NormalizeMimeType(mimeType), message.MimeType);
        Assert.Equal(caption, message.Caption);
        Assert.Equal(voice, message.IsVoiceMessage);
        Assert.Equal(WhatsAppMediaState.Available, message.MediaState);
        Assert.Equal("stored/reference", message.MediaStorageReference);
        Assert.Equal(4, message.MediaSizeBytes);
        Assert.DoesNotContain("..", message.FileName ?? string.Empty);
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, storage.Calls);
    }

    [Fact]
    public async Task Animated_sticker_webp_is_preserved_without_inventing_a_gif_message_type()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, new FakeMediaClient("image/webp"), new FakeMediaStorage());
        using var payload = JsonDocument.Parse(BuildMediaPayload("animated-sticker", "sticker", "image/webp"));

        await service.ProcessAsync(payload);

        var message = await db.WhatsAppMessages.AsNoTracking().SingleAsync();
        Assert.Equal(WhatsAppMessageType.Sticker, message.Type);
        Assert.Equal("image/webp", message.MimeType);
        Assert.Equal(WhatsAppMediaState.Available, message.MediaState);
    }

    [Fact]
    public async Task Invalid_media_mime_is_rejected_without_download_or_storage()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var client = new FakeMediaClient("image/svg+xml");
        var storage = new FakeMediaStorage();
        var service = CreateService(db, client, storage);
        using var payload = JsonDocument.Parse(BuildMediaPayload("invalid-mime", "image", "image/svg+xml"));

        await service.ProcessAsync(payload);

        Assert.Equal(WhatsAppMediaState.Rejected, (await db.WhatsAppMessages.AsNoTracking().SingleAsync()).MediaState);
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, storage.Calls);
    }

    [Theory]
    [InlineData("DOWNLOAD_503", WhatsAppMediaState.Failed)]
    [InlineData("SIZE_REJECTED", WhatsAppMediaState.Rejected)]
    [InlineData("MIME_MISMATCH", WhatsAppMediaState.Rejected)]
    public async Task Media_retrieval_failures_are_persisted_without_failing_the_webhook(string errorCode, WhatsAppMediaState expected)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var client = new FakeMediaClient("image/jpeg") { ErrorCode = errorCode };
        var service = CreateService(db, client, new FakeMediaStorage());
        using var payload = JsonDocument.Parse(BuildMediaPayload($"failure-{errorCode}", "image", "image/jpeg"));

        await service.ProcessAsync(payload);

        Assert.Equal(expected, (await db.WhatsAppMessages.AsNoTracking().SingleAsync()).MediaState);
    }

    [Fact]
    public async Task Duplicate_media_webhook_does_not_download_or_store_twice()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var client = new FakeMediaClient("video/mp4");
        var storage = new FakeMediaStorage();
        var service = CreateService(db, client, storage);
        var json = BuildMediaPayload("duplicate-media", "video", "video/mp4");
        using var first = JsonDocument.Parse(json);
        using var second = JsonDocument.Parse(json);

        await service.ProcessAsync(first);
        await service.ProcessAsync(second);

        Assert.Single(await db.WhatsAppMessages.AsNoTracking().ToListAsync());
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, storage.Calls);
    }

    [Fact]
    public async Task Unsupported_message_type_uses_safe_fallback_and_keeps_conversation_available()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, new FakeMediaClient("image/jpeg"), new FakeMediaStorage());
        const string json = "{\"object\":\"whatsapp_business_account\",\"entry\":[{\"id\":\"waba-test\",\"changes\":[{\"value\":{\"metadata\":{\"phone_number_id\":\"phone-test\"},\"messages\":[{\"id\":\"unsupported\",\"from\":\"5548999999999\",\"type\":\"reaction\",\"reaction\":{\"emoji\":\"ok\"}}]}}]}]}";
        using var payload = JsonDocument.Parse(json);

        await service.ProcessAsync(payload);

        var message = await db.WhatsAppMessages.AsNoTracking().SingleAsync();
        Assert.Equal(WhatsAppMessageType.Unsupported, message.Type);
        Assert.Equal("Tipo de mensagem ainda não suportado.", message.TextBody);
        Assert.Equal(WhatsAppMediaState.None, message.MediaState);
    }

    [Fact]
    public async Task Meta_media_client_retrieves_metadata_then_downloads_with_bearer_auth()
    {
        var handler = new MediaHttpHandler(4);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var service = new MetaWhatsAppMediaClient(client, Options.Create(MetaOptions()));

        using var result = await service.DownloadAsync("media-id", "image/jpeg", 1024);

        Assert.True(result.Succeeded);
        Assert.Equal("image/jpeg", result.MimeType);
        Assert.Equal(4, result.SizeBytes);
        Assert.Equal(2, handler.Calls);
        Assert.All(handler.AuthorizationSchemes, scheme => Assert.Equal("Bearer", scheme));
        Assert.DoesNotContain("access-token", string.Join('|', handler.RequestUris));
    }

    [Fact]
    public async Task Meta_media_client_rejects_oversized_metadata_without_downloading_content()
    {
        var handler = new MediaHttpHandler(4096);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.com/") };
        var service = new MetaWhatsAppMediaClient(client, Options.Create(MetaOptions()));

        using var result = await service.DownloadAsync("media-id", "image/jpeg", 1024);

        Assert.False(result.Succeeded);
        Assert.Equal("SIZE_REJECTED", result.ErrorCode);
        Assert.Equal(1, handler.Calls);
    }

    private static WhatsAppConversationService CreateService(ApplicationDbContext db, IWhatsAppMediaClient client, IWhatsAppMediaStorage storage) =>
        new(db, Options.Create(new WhatsAppBusinessOptions
        {
            Enabled = true,
            PhoneNumberId = "phone-test",
            BusinessAccountId = "waba-test",
            MaxInboundMediaBytes = 1024
        }), new UserNotificationService(db), client, storage);

    private static WhatsAppBusinessOptions MetaOptions() => new()
    {
        Enabled = true,
        GraphApiVersion = "v25.0",
        PhoneNumberId = "phone-test",
        AccessToken = "access-token"
    };

    private static string BuildMediaPayload(string externalId, string type, string mimeType, string? fileName = null, string? caption = null, bool voice = false)
    {
        var media = new Dictionary<string, object?>
        {
            ["id"] = "meta-media-id",
            ["mime_type"] = mimeType
        };
        if (fileName is not null) media["filename"] = fileName;
        if (caption is not null) media["caption"] = caption;
        if (voice) media["voice"] = true;
        var message = new Dictionary<string, object?>
        {
            ["id"] = externalId,
            ["from"] = "5548999999999",
            ["type"] = type,
            [type] = media
        };
        return JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-test", changes = new[] { new { value = new { metadata = new { phone_number_id = "phone-test" }, messages = new[] { message } } } } } }
        });
    }

    private sealed class FakeMediaClient(string mimeType) : IWhatsAppMediaClient
    {
        public int Calls { get; private set; }
        public string? ErrorCode { get; init; }

        public Task<WhatsAppMediaDownloadResult> DownloadAsync(string mediaId, string expectedMimeType, long maximumBytes, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(ErrorCode is null
                ? new WhatsAppMediaDownloadResult(true, new MemoryStream([1, 2, 3, 4]), WhatsAppMediaPolicy.NormalizeMimeType(mimeType), 4, null)
                : new WhatsAppMediaDownloadResult(false, null, mimeType, maximumBytes + 1, ErrorCode));
        }
    }

    private sealed class FakeMediaStorage : IWhatsAppMediaStorage
    {
        public int Calls { get; private set; }
        public Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult("stored/reference");
        }

        public Task<WhatsAppMediaReadResult?> OpenReadAsync(string reference, CancellationToken cancellationToken = default) =>
            Task.FromResult<WhatsAppMediaReadResult?>(null);
    }

    private sealed class MediaHttpHandler(long declaredSize) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string?> AuthorizationSchemes { get; } = [];
        public List<string> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            AuthorizationSchemes.Add(request.Headers.Authorization?.Scheme);
            RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
            if (Calls == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        url = "https://lookaside.fbsbx.com/media/test",
                        mime_type = "image/jpeg",
                        file_size = declaredSize
                    })
                });
            }

            var content = new ByteArrayContent([1, 2, 3, 4]);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
