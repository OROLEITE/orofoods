using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Services.Commercial;

public sealed record WhatsAppEmbeddedSignupResult(bool Connected, bool ContactsSyncRequested, bool HistorySyncRequested);

public sealed class WhatsAppBusinessConnectionService(
    ApplicationDbContext db,
    IMetaWhatsAppEmbeddedSignupClient metaClient,
    WhatsAppAccessTokenProtector tokenProtector,
    IOptions<WhatsAppBusinessOptions> options)
{
    public async Task<WhatsAppEmbeddedSignupResult> CompleteAsync(
        string eventType,
        string authorizationCode,
        string businessAccountId,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsEmbeddedSignupReady)
            throw new WhatsAppEmbeddedSignupValidationException();

        if (!string.Equals(eventType, WhatsAppEmbeddedSignupEvents.FinishBusinessAppOnboarding, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(authorizationCode) ||
            string.IsNullOrWhiteSpace(businessAccountId) ||
            businessAccountId.Length > 128 ||
            !businessAccountId.All(char.IsAsciiDigit))
            throw new WhatsAppEmbeddedSignupValidationException();

        var accessToken = await metaClient.ExchangeCodeAsync(authorizationCode, cancellationToken);
        var phoneNumbers = await metaClient.GetPhoneNumbersAsync(businessAccountId, accessToken, cancellationToken);
        var configuredPhone = phoneNumbers.SingleOrDefault(number =>
            string.Equals(number.Id, settings.EmbeddedSignupPhoneNumberId, StringComparison.Ordinal));
        if (configuredPhone is null ||
            NormalizePhone(configuredPhone.DisplayPhoneNumber) != NormalizePhone(settings.EmbeddedSignupDisplayPhoneNumber))
            throw new WhatsAppEmbeddedSignupValidationException();

        await metaClient.SubscribeAppAsync(businessAccountId, accessToken, cancellationToken);

        var connection = await db.WhatsAppBusinessConnections
            .SingleOrDefaultAsync(x => x.Id == WhatsAppBusinessConnection.SingletonId, cancellationToken);
        if (connection is null)
        {
            connection = new WhatsAppBusinessConnection { Id = WhatsAppBusinessConnection.SingletonId };
            db.WhatsAppBusinessConnections.Add(connection);
        }

        connection.BusinessAccountId = businessAccountId;
        connection.PhoneNumberId = configuredPhone.Id;
        connection.DisplayPhoneNumber = configuredPhone.DisplayPhoneNumber;
        connection.ProtectedAccessToken = tokenProtector.Protect(accessToken);
        connection.ConnectedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var historyRequested = await TryRequestSyncAsync(configuredPhone.Id, accessToken, "history", cancellationToken);
        return new WhatsAppEmbeddedSignupResult(true, false, historyRequested);
    }

    private async Task<bool> TryRequestSyncAsync(string phoneNumberId, string accessToken, string syncType, CancellationToken cancellationToken)
    {
        try
        {
            await metaClient.RequestCoexistenceSyncAsync(phoneNumberId, accessToken, syncType, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string NormalizePhone(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
}

public static class WhatsAppEmbeddedSignupEvents
{
    public const string FinishBusinessAppOnboarding = "FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING";
}

public sealed class WhatsAppEmbeddedSignupValidationException() : Exception("A sessão de conexão WhatsApp não pôde ser validada.");
