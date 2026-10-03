using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Orofoods.Web.ViewModels;

public sealed record AdminUserRow(ApplicationUser User, string Role, string? Customer, string? SalesRepresentative);

public sealed class AdminUserEditViewModel
{
    [Required, StringLength(256)]
    public string Id { get; set; } = "";
    [Required, StringLength(256)]
    public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";
    [Required]
    public string ConcurrencyStamp { get; set; } = "";
    public bool IsActive { get; set; }
    public int? CustomerId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    [Required]
    public string Role { get; set; } = "Cliente";
    public List<SelectListItem> Customers { get; set; } = [];
    public List<SelectListItem> SalesRepresentatives { get; set; } = [];
    public List<SelectListItem> Roles { get; set; } = [];
}

public sealed class AdminUserCreateViewModel
{
    [Required, StringLength(256)]
    public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";
    [Required]
    public string Role { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int? CustomerId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
    public List<SelectListItem> Customers { get; set; } = [];
    public List<SelectListItem> SalesRepresentatives { get; set; } = [];
    public List<SelectListItem> Roles { get; set; } = [];
}

public sealed class AdminUserListViewModel
{
    public IReadOnlyList<AdminUserRow> Users { get; init; } = [];
    public string? Query { get; init; }
    public string? Role { get; init; }
    public bool? IsActive { get; init; }
    public List<SelectListItem> Roles { get; init; } = [];
}

public sealed class AdminUserResetPasswordViewModel
{
    [Required]
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
    public bool ConfirmReset { get; set; }
}
