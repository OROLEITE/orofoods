using Microsoft.AspNetCore.Mvc.Rendering;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.ViewModels;

public sealed class CommercialDashboardViewModel
{
    public DateTime ReferenceDate { get; init; }
    public int CustomersToday { get; init; }
    public int PendingReturns { get; init; }
    public int OrdersToday { get; init; }
    public decimal TodayRevenue { get; init; }
    public IReadOnlyList<CommercialActivity> Activities { get; init; } = [];
    public IReadOnlyList<CommercialActivity> UpcomingActivities { get; init; } = [];
    public CommercialAttentionViewModel Attention { get; init; } = CommercialAttentionViewModel.Empty;
}

public sealed class CommercialActivityFormViewModel
{
    public int? Id { get; set; }
    public int CustomerId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public string? AssignedUserId { get; set; }
    public CommercialActivityType Type { get; set; } = CommercialActivityType.Call;
    public CommercialActivityStatus Status { get; set; } = CommercialActivityStatus.Scheduled;
    public DateTime ScheduledAt { get; set; } = DateTime.Now.AddHours(1);
    public string Title { get; set; } = "";
    public string? Notes { get; set; }
    public bool IsPriority { get; set; }
    public IReadOnlyList<SelectListItem> Customers { get; set; } = [];
    public IReadOnlyList<SelectListItem> SalesRepresentatives { get; set; } = [];
    public IReadOnlyList<SelectListItem> InternalSalesUsers { get; set; } = [];
}

public sealed class CommercialCalendarViewModel
{
    public DateTime WeekStart { get; init; }
    public int? SalesRepresentativeId { get; init; }
    public CommercialActivityType? Type { get; init; }
    public string? City { get; init; }
    public CommercialActivityStatus? Status { get; init; }
    public IReadOnlyList<CommercialActivity> Activities { get; init; } = [];
    public IReadOnlyList<SelectListItem> SalesRepresentatives { get; init; } = [];
    public IReadOnlyList<SelectListItem> Cities { get; init; } = [];
}