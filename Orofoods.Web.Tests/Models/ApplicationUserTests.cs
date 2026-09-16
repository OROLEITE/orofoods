using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Tests.Models;

public class ApplicationUserTests
{
    [Fact]
    public void New_user_is_active_and_can_link_to_customer_and_sales_representative()
    {
        var user = new ApplicationUser
        {
            CustomerId = 12,
            SalesRepresentativeId = 7
        };

        Assert.True(user.IsActive);
        Assert.Equal(12, user.CustomerId);
        Assert.Equal(7, user.SalesRepresentativeId);
    }
}
