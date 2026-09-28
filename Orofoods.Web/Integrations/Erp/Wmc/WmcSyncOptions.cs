namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcSyncOptions
{
    public const string SectionName = "WmcSync";

    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 120;
}
