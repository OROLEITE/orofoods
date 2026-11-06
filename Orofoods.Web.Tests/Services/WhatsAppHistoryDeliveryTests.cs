using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Orofoods.Web.Controllers.Api.V1;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WhatsAppHistoryDeliveryTests
{
    [Fact]
    public async Task History_webhook_over_one_mib_is_encrypted_and_durably_deduplicated()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var protection = new EphemeralDataProtectionProvider();
        var options = Options.Create(ConnectedOptions());
        var body = BuildLargeHistoryPayload();
        Assert.True(Encoding.UTF8.GetByteCount(body) > 1_048_576);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var controller = CreateWebhookController(db, options, protection, body);
            Sign(controller, body, options.Value.AppSecret);
            Assert.IsType<OkResult>(await controller.Receive(CancellationToken.None));
        }

        var delivery = await db.WhatsAppWebhookDeliveries.OrderBy(x => x.Id).FirstAsync();
        Assert.Equal(WhatsAppWebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant(), delivery.PayloadHash);
        Assert.NotNull(delivery.ProtectedPayload);
        Assert.NotEqual(Encoding.UTF8.GetBytes(body), delivery.ProtectedPayload);
        Assert.Equal(Encoding.UTF8.GetBytes(body), protection.CreateProtector("Orofoods.WhatsAppBusiness.HistoryPayload.v1").Unprotect(delivery.ProtectedPayload));
    }

    [Fact]
    public async Task History_processor_reclaims_expired_lease_and_completes_replay_safe_import()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var protection = new EphemeralDataProtectionProvider();
        var options = Options.Create(ConnectedOptions());
        var body = BuildHistoryPayload("wamid.durable.1");
        var queue = new WhatsAppWebhookDeliveryQueue(db, protection);
        await queue.EnqueueHistoryAsync(Encoding.UTF8.GetBytes(body));
        await queue.EnqueueHistoryAsync(Encoding.UTF8.GetBytes(body + " "));
        var delivery = await db.WhatsAppWebhookDeliveries.OrderBy(x => x.Id).FirstAsync();
        delivery.Status = WhatsAppWebhookDeliveryStatus.Processing;
        delivery.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(db, protection, options);

        Assert.True(await processor.ProcessNextAsync());
        Assert.True(await processor.ProcessNextAsync());
        Assert.False(await processor.ProcessNextAsync());
        Assert.All(await db.WhatsAppWebhookDeliveries.ToListAsync(), item => Assert.Equal(WhatsAppWebhookDeliveryStatus.Completed, item.Status));
        Assert.All(await db.WhatsAppWebhookDeliveries.ToListAsync(), item => Assert.Null(item.ProtectedPayload));
        Assert.Single(await db.WhatsAppMessages.AsNoTracking().ToListAsync());
        Assert.Equal(0, (await db.WhatsAppConversations.AsNoTracking().SingleAsync()).UnreadCount);
    }

    [Fact]
    public async Task History_processor_marks_unreadable_delivery_failed_without_exposing_payload()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var protection = new EphemeralDataProtectionProvider();
        var delivery = new WhatsAppWebhookDelivery
        {
            PayloadHash = new string('a', 64),
            ProtectedPayload = [1, 2, 3],
            Status = WhatsAppWebhookDeliveryStatus.Pending
        };
        db.WhatsAppWebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        var processor = CreateProcessor(db, protection, Options.Create(ConnectedOptions()));

        Assert.True(await processor.ProcessNextAsync());

        var failed = await db.WhatsAppWebhookDeliveries.SingleAsync();
        Assert.Equal(WhatsAppWebhookDeliveryStatus.Failed, failed.Status);
        Assert.Equal("PAYLOAD_UNREADABLE", failed.LastErrorCode);
        Assert.NotNull(failed.ProtectedPayload);
    }

    [Fact]
    public async Task History_processor_retains_unrecognized_history_shape_instead_of_marking_empty_import_complete()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var protection = new EphemeralDataProtectionProvider();
        var payload = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-test", changes = new[] { new { field = "history", value = new { metadata = new { phone_number_id = "phone-test" } } } } } }
        });
        await new WhatsAppWebhookDeliveryQueue(db, protection).EnqueueHistoryAsync(Encoding.UTF8.GetBytes(payload));
        var processor = CreateProcessor(db, protection, Options.Create(ConnectedOptions()));

        Assert.True(await processor.ProcessNextAsync());

        var delivery = await db.WhatsAppWebhookDeliveries.SingleAsync();
        Assert.Equal(WhatsAppWebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Equal("INVALID_PAYLOAD", delivery.LastErrorCode);
        Assert.NotNull(delivery.ProtectedPayload);
        Assert.Empty(await db.WhatsAppMessages.ToListAsync());
    }

    [Fact]
    public async Task History_processor_retries_transient_database_failure_without_duplicate_messages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new FailOneWhatsAppMessageSaveInterceptor())
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();
        var protection = new EphemeralDataProtectionProvider();
        var options = Options.Create(ConnectedOptions());
        await new WhatsAppWebhookDeliveryQueue(db, protection).EnqueueHistoryAsync(Encoding.UTF8.GetBytes(BuildHistoryPayload("wamid.retry.1")));
        var processor = CreateProcessor(db, protection, options);

        Assert.True(await processor.ProcessNextAsync());
        var retry = await db.WhatsAppWebhookDeliveries.SingleAsync();
        Assert.Equal(WhatsAppWebhookDeliveryStatus.Pending, retry.Status);
        Assert.Equal("PROCESSING_RETRY", retry.LastErrorCode);
        retry.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();

        Assert.True(await processor.ProcessNextAsync());
        Assert.Equal(WhatsAppWebhookDeliveryStatus.Completed, (await db.WhatsAppWebhookDeliveries.SingleAsync()).Status);
        Assert.Single(await db.WhatsAppMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task History_webhook_rejects_cross_waba_before_durable_enqueue()
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
        var options = Options.Create(ConnectedOptions());
        var body = BuildHistoryPayload("wamid.cross-waba", wabaId: "other-waba");
        var controller = CreateWebhookController(db, options, new EphemeralDataProtectionProvider(), body);
        Sign(controller, body, options.Value.AppSecret);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await controller.Receive(CancellationToken.None)).StatusCode);
        Assert.Empty(await db.WhatsAppWebhookDeliveries.ToListAsync());
    }

    [Fact]
    public async Task History_webhook_rejects_cross_phone_number_before_durable_enqueue()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var options = Options.Create(ConnectedOptions());
        var body = BuildHistoryPayload("wamid.cross-phone", phoneNumberId: "other-phone");
        var controller = CreateWebhookController(db, options, new EphemeralDataProtectionProvider(), body);
        Sign(controller, body, options.Value.AppSecret);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await controller.Receive(CancellationToken.None)).StatusCode);
        Assert.Empty(await db.WhatsAppWebhookDeliveries.ToListAsync());
    }

    private static WhatsAppWebhookDeliveryProcessor CreateProcessor(
        ApplicationDbContext db,
        IDataProtectionProvider protection,
        IOptions<WhatsAppBusinessOptions> options)
    {
        var service = new WhatsAppConversationService(db, options, new UserNotificationService(db));
        return new WhatsAppWebhookDeliveryProcessor(db, service, protection);
    }

    private static WhatsAppWebhookController CreateWebhookController(
        ApplicationDbContext db,
        IOptions<WhatsAppBusinessOptions> options,
        IDataProtectionProvider protection,
        string body)
    {
        var service = new WhatsAppConversationService(db, options, new UserNotificationService(db));
        var http = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        http.Request.Body = new MemoryStream(bytes);
        http.Request.ContentLength = bytes.Length;
        return new WhatsAppWebhookController(service, options, new WhatsAppWebhookDeliveryQueue(db, protection))
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static void Sign(WhatsAppWebhookController controller, string body, string appSecret)
    {
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(body));
        controller.HttpContext.Request.Headers["X-Hub-Signature-256"] = "sha256=" + Convert.ToHexString(signature).ToLowerInvariant();
    }

    private static WhatsAppBusinessOptions ConnectedOptions() => new()
    {
        Enabled = true,
        PhoneNumberId = "phone-test",
        BusinessAccountId = "waba-test",
        AppSecret = "local-test-secret"
    };

    private static string BuildHistoryPayload(string id, string wabaId = "waba-test", string phoneNumberId = "phone-test") => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[] { new { id = wabaId, changes = new[] { new { field = "history", value = new
        {
            metadata = new { phone_number_id = phoneNumberId },
            history = new[] { new { messages = new[] { new { id, from = "5548999999999", timestamp = "1730000000", type = "text", text = new { body = "Histórico recuperável" } } } } }
        } } } } }
    });

    private static string BuildLargeHistoryPayload()
    {
        var messages = Enumerable.Range(0, 280).Select(index => new
        {
            id = $"wamid.large.{index}",
            from = "5548999999999",
            timestamp = "1730000000",
            type = "text",
            text = new { body = new string('x', 4000) }
        }).ToArray();
        return JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-test", changes = new[] { new { field = "history", value = new
            {
                metadata = new { phone_number_id = "phone-test" },
                history = new[] { new { messages } }
            } } } } }
        });
    }

    private sealed class FailOneWhatsAppMessageSaveInterceptor : SaveChangesInterceptor
    {
        private int hasFailed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<WhatsAppMessage>().Any(x => x.State == EntityState.Added) == true &&
                Interlocked.Exchange(ref hasFailed, 1) == 0)
                throw new DbUpdateException("Injected transient persistence failure.");
            return ValueTask.FromResult(result);
        }
    }
}
