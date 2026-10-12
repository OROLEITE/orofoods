using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Services.Commercial;

public static class WhatsAppConversationLookup
{
    public static long? FindConversationId(
        IEnumerable<WhatsAppConversation> conversations,
        int customerId,
        string? whatsapp,
        string? phone)
    {
        var customerMatch = conversations
            .Where(conversation => conversation.CustomerId == customerId)
            .OrderByDescending(conversation => conversation.LastMessageAt)
            .ThenByDescending(conversation => conversation.Id)
            .FirstOrDefault();
        if (customerMatch is not null)
        {
            return customerMatch.Id;
        }

        var normalizedPhones = new[]
        {
            WhatsAppConversationService.TryNormalizePhone(whatsapp),
            WhatsAppConversationService.TryNormalizePhone(phone)
        }
        .Where(value => value is not null)
        .Select(value => value!)
        .Distinct(StringComparer.Ordinal)
        .ToHashSet(StringComparer.Ordinal);
        if (normalizedPhones.Count == 0)
        {
            return null;
        }

        var fallbackMatches = conversations
            .Where(conversation => conversation.CustomerId is null && normalizedPhones.Contains(conversation.PhoneNumber))
            .Select(conversation => conversation.Id)
            .Distinct()
            .Take(2)
            .ToList();

        return fallbackMatches.Count == 1 ? fallbackMatches[0] : null;
    }

    public static bool HasValidPhone(string? whatsapp, string? phone) =>
        WhatsAppConversationService.TryNormalizePhone(whatsapp) is not null ||
        WhatsAppConversationService.TryNormalizePhone(phone) is not null;
}
