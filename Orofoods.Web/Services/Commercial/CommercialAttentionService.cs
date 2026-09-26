using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Services.Commercial;

public sealed class CrmOptions
{
    public const string SectionName = "Crm";
    public int NoPurchaseDays { get; set; } = 30;
    public int RepurchaseToleranceDays { get; set; } = 3;
    public int RepurchaseHistorySize { get; set; } = 6;
}

public sealed class CommercialAttentionService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    IOptions<CrmOptions> options,
    IOptions<PaymentEligibilityOptions> paymentOptions)
{
    public async Task<CommercialAttentionViewModel> GetAsync(ClaimsPrincipal user, DateTime now, CancellationToken cancellationToken = default)
    {
        var scope = await accessService.GetScopeAsync(user, cancellationToken);
        var customersQuery = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope)
            .Where(x => x.IsActive && x.Status == CustomerStatus.Approved);
        var customers = await customersQuery
            .Include(x => x.SalesRepresentative)
            .Include(x => x.InternalSalesUser)
            .ToListAsync(cancellationToken);
        var customerIds = customers.Select(x => x.Id).ToList();
        if (customerIds.Count == 0) return CommercialAttentionViewModel.Empty;

        var validStatuses = paymentOptions.Value.ValidPurchaseStatuses;
        var purchaseRows = await db.Orders.AsNoTracking()
            .Where(x => customerIds.Contains(x.CustomerId) && validStatuses.Contains(x.Status))
            .Select(x => new PurchaseDateRow(x.CustomerId, x.CreatedAt))
            .ToListAsync(cancellationToken);
        var activities = await db.CommercialActivities.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.AssignedUser)
            .Where(x => customerIds.Contains(x.CustomerId) && x.Status != CommercialActivityStatus.Completed && x.Status != CommercialActivityStatus.Cancelled)
            .OrderBy(x => x.ScheduledAt)
            .ToListAsync(cancellationToken);

        var purchaseMap = purchaseRows.GroupBy(x => x.CustomerId).ToDictionary(x => x.Key, x => x.Select(row => row.CreatedAt).OrderByDescending(x => x).ToList());
        var items = customers.Select(customer => BuildItem(customer, purchaseMap.GetValueOrDefault(customer.Id) ?? [], now)).ToList();
        var overdue = activities.Where(x => x.ScheduledAt < now).Select(x => BuildActivityItem(x, "RETORNO VENCIDO", "Alta")).ToList();
        var today = activities.Where(x => x.ScheduledAt.Date == now.Date).Select(x => BuildActivityItem(x, "ATENDIMENTO DE HOJE", "Alta")).ToList();
        var attention = items.Where(x => x.NeedsAttention).OrderByDescending(x => x.Priority == "Alta").ThenByDescending(x => x.DaysSinceLastPurchase ?? -1).ToList();

        return new CommercialAttentionViewModel(
            TodayAppointments: today.Count,
            OverdueReturns: overdue.Count,
            CustomersWithoutPurchase: items.Count(x => x.DaysSinceLastPurchase is null || x.DaysSinceLastPurchase >= options.Value.NoPurchaseDays),
            RepurchasesDue: items.Count(x => x.IsRepurchaseDue),
            NewCustomers: items.Count(x => x.IsFirstContact),
            Routine: overdue.Concat(today).Concat(attention.Select(x => new CommercialRoutineItemViewModel(x.CustomerId, x.CustomerName, x.Situation, x.Priority, $"{x.DaysSinceLastPurchase?.ToString() ?? "0"} dias sem comprar", x.LastPurchaseAt ?? now, null))).DistinctBy(x => $"{x.CustomerId}:{x.Kind}").Take(50).ToList(),
            Customers: items);
    }

    private CommercialAttentionCustomerViewModel BuildItem(Customer customer, IReadOnlyList<DateTime> dates, DateTime now)
    {
        var lastPurchase = dates.FirstOrDefault();
        int? days = dates.Count == 0 ? null : Math.Max(0, (int)(now.Date - lastPurchase.Date).TotalDays);
        var recent = dates.Take(Math.Max(1, options.Value.RepurchaseHistorySize)).OrderBy(x => x).ToList();
        var intervals = recent.Zip(recent.Skip(1), (first, second) => (second - first).Days).Where(x => x > 0).ToList();
        int? average = intervals.Count >= 2 ? (int)Math.Round(intervals.Average()) : null;
        var repurchaseDue = average.HasValue && days >= average.Value + options.Value.RepurchaseToleranceDays;
        var firstContact = dates.Count == 0;
        var noPurchase = days is null || days >= options.Value.NoPurchaseDays;
        var kind = repurchaseDue ? "POSSÍVEL RECOMPRA" : firstContact ? "REALIZAR PRIMEIRO CONTATO" : noPurchase ? "SEM COMPRA" : "ACOMPANHAR";
        var priority = firstContact || (days ?? 0) >= options.Value.NoPurchaseDays * 2 ? "Alta" : "Normal";
        return new CommercialAttentionCustomerViewModel(customer.Id, customer.TradeName, customer.SalesRepresentative?.Name, customer.InternalSalesUser?.Email, lastPurchase, days, average, repurchaseDue, firstContact, noPurchase, noPurchase || repurchaseDue || firstContact, kind, priority);
    }

    private static CommercialRoutineItemViewModel BuildActivityItem(CommercialActivity activity, string kind, string priority) =>
        new(activity.CustomerId, activity.Customer.TradeName, kind, priority, activity.Title, activity.ScheduledAt, activity.Id);

    private sealed record PurchaseDateRow(int CustomerId, DateTime CreatedAt);
}

public sealed record CommercialAttentionViewModel(
    int TodayAppointments,
    int OverdueReturns,
    int CustomersWithoutPurchase,
    int RepurchasesDue,
    int NewCustomers,
    IReadOnlyList<CommercialRoutineItemViewModel> Routine,
    IReadOnlyList<CommercialAttentionCustomerViewModel> Customers)
{
    public static CommercialAttentionViewModel Empty { get; } = new(0, 0, 0, 0, 0, [], []);
}

public sealed record CommercialAttentionCustomerViewModel(
    int CustomerId,
    string CustomerName,
    string? SalesRepresentativeName,
    string? InternalUserName,
    DateTime? LastPurchaseAt,
    int? DaysSinceLastPurchase,
    int? AveragePurchaseIntervalDays,
    bool IsRepurchaseDue,
    bool IsFirstContact,
    bool IsNoPurchase,
    bool NeedsAttention,
    string Situation,
    string Priority);

public sealed record CommercialRoutineItemViewModel(
    int CustomerId,
    string CustomerName,
    string Kind,
    string Priority,
    string Title,
    DateTime ScheduledAt,
    int? ActivityId);
