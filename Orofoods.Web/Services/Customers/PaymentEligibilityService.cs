using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Services.Customers;

public interface IPaymentEligibilityService
{
    Task<int> GetValidPurchaseCountAsync(int customerId, CancellationToken cancellationToken = default);
    Task<PaymentEligibilityResult> GetAvailablePaymentOptionsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<PaymentEligibilityValidationResult> ValidateAsync(int customerId, int paymentTermId, CancellationToken cancellationToken = default);
}

public sealed class PaymentEligibilityService(
    ApplicationDbContext db,
    IOptions<PaymentEligibilityOptions> options) : IPaymentEligibilityService
{
    public Task<int> GetValidPurchaseCountAsync(int customerId, CancellationToken cancellationToken = default) =>
        db.Orders.AsNoTracking().CountAsync(
            order => order.CustomerId == customerId && options.Value.ValidPurchaseStatuses.Contains(order.Status),
            cancellationToken);

    public async Task<PaymentEligibilityResult> GetAvailablePaymentOptionsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Customer not found.");
        var validPurchases = await GetValidPurchaseCountAsync(customerId, cancellationToken);
        var maximumTermDays = GetMaximumTermDays(customer, validPurchases);
        var paymentMethods = await db.PaymentTerms.AsNoTracking()
            .Where(term => term.IsActive && term.DaysUntilDue <= maximumTermDays)
            .OrderBy(term => term.SortOrder)
            .ToListAsync(cancellationToken);

        return new PaymentEligibilityResult(
            validPurchases,
            maximumTermDays > 0,
            maximumTermDays,
            customer.CreditBlocked,
            customer.CreditOverrideEnabled,
            paymentMethods);
    }

    public async Task<PaymentEligibilityValidationResult> ValidateAsync(int customerId, int paymentTermId, CancellationToken cancellationToken = default)
    {
        var eligibility = await GetAvailablePaymentOptionsAsync(customerId, cancellationToken);
        if (eligibility.PaymentMethods.Any(term => term.Id == paymentTermId))
        {
            return PaymentEligibilityValidationResult.Allowed;
        }

        var selectedTerm = await db.PaymentTerms.AsNoTracking().SingleOrDefaultAsync(term => term.Id == paymentTermId, cancellationToken);
        if (selectedTerm is not null && selectedTerm.DaysUntilDue > 0 && eligibility.ValidPurchases < options.Value.InitialCashOnlyPurchaseCount)
        {
            return new PaymentEligibilityValidationResult(false, "Pagamento via boleto a prazo será disponibilizado após a conclusão das três primeiras compras. Para este pedido, selecione PIX, pagamento à vista ou cartão de crédito.");
        }

        return new PaymentEligibilityValidationResult(false, "Esta condição de pagamento não está disponível para este cliente.");
    }

    private int GetMaximumTermDays(Customer customer, int validPurchases)
    {
        if (customer.CreditBlocked)
        {
            return 0;
        }

        if (customer.CreditOverrideEnabled)
        {
            return Math.Clamp(customer.MaximumPaymentTermDays ?? 0, 0, options.Value.AbsoluteMaximumTermDays);
        }

        return validPurchases >= options.Value.InitialCashOnlyPurchaseCount
            ? options.Value.AutomaticMaximumTermDays
            : 0;
    }
}

public sealed class PaymentEligibilityOptions
{
    public const string SectionName = "PaymentEligibility";
    public int InitialCashOnlyPurchaseCount { get; set; } = 3;
    public int AutomaticMaximumTermDays { get; set; } = 14;
    public int AbsoluteMaximumTermDays { get; set; } = 14;
    public HashSet<OrderStatus> ValidPurchaseStatuses { get; set; } = [OrderStatus.Invoiced, OrderStatus.Delivered];
    public PaymentTermBaseDate PaymentTermBaseDate { get; set; } = PaymentTermBaseDate.InvoiceDate;
}

public enum PaymentTermBaseDate
{
    InvoiceDate,
    DeliveryDate
}

public sealed record PaymentEligibilityResult(
    int ValidPurchases,
    bool InvoiceCreditEnabled,
    int MaximumTermDays,
    bool CreditBlocked,
    bool CreditOverrideEnabled,
    IReadOnlyList<PaymentTerm> PaymentMethods);

public sealed record PaymentEligibilityValidationResult(bool IsAllowed, string? ErrorMessage)
{
    public static PaymentEligibilityValidationResult Allowed { get; } = new(true, null);
}