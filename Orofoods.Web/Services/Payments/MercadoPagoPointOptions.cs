namespace Orofoods.Web.Services.Payments;

public sealed class MercadoPagoPointOptions
{
    public const string ConfigurationKey = "Payments:MercadoPagoPointEnabled";
    public const string SectionName = "MercadoPagoPoint";

    public bool Enabled { get; set; }
    public bool StagingRealEnabled { get; set; }
    public bool ProductionEnabled { get; set; }
    public string Environment { get; set; } = "";
    public Dictionary<string, string[]> AuthorizedDeviceIdsByEnvironment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string AccessToken { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public Uri BaseAddress { get; set; } = new("https://api.mercadopago.com/");
    public string PoiType { get; set; } = "NEWLAND_N950";
    public int RequestTimeoutSeconds { get; set; } = 15;

    public IReadOnlyList<string> GetAuthorizedDeviceIds(string environmentName) =>
        AuthorizedDeviceIdsByEnvironment
            .FirstOrDefault(entry => string.Equals(entry.Key, environmentName, StringComparison.OrdinalIgnoreCase))
            .Value ?? [];

    public bool IsDeviceIdAuthorized(string environmentName, string? deviceId) =>
        !string.IsNullOrWhiteSpace(deviceId)
        && GetAuthorizedDeviceIds(environmentName).Contains(deviceId, StringComparer.Ordinal);
}
