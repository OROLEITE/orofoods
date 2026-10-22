using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Tests.Services;

public sealed class OrderMinimumPolicyTests
{
    public static IEnumerable<object?[]> MinimumCases => new object?[][]
    {
        [null, 0m, 160m],
        [0m, 0m, 160m],
        [160m, 0m, 160m],
        [200m, 0m, 200m],
        [160m, 250m, 250m]
    };

    [Theory]
    [MemberData(nameof(MinimumCases))]
    public void Resolves_global_minimum_and_preserves_stricter_customer_minimum(decimal? configured, decimal customerMinimum, decimal expected)
    {
        Assert.Equal(expected, OrderMinimumPolicy.Resolve(configured, customerMinimum));
    }

    [Theory]
    [InlineData(159.99, false)]
    [InlineData(160, true)]
    [InlineData(200, true)]
    public void Confirmation_uses_effective_item_subtotal_without_freight(decimal subtotal, bool expected)
    {
        Assert.Equal(expected, OrderMinimumPolicy.MeetsMinimum(subtotal, 160m));
    }
}
