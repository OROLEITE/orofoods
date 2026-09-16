namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcFileDropOptions
{
    public const string SectionName = "WmcFileDrop";

    public bool Enabled { get; set; }
    public bool AutoRetryEnabled { get; set; }
    public string OutputDirectory { get; set; } = "";
}
