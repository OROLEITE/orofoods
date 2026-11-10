namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcOrderFileOptions
{
    public const string SectionName = "WmcOrderFile";

    public string SupplierCnpj { get; set; } = "";

    public static bool HasValidSupplierCnpj(WmcOrderFileOptions options) =>
        options.SupplierCnpj is { Length: 14 } && options.SupplierCnpj.All(char.IsAsciiDigit);
}
