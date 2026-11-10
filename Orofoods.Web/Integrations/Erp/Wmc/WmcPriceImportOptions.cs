namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcPriceImportOptions
{
    public const string SectionName = "WmcPriceImport";

    /// <summary>Only true after the WMC price source has been reconciled with Lista 1.</summary>
    public bool SourcePriceValidated { get; set; }

    /// <summary>Exact WMC unit in which PRECOVENDA is expressed; no PC/PCT/CX conversion is implicit.</summary>
    public string? SourcePriceUnitCode { get; set; }

    /// <summary>Explicit Orofoods price table allowed to receive the validated source price.</summary>
    public int? TargetPriceTableId { get; set; }
}
