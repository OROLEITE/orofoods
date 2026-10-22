namespace Orofoods.Web.Services.Orders;

public static class OrderMinimumPolicy
{
    public static decimal Resolve(decimal? configuredMinimum, decimal customerMinimum) =>
        Math.Max(configuredMinimum is > 0 ? configuredMinimum.Value : 160m, Math.Max(0m, customerMinimum));

    public static bool MeetsMinimum(decimal discountedItemSubtotal, decimal minimum) =>
        discountedItemSubtotal >= minimum;
}
