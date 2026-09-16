using Microsoft.AspNetCore.Identity;

namespace Orofoods.Web.Models.Identity;

public class ApplicationUser : IdentityUser
{
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public SalesRepresentative? SalesRepresentative { get; set; }
    public bool IsActive { get; set; } = true;
}
