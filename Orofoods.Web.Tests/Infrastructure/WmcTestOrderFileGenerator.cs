using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp.Wmc;

namespace Orofoods.Web.Tests.Infrastructure;

internal static class WmcTestOrderFileGenerator
{
    internal const string HomologatedSupplierCnpj = "29475222000142";

    internal static WmcOrderFileGenerator Create(string? supplierCnpj = HomologatedSupplierCnpj) =>
        new(Options.Create(new WmcOrderFileOptions { SupplierCnpj = supplierCnpj! }));
}
