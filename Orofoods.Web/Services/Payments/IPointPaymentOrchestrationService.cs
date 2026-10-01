using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.Services.Payments;

public interface IPointPaymentOrchestrationService
{
    Task<PointPaymentOperationResult> StartChargeAsync(int orderId, int assignmentId, string requestKey, CancellationToken cancellationToken = default, string? adminUserId = null);
    Task<PointPaymentOperationResult> RefreshAsync(int paymentId, CancellationToken cancellationToken = default, string? adminUserId = null);
    Task<PointPaymentOperationResult> CancelAsync(int paymentId, CancellationToken cancellationToken = default, string? adminUserId = null);
    Task<IReadOnlyList<DriverPaymentTerminalAssignment>> GetEligibleAssignmentsAsync(CancellationToken cancellationToken = default);
}

public sealed record PointPaymentOperationResult(bool Succeeded, Payment? Payment, string? ErrorCode, string? ErrorMessage)
{
    public static PointPaymentOperationResult Success(Payment payment) => new(true, payment, null, null);
    public static PointPaymentOperationResult Failure(string code, string message, Payment? payment = null) => new(false, payment, code, message);
}
