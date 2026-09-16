using Microsoft.AspNetCore.Mvc.Rendering;

namespace Orofoods.Web.ViewModels;

public sealed record AdminUserRow(ApplicationUser User, string Role, string? Customer, string? SalesRepresentative);

public sealed class AdminUserEditViewModel
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; }
    public int? CustomerId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public string Role { get; set; } = "Cliente";
    public List<SelectListItem> Customers { get; set; } = [];
    public List<SelectListItem> SalesRepresentatives { get; set; } = [];
    public List<SelectListItem> Roles { get; set; } = [];
}
