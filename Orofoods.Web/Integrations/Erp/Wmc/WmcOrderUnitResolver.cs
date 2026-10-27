using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public static class WmcOrderUnitResolver
{
    public const string ConfirmedPackageUnitCode = "PC";
    public const string PackageCommercialUnitCode = "PCT";

    public static bool IsPackageItem(OrderItem item) =>
        string.Equals(item.SalesUnitCodeSnapshot, PackageCommercialUnitCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.Product?.CommercialUnitCode, PackageCommercialUnitCode, StringComparison.OrdinalIgnoreCase);

    public static bool HasConfirmedPackageUnit(Product? product) => product?.WmcUnits
        .Where(unit => string.Equals(unit.UnitCode.Trim(), ConfirmedPackageUnitCode, StringComparison.OrdinalIgnoreCase))
        .Take(2)
        .Count() == 1;

    public static string Resolve(OrderItem item)
    {
        if (IsPackageItem(item))
        {
            if (!HasConfirmedPackageUnit(item.Product))
                throw new WmcOrderMappingException("WMC_PACKAGE_UNIT_CODE_UNCONFIRMED");

            return ConfirmedPackageUnitCode;
        }

        return (item.Product?.Unit ?? string.Empty).Trim().ToUpperInvariant();
    }
}
