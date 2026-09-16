using Orofoods.Web.Models.Contact;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Models;

public class ContactMessageTests
{
    [Fact]
    public void Contact_message_sets_creation_time_when_created()
    {
        var message = new ContactMessage();

        Assert.InRange(message.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Contact_message_can_be_persisted()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ContactMessages.Add(new ContactMessage
        {
            Name = "Ana Souza",
            Email = "ana@burger.com",
            Phone = "(19) 3000-1000",
            City = "Campinas",
            Message = "Quero conhecer a Orofoods.",
            PrivacyConsentAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        Assert.Single(db.ContactMessages);
    }
}
