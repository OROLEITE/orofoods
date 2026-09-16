using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;

namespace Orofoods.Web.Tests.Models;

public class WmcMappingTests
{
    [Fact]
    public void Customer_and_product_store_wmc_codes()
    {
        var customer = new Customer { WmcCode = "107072" };
        var product = new Product { WmcCode = "610601552" };

        Assert.Equal("107072", customer.WmcCode);
        Assert.Equal("610601552", product.WmcCode);
    }
}
