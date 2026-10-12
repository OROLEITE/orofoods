using Orofoods.Web.Models.Commercial;
using Xunit;

namespace Orofoods.Web.Tests.Services;

public class WhatsAppConversationLookupTests
{
    [Fact]
    public void Customer_id_match_wins_over_phone_fallback()
    {
        var conversations = new[]
        {
            new WhatsAppConversation { Id = 11, CustomerId = 42, PhoneNumber = "5511999990001" },
            new WhatsAppConversation { Id = 12, PhoneNumber = "5511999990001" }
        };

        Assert.Equal(11L, Resolve(conversations, 42, "(11) 99999-0001", null));
    }

    [Fact]
    public void Fallback_matches_equivalent_phone_formats_only_when_unique()
    {
        var conversations = new[]
        {
            new WhatsAppConversation { Id = 21, PhoneNumber = "5511999990001" }
        };

        Assert.Equal(21L, Resolve(conversations, 42, "+55 (11) 99999-0001", null));
    }

    [Fact]
    public void Ambiguous_or_missing_fallback_does_not_select_a_conversation()
    {
        var ambiguous = new[]
        {
            new WhatsAppConversation { Id = 31, PhoneNumber = "5511999990001" },
            new WhatsAppConversation { Id = 32, PhoneNumber = "5511999990001" }
        };

        Assert.Null(Resolve(ambiguous, 42, "5511999990001", null));
        Assert.Null(Resolve([], 42, null, null));
    }

    private static long? Resolve(
        IReadOnlyCollection<WhatsAppConversation> conversations,
        int customerId,
        string? whatsapp,
        string? phone)
    {
        var type = typeof(WhatsAppConversation).Assembly.GetType("Orofoods.Web.Services.Commercial.WhatsAppConversationLookup");
        Assert.NotNull(type);
        var method = type!.GetMethod("FindConversationId");
        Assert.NotNull(method);
        return (long?)method!.Invoke(null, [conversations, customerId, whatsapp, phone]);
    }
}
