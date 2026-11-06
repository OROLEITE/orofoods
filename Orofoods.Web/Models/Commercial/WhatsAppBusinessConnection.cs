using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Commercial;

/// <summary>The single WhatsApp Cloud API number connected to this Orofoods instance.</summary>
public sealed class WhatsAppBusinessConnection
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    [MaxLength(128)]
    public string BusinessAccountId { get; set; } = "";

    [MaxLength(128)]
    public string PhoneNumberId { get; set; } = "";

    [MaxLength(30)]
    public string DisplayPhoneNumber { get; set; } = "";

    [MaxLength(8192)]
    public string ProtectedAccessToken { get; set; } = "";

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
}
