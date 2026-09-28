namespace Orofoods.Web.Services.Payments;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";
    public const string HttpClientName = "MercadoPago";

    public string AccessToken { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public Uri BaseAddress { get; set; } = new("https://api.mercadopago.com/");
    public TimeSpan PixExpiration { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan WebhookSignatureTolerance { get; set; } = TimeSpan.FromMinutes(5);
}
