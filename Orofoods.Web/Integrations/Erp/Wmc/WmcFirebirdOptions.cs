namespace Orofoods.Web.Integrations.Erp.Wmc;

/// <summary>Read-only connection settings for the WMC Firebird mirror. Populated via User Secrets/environment variables only.</summary>
public sealed class WmcFirebirdOptions
{
    public const string SectionName = "WmcFirebird";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 3050;
    public string Database { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string Charset { get; set; } = "UTF8";
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int CommandTimeoutSeconds { get; set; } = 30;
}
