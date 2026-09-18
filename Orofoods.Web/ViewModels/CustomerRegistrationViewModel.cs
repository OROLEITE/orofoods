using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Orofoods.Web.ViewModels;

public class CustomerRegistrationViewModel
{
    [Required(ErrorMessage = "Informe a razão social."), Display(Name = "Razão social")]
    public string LegalName { get; set; } = "";

    [Required(ErrorMessage = "Informe o nome fantasia."), Display(Name = "Nome fantasia")]
    public string TradeName { get; set; } = "";

    [Required(ErrorMessage = "Informe o CNPJ."), Display(Name = "CNPJ")]
    public string Cnpj { get; set; } = "";

    [Display(Name = "Inscricao estadual")]
    public string? StateRegistration { get; set; }

    [Required(ErrorMessage = "Informe o responsável."), Display(Name = "Responsável")]
    public string ResponsibleName { get; set; } = "";

    [Display(Name = "CPF do responsavel")]
    public string? ResponsibleDocument { get; set; }

    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = "";

    [Required, Display(Name = "Telefone")]
    public string Phone { get; set; } = "";

    [Required, Display(Name = "WhatsApp")]
    public string WhatsApp { get; set; } = "";

    [Required, Display(Name = "CEP")]
    public string ZipCode { get; set; } = "";

    [Required, Display(Name = "Endereço")]
    public string Street { get; set; } = "";

    [Required]
    public string Number { get; set; } = "";

    public string? Complement { get; set; }

    [Required, Display(Name = "Bairro")]
    public string District { get; set; } = "";

    [Required, Display(Name = "Cidade")]
    public string City { get; set; } = "";

    [Required, Display(Name = "Estado")]
    public string State { get; set; } = "SP";

    [Required(ErrorMessage = "Informe a senha."), Display(Name = "Senha"), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Confirme a senha."), Display(Name = "Confirmar senha"), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "As senhas não coincidem.")]
    public string ConfirmPassword { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "Você precisa aceitar a política de privacidade.")]
    public bool AcceptPrivacyPolicy { get; set; }
}

public class AdminCustomerApprovalViewModel
{
    public int Id { get; set; }
    public string LegalName { get; set; } = "";
    public string TradeName { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public CustomerStatus Status { get; set; }
    public decimal MinimumOrder { get; set; }
    public decimal CreditLimit { get; set; }
    public int ValidPurchaseCount { get; set; }
    public bool InvoiceCreditEnabled { get; set; }
    public int EffectiveMaximumPaymentTermDays { get; set; }
    public DateTime? EffectiveCreditReleaseDate { get; set; }
    public bool CreditOverrideEnabled { get; set; }
    [Range(0, 14)] public int? MaximumPaymentTermDays { get; set; }
    public bool CreditBlocked { get; set; }
    [StringLength(1000)] public string? CreditNotes { get; set; }
    [Display(Name = "Codigo WMC")]
    [StringLength(30)]
    public string? WmcCode { get; set; }
    public int? PriceTableId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public string? InternalSalesUserId { get; set; }
    public List<int> PaymentTermIds { get; set; } = [];
    public List<SelectListItem> PriceTables { get; set; } = [];
    public List<SelectListItem> SalesRepresentatives { get; set; } = [];
    public List<SelectListItem> InternalSalesUsers { get; set; } = [];
    public List<SelectListItem> PaymentTerms { get; set; } = [];
}
