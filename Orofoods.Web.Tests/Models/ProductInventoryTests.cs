using Orofoods.Web.Models.Inventory;

namespace Orofoods.Web.Tests.Models;

public class ProductInventoryTests
{
    [Fact]
    public void AvailableQuantity_SubtractsReservedCases()
    {
        var inventory = new ProductInventory
        {
            QuantityOnHand = 12,
            QuantityReserved = 5
        };

        Assert.Equal(7, inventory.AvailableQuantity);
    }
}
