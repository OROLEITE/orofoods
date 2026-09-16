using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Contact;

public class ContactMessage
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(120)]
    public string Company { get; set; } = "";

    [MaxLength(30)]
    public string Phone { get; set; } = "";

    [MaxLength(30)]
    public string WhatsApp { get; set; } = "";

    [MaxLength(160)]
    public string Email { get; set; } = "";

    [MaxLength(80)]
    public string City { get; set; } = "";

    [MaxLength(1500)]
    public string Message { get; set; } = "";

    public DateTimeOffset PrivacyConsentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
