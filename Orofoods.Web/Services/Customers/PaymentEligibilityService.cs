using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;

namespace Orofoods.Web.Services.Customers;

public interface IPaymentEligibilityService
{
    Task<int> GetValidPurchaseCountAsync(int customerId, CancellationToken cancellationToken = default);
    Task<PaymentEligibilityResult> GetAvailablePaymentOptionsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<PaymentEligibilityValidationResult> ValidateAsync(int customerId, int paymentTermId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps immediate payment modalities local while consulting the read-only WMC mirror for the one
/// customer-authorized credit condition. No authorization result is cached between requests.
/// </summary>
public sealed class PaymentEligibilityService(
    ApplicationDbContext db,
    IOptions<PaymentEligibilityOptions> options,
    IWmcCustomerPaymentConditionReader? wmcReader = null,
    ILogger<PaymentEligibilityService>? logger = null) : IPaymentEligibilityService
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
        var wmcCondition = await ReadWmcConditionAsync(customer.WmcCode, cancellationToken);

        var paymentMethods = await db.PaymentTerms.AsNoTracking()
            .Where(term => term.IsActive
                && term.DaysUntilDue == 0
                && (term.Code == "PIX"
                    || term.Code == "CASH"
                    || term.Code == "CREDIT_CARD"
                    || (options.Value.CardOnDeliveryEnabled && term.Code == "CARD_ON_DELIVERY")))
            .OrderBy(term => term.SortOrder)
            .ToListAsync(cancellationToken);

        var authorizedDays = ResolveAuthorizedCreditTerm(wmcCondition);
        var matchingAuthorizedTerms = new List<PaymentTerm>();
        if (authorizedDays is { } days)
        {
            matchingAuthorizedTerms = await db.PaymentTerms.AsNoTracking()
                .Where(term => term.IsActive
                    && term.DaysUntilDue == days
                    && term.Code == $"BOLETO_{days}D")
                .OrderBy(term => term.SortOrder)
                .ToListAsync(cancellationToken);
        }

        var authorizedTerm = matchingAuthorizedTerms.Count == 1 ? matchingAuthorizedTerms[0] : null;

        if (authorizedTerm is not null)
        {
            paymentMethods.Add(authorizedTerm);
        }

        return new PaymentEligibilityResult(
            validPurchases,
            authorizedTerm is not null,
            authorizedTerm?.DaysUntilDue ?? 0,
            paymentMethods,
            wmcCondition);
    }

    public async Task<PaymentEligibilityValidationResult> ValidateAsync(int customerId, int paymentTermId, CancellationToken cancellationToken = default)
    {
        var eligibility = await GetAvailablePaymentOptionsAsync(customerId, cancellationToken);
        if (eligibility.PaymentMethods.Any(term => term.Id == paymentTermId))
        {
            return new PaymentEligibilityValidationResult(true, null, eligibility.WmcPaymentCondition);
        }

        var selectedTerm = await db.PaymentTerms.AsNoTracking().SingleOrDefaultAsync(term => term.Id == paymentTermId, cancellationToken);
        return selectedTerm is { DaysUntilDue: > 0 }
            ? new PaymentEligibilityValidationResult(false, "Esta condição a prazo não está autorizada ou disponível no WMC para este cliente.", eligibility.WmcPaymentCondition)
            : new PaymentEligibilityValidationResult(false, "Esta condição de pagamento não está disponível para este cliente.", eligibility.WmcPaymentCondition);
    }

    private async Task<WmcCustomerPaymentCondition?> ReadWmcConditionAsync(string? customerCode, CancellationToken cancellationToken)
    {
        if (wmcReader is null || string.IsNullOrWhiteSpace(customerCode))
        {
            return null;
        }

        try
        {
            var condition = await wmcReader.GetForCustomerAsync(customerCode.Trim(), cancellationToken);
            if (condition is not null)
            {
                logger?.LogInformation(
                    "Condição de pagamento consultada no {Source} para o cliente WMC {CustomerCode}; condição {ConditionCode}; consulta UTC {ReadAtUtc:O}; sincronização do espelho UTC {SynchronizationAtUtc:O}.",
                    condition.Source,
                    condition.CustomerCode,
                    condition.ConditionCode,
                    condition.ReadAtUtc,
                    condition.SynchronizationAtUtc);
            }

            return condition;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogWarning(exception, "Falha ao consultar condição de pagamento do cliente WMC {CustomerCode}; prazo bloqueado.", customerCode);
            return null;
        }
    }

    private static int? ResolveAuthorizedCreditTerm(WmcCustomerPaymentCondition? condition)
    {
        if (condition is null
            || !string.Equals(condition.CustomerConditionCode, condition.ConditionCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(condition.Situation?.Trim(), "A", StringComparison.OrdinalIgnoreCase)
            || condition.InstallmentCount != 1
            || condition.FirstInstallmentDays is not (> 0 and <= 999))
        {
            return null;
        }

        return condition.FirstInstallmentDays;
    }
}

public sealed class PaymentEligibilityOptions
{
    public const string SectionName = "PaymentEligibility";
    public HashSet<OrderStatus> ValidPurchaseStatuses { get; set; } = [OrderStatus.Invoiced, OrderStatus.Delivered];
    public PaymentTermBaseDate PaymentTermBaseDate { get; set; } = PaymentTermBaseDate.InvoiceDate;
    public bool CardOnDeliveryEnabled { get; set; }
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
    IReadOnlyList<PaymentTerm> PaymentMethods,
    WmcCustomerPaymentCondition? WmcPaymentCondition = null);

public sealed record PaymentEligibilityValidationResult(bool IsAllowed, string? ErrorMessage, WmcCustomerPaymentCondition? WmcPaymentCondition = null)
{
    public static PaymentEligibilityValidationResult Allowed { get; } = new(true, null);
}
