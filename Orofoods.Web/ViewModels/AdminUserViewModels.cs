using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Orofoods.Web.ViewModels;

public sealed record AdminUserRow(
    string Id,
    string FullName,
    string UserName,
    string Email,
    string ConcurrencyStamp,
    bool IsActive,
    string Role,
    string? Customer,
    string? SalesRepresentative);

public sealed class AdminUserEditViewModel
{
    [Required, StringLength(256)]
    public string Id { get; set; } = "";
    [Required, StringLength(256)]
    public string Name { get; set; } = "";
    [Required, StringLength(256)]
    public string UserName { get; set; } = "";
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
    [Required, StringLength(256)]
    public string UserName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";
    [Required]
    public string Role { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
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
    public bool MustChangePassword { get; set; } = true;
}

public sealed class AdminUserHistoryViewModel
{
    public string UserId { get; init; } = "";
    public string Name { get; init; } = "";
    public string UserName { get; init; } = "";
    public string Email { get; init; } = "";
    public IReadOnlyList<Orofoods.Web.Models.Identity.AdminUserChangeAudit> Events { get; init; } = [];
}

public sealed class ChangeTemporaryPasswordViewModel
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";
    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))]
    public string ConfirmNewPassword { get; set; } = "";
}
