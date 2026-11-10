using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Services.Commercial;

public interface IWhatsAppWebhookDeliveryQueue
{
    Task<bool> EnqueueHistoryAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
    Task<bool> EnqueueDisabledStateSyncAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
}

public sealed class WhatsAppWebhookDeliveryQueue(ApplicationDbContext db, IDataProtectionProvider dataProtection) : IWhatsAppWebhookDeliveryQueue
{
    private readonly IDataProtector protector = dataProtection.CreateProtector("Orofoods.WhatsAppBusiness.HistoryPayload.v1");

    public Task<bool> EnqueueHistoryAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) =>
        EnqueueAsync(payload, WhatsAppWebhookDeliveryStatus.Pending, cancellationToken);

    public Task<bool> EnqueueDisabledStateSyncAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) =>
        EnqueueAsync(payload, WhatsAppWebhookDeliveryStatus.Blocked, cancellationToken);

    private async Task<bool> EnqueueAsync(ReadOnlyMemory<byte> payload, WhatsAppWebhookDeliveryStatus initialStatus, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(payload.Length);
        var hash = Convert.ToHexString(SHA256.HashData(payload.Span)).ToLowerInvariant();
        db.WhatsAppWebhookDeliveries.Add(new WhatsAppWebhookDelivery
        {
            PayloadHash = hash,
            ProtectedPayload = protector.Protect(payload.ToArray()),
            Status = initialStatus,
            LastErrorCode = initialStatus == WhatsAppWebhookDeliveryStatus.Blocked ? "SMB_STATE_SYNC_DOCS_UNAVAILABLE" : null,
            ReceivedAt = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var existing = await db.WhatsAppWebhookDeliveries.SingleOrDefaultAsync(x => x.PayloadHash == hash, cancellationToken);
            if (existing is null) throw;
            if (existing.Status == WhatsAppWebhookDeliveryStatus.Failed && existing.ProtectedPayload is not null)
            {
                existing.Status = initialStatus;
                existing.ProtectedPayload = protector.Protect(payload.ToArray());
                existing.Attempts = 0;
                existing.NextAttemptAt = null;
                existing.LeaseExpiresAt = null;
                existing.LastErrorCode = initialStatus == WhatsAppWebhookDeliveryStatus.Blocked ? "SMB_STATE_SYNC_DOCS_UNAVAILABLE" : null;
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }

            return false;
        }
    }
}

public sealed class WhatsAppWebhookDeliveryProcessor(
    ApplicationDbContext db,
    WhatsAppConversationService conversationService,
    IDataProtectionProvider dataProtection,
    ILogger<WhatsAppWebhookDeliveryProcessor>? logger = null)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IDataProtector protector = dataProtection.CreateProtector("Orofoods.WhatsAppBusiness.HistoryPayload.v1");

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var candidateId = await db.WhatsAppWebhookDeliveries.AsNoTracking()
            .Where(x => (x.Status == WhatsAppWebhookDeliveryStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                        (x.Status == WhatsAppWebhookDeliveryStatus.Processing && x.LeaseExpiresAt <= now))
            .OrderBy(x => x.ReceivedAt)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidateId is null) return false;

        var leaseExpiresAt = now.Add(LeaseDuration);
        var claimed = await db.WhatsAppWebhookDeliveries
            .Where(x => x.Id == candidateId &&
                        ((x.Status == WhatsAppWebhookDeliveryStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                         (x.Status == WhatsAppWebhookDeliveryStatus.Processing && x.LeaseExpiresAt <= now)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, WhatsAppWebhookDeliveryStatus.Processing)
                .SetProperty(x => x.LeaseExpiresAt, leaseExpiresAt)
                .SetProperty(x => x.Attempts, x => x.Attempts + 1), cancellationToken);
        if (claimed == 0) return false;

        db.ChangeTracker.Clear();
        var delivery = await db.WhatsAppWebhookDeliveries.SingleAsync(x => x.Id == candidateId, cancellationToken);
        if (delivery.ProtectedPayload is null)
        {
            MarkFailed(delivery, "PAYLOAD_MISSING");
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        byte[] payload;
        try { payload = protector.Unprotect(delivery.ProtectedPayload); }
        catch (CryptographicException)
        {
            MarkFailed(delivery, "PAYLOAD_UNREADABLE");
            await db.SaveChangesAsync(cancellationToken);
            logger?.LogError("WhatsApp history delivery moved to failed state. DeliveryId={DeliveryId} ErrorCode={ErrorCode}", delivery.Id, delivery.LastErrorCode);
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            await conversationService.ProcessAsync(document, cancellationToken);
            delivery.Status = WhatsAppWebhookDeliveryStatus.Completed;
            delivery.ProcessedAt = DateTime.UtcNow;
            delivery.ProtectedPayload = null;
            delivery.LeaseExpiresAt = null;
            delivery.NextAttemptAt = null;
            delivery.LastErrorCode = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException)
        {
            db.ChangeTracker.Clear();
            var failed = await db.WhatsAppWebhookDeliveries.SingleAsync(x => x.Id == candidateId, cancellationToken);
            MarkFailed(failed, "INVALID_PAYLOAD");
            await db.SaveChangesAsync(cancellationToken);
            logger?.LogError("WhatsApp history delivery moved to failed state. DeliveryId={DeliveryId} ErrorCode={ErrorCode}", failed.Id, failed.LastErrorCode);
        }
        catch (WhatsAppWebhookPayloadException)
        {
            db.ChangeTracker.Clear();
            var failed = await db.WhatsAppWebhookDeliveries.SingleAsync(x => x.Id == candidateId, cancellationToken);
            MarkFailed(failed, "INVALID_PAYLOAD");
            await db.SaveChangesAsync(cancellationToken);
            logger?.LogError("WhatsApp history delivery moved to failed state. DeliveryId={DeliveryId} ErrorCode={ErrorCode}", failed.Id, failed.LastErrorCode);
        }
        catch (WhatsAppWebhookSourceMismatchException)
        {
            db.ChangeTracker.Clear();
            var failed = await db.WhatsAppWebhookDeliveries.SingleAsync(x => x.Id == candidateId, cancellationToken);
            MarkFailed(failed, "SOURCE_MISMATCH");
            await db.SaveChangesAsync(cancellationToken);
            logger?.LogError("WhatsApp history delivery moved to failed state. DeliveryId={DeliveryId} ErrorCode={ErrorCode}", failed.Id, failed.LastErrorCode);
        }
        catch (Exception exception)
        {
            db.ChangeTracker.Clear();
            var retry = await db.WhatsAppWebhookDeliveries.SingleAsync(x => x.Id == candidateId, cancellationToken);
            retry.Status = WhatsAppWebhookDeliveryStatus.Pending;
            retry.LeaseExpiresAt = null;
            retry.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, Math.Min(retry.Attempts, 9))));
            retry.LastErrorCode = "PROCESSING_RETRY";
            await db.SaveChangesAsync(cancellationToken);
            logger?.LogWarning("WhatsApp history delivery scheduled for retry. DeliveryId={DeliveryId} Attempt={Attempt} FailureType={FailureType}", retry.Id, retry.Attempts, exception.GetType().Name);
        }

        return true;
    }

    private static void MarkFailed(WhatsAppWebhookDelivery delivery, string errorCode)
    {
        delivery.Status = WhatsAppWebhookDeliveryStatus.Failed;
        delivery.LeaseExpiresAt = null;
        delivery.NextAttemptAt = null;
        delivery.LastErrorCode = errorCode;
    }
}
