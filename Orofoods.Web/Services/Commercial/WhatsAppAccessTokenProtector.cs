using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Services.Commercial;

public sealed class WhatsAppAccessTokenProtector
{
    private readonly IDataProtector protector;

    public WhatsAppAccessTokenProtector(IDataProtectionProvider provider) =>
        protector = provider.CreateProtector("Orofoods.WhatsAppBusiness.AccessToken.v1");

    public string Protect(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return protector.Protect(accessToken);
    }

    public string Unprotect(string protectedAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedAccessToken);
        return protector.Unprotect(protectedAccessToken);
    }
}

public interface IWhatsAppAccessTokenProvider
{
    Task<string> GetAsync(CancellationToken cancellationToken = default);
}

public interface IWhatsAppPhoneNumberIdProvider
{
    Task<string> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class WhatsAppAccessTokenProvider(
    ApplicationDbContext db,
    WhatsAppAccessTokenProtector protector,
    IOptions<WhatsAppBusinessOptions> options) : IWhatsAppAccessTokenProvider
{
    public async Task<string> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.EmbeddedSignupEnabled)
            return GetConfiguredToken();

        var connection = await WhatsAppBusinessConnectionResolver.GetValidAsync(db, options.Value, cancellationToken);

        try
        {
            var accessToken = protector.Unprotect(connection.ProtectedAccessToken);
            if (!string.IsNullOrWhiteSpace(accessToken)) return accessToken;
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("WhatsApp Business não está configurado.", exception);
        }

        throw new InvalidOperationException("WhatsApp Business não está configurado.");
    }

    private string GetConfiguredToken()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.AccessToken))
            return options.Value.AccessToken;
        throw new InvalidOperationException("WhatsApp Business não está configurado.");
    }
}

public sealed class WhatsAppPhoneNumberIdProvider(
    ApplicationDbContext db,
    IOptions<WhatsAppBusinessOptions> options) : IWhatsAppPhoneNumberIdProvider
{
    public async Task<string> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.EmbeddedSignupEnabled)
            return options.Value.PhoneNumberId;

        var connection = await WhatsAppBusinessConnectionResolver.GetValidAsync(db, options.Value, cancellationToken);
        return connection.PhoneNumberId;
    }
}

internal static class WhatsAppBusinessConnectionResolver
{
    public static async Task<WhatsAppBusinessConnection> GetValidAsync(
        ApplicationDbContext db,
        WhatsAppBusinessOptions options,
        CancellationToken cancellationToken)
    {
        var connection = await db.WhatsAppBusinessConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == WhatsAppBusinessConnection.SingletonId, cancellationToken);
        if (connection is null)
            throw new InvalidOperationException("WhatsApp Business não está configurado.");

        var connectedDisplayPhoneNumber = NormalizePhone(connection.DisplayPhoneNumber);
        var configuredDisplayPhoneNumber = NormalizePhone(options.EmbeddedSignupDisplayPhoneNumber);

        if (string.IsNullOrWhiteSpace(connection.PhoneNumberId) ||
            !string.Equals(connection.PhoneNumberId, options.EmbeddedSignupPhoneNumberId, StringComparison.Ordinal) ||
            connectedDisplayPhoneNumber.Length == 0 ||
            configuredDisplayPhoneNumber.Length == 0 ||
            connectedDisplayPhoneNumber != configuredDisplayPhoneNumber ||
            string.IsNullOrWhiteSpace(connection.ProtectedAccessToken))
            throw new InvalidOperationException("WhatsApp Business não está configurado.");

        return connection;
    }

    private static string NormalizePhone(string? value) => new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
}
