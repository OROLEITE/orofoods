namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcJsonOrderOptions
{
    public const string SectionName = "WmcJsonOrder";

    public bool Enabled { get; set; }
    public bool HomologationWorkerEnabled { get; set; }
    public int? HomologationOrderId { get; set; }
    public bool HomologationOrderIdConfirmedUnique { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public Dictionary<int, int> SalesRepresentativeCodes { get; set; } = [];
    public Dictionary<int, string> SalesRepresentativeSerials { get; set; } = [];
}
