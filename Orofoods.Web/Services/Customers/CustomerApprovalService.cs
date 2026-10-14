using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Customers;

public class CustomerApprovalService(ApplicationDbContext db)
{
    public async Task<CustomerApprovalResult> ApplyAsync(CustomerApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers
            .Include(x => x.CustomerPaymentTerms)
            .SingleAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (request.Cnpj is not null
            && !string.Equals(request.Cnpj.Trim(), customer.Cnpj.Trim(), StringComparison.Ordinal)
            && !(CnpjValidator.TryNormalize(request.Cnpj, out var requestedNormalizedCnpj)
                && CnpjValidator.TryNormalize(customer.Cnpj, out var currentNormalizedCnpj)
                && requestedNormalizedCnpj == currentNormalizedCnpj))
        {
            if (!CnpjValidator.TryNormalize(request.Cnpj, out var normalizedCnpj))
            {
                return CustomerApprovalResult.InvalidCnpj("Informe um CNPJ válido com formato e dígitos verificadores corretos.");
            }

            var newCnpj = CnpjValidator.Format(normalizedCnpj);
            if (string.IsNullOrWhiteSpace(request.CnpjChangeReason))
            {
                return CustomerApprovalResult.InvalidReason("Informe o motivo da alteração do CNPJ.");
            }

            if (request.CnpjChangeReason.Trim().Length > 1000)
            {
                return CustomerApprovalResult.InvalidReason("O motivo da alteração deve ter até 1000 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(request.ChangedByUserId))
            {
                return CustomerApprovalResult.InvalidReason("Não foi possível identificar o administrador responsável pela alteração.");
            }

            var existingCnpjs = await db.Customers.AsNoTracking()
                .Where(x => x.Id != customer.Id)
                .Select(x => x.Cnpj)
                .ToListAsync(cancellationToken);
            if (existingCnpjs.Any(value => CnpjValidator.TryNormalize(value, out var other) && other == normalizedCnpj))
            {
                return CustomerApprovalResult.InvalidCnpj("Este CNPJ já está cadastrado em outro cliente.");
            }

            db.CustomerCnpjAudits.Add(new CustomerCnpjAudit
            {
                CustomerId = customer.Id,
                Action = CustomerCnpjAuditAction.CustomerUpdated,
                PreviousCnpj = customer.Cnpj,
                NewCnpj = newCnpj,
                Reason = request.CnpjChangeReason.Trim(),
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = request.ChangedByUserId,
                ChangedByEmail = string.IsNullOrWhiteSpace(request.ChangedByEmail) ? null : request.ChangedByEmail.Trim()
            });
            customer.Cnpj = newCnpj;
        }

        customer.Status = request.Status;
        customer.MinimumOrder = request.MinimumOrder;
        customer.CreditLimit = request.CreditLimit;
        customer.CreditOverrideEnabled = request.CreditOverrideEnabled;
        customer.MaximumPaymentTermDays = request.CreditOverrideEnabled ? Math.Clamp(request.MaximumPaymentTermDays ?? 0, 0, 14) : null;
        customer.CreditBlocked = request.CreditBlocked;
        customer.CreditNotes = string.IsNullOrWhiteSpace(request.CreditNotes) ? null : request.CreditNotes.Trim();
        if (request.CreditOverrideEnabled && customer.MaximumPaymentTermDays > 0 && customer.CreditReleaseDate is null)
        {
            customer.CreditReleaseDate = DateTime.UtcNow;
        }
        customer.WmcCode = NormalizeWmcCode(request.WmcCode);
        customer.PriceTableId = request.PriceTableId;
        customer.SalesRepresentativeId = request.SalesRepresentativeId;
        customer.InternalSalesUserId = request.InternalSalesUserId;
        customer.IsActive = request.Status != CustomerStatus.Inactive;
        customer.ApprovedAt = request.Status == CustomerStatus.Approved
            ? DateTime.UtcNow
            : null;

        db.CustomerPaymentTerms.RemoveRange(customer.CustomerPaymentTerms);
        customer.CustomerPaymentTerms.Clear();

        foreach (var paymentTermId in request.PaymentTermIds.Distinct())
        {
            customer.CustomerPaymentTerms.Add(new CustomerPaymentTerm
            {
                CustomerId = customer.Id,
                PaymentTermId = paymentTermId,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return CustomerApprovalResult.Success;
    }

    private static string? NormalizeWmcCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CustomerApprovalRequest
{
    public int CustomerId { get; set; }
    public string? Cnpj { get; set; }
    public string? CnpjChangeReason { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? ChangedByEmail { get; set; }
    public CustomerStatus Status { get; set; }
    public decimal MinimumOrder { get; set; }
    public decimal CreditLimit { get; set; }
    public bool CreditOverrideEnabled { get; set; }
    public int? MaximumPaymentTermDays { get; set; }
    public bool CreditBlocked { get; set; }
    public string? CreditNotes { get; set; }
    public string? WmcCode { get; set; }
    public int? PriceTableId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public string? InternalSalesUserId { get; set; }
    public IReadOnlyCollection<int> PaymentTermIds { get; set; } = [];
}

public sealed record CustomerApprovalResult(bool Succeeded, string? ErrorField = null, string? ErrorMessage = null)
{
    public static CustomerApprovalResult Success { get; } = new(true);
    public static CustomerApprovalResult InvalidCnpj(string message) => new(false, nameof(CustomerApprovalRequest.Cnpj), message);
    public static CustomerApprovalResult InvalidReason(string message) => new(false, nameof(CustomerApprovalRequest.CnpjChangeReason), message);
}
