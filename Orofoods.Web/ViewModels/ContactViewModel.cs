using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.ViewModels;

public class ContactViewModel
{
    [Required, StringLength(120)]
    public string Name { get; set; } = "";

    [StringLength(120)]
    public string Company { get; set; } = "";

    [Required, StringLength(30)]
    public string Phone { get; set; } = "";

    [StringLength(30)]
    public string WhatsApp { get; set; } = "";

    [Required, EmailAddress, StringLength(160)]
    public string Email { get; set; } = "";

    [Required, StringLength(80)]
    public string City { get; set; } = "";

    [Required, StringLength(1500)]
    public string Message { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirme que leu a Política de Privacidade.")]
    public bool AcceptPrivacyPolicy { get; set; }
}
