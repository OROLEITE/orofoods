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
        var connection = await db.WhatsAppBusinessConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == WhatsAppBusinessConnection.SingletonId, cancellationToken);

        if (connection is not null)
        {
            try
            {
                return protector.Unprotect(connection.ProtectedAccessToken);
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException("O token da conexão WhatsApp não pôde ser recuperado com segurança.", exception);
            }
        }

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
        var connectedPhoneNumberId = await db.WhatsAppBusinessConnections
            .AsNoTracking()
            .Where(x => x.Id == WhatsAppBusinessConnection.SingletonId)
            .Select(x => x.PhoneNumberId)
            .SingleOrDefaultAsync(cancellationToken);

        return !string.IsNullOrWhiteSpace(connectedPhoneNumberId)
            ? connectedPhoneNumberId
            : options.Value.PhoneNumberId;
    }
}
