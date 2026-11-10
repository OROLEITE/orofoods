using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Services.Sellers;

public sealed class SellerOrderDraftQuoteProtector
{
    public const int MaximumProtectedQuoteLength = 262144;

    private readonly IDataProtector protector;

    public SellerOrderDraftQuoteProtector(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("Orofoods.SellerOrderDraftQuote.v1");
    }

    public string Protect(string ownerUserId, int draftId, int version, OrderPlacementQuote quote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(draftId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentNullException.ThrowIfNull(quote);
        var payload = new ProtectedSellerDraftQuote(ownerUserId, draftId, version, quote);
        return protector.Protect(JsonSerializer.Serialize(payload));
    }

    public bool TryUnprotect(string? protectedQuote, out ProtectedSellerDraftQuote? quote)
    {
        quote = null;
        if (string.IsNullOrWhiteSpace(protectedQuote) || protectedQuote.Length > MaximumProtectedQuoteLength)
        {
            return false;
        }

        try
        {
            quote = JsonSerializer.Deserialize<ProtectedSellerDraftQuote>(protector.Unprotect(protectedQuote));
            return quote is not null
                && !string.IsNullOrWhiteSpace(quote.OwnerUserId)
                && quote.DraftId > 0
                && quote.Version > 0
                && quote.Quote is not null
                && quote.Quote.Lines is not null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            return false;
        }
    }
}

public sealed record ProtectedSellerDraftQuote(
    string OwnerUserId,
    int DraftId,
    int Version,
    OrderPlacementQuote Quote);
