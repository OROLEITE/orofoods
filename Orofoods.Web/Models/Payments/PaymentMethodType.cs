namespace Orofoods.Web.Models.Payments;

public enum PaymentMethodType
{
    /// <summary>No safe, non-inferred method could be determined (e.g. historical rows predating this field).</summary>
    Legacy = 0,
    Pix = 1,
    CreditCard = 2,
    Cash = 3,
    Boleto = 4
}
