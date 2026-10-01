using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.Services.Payments;

public interface IDriverPaymentTerminalService
{
    Task<AssignmentResult> CreateTerminalAsync(PaymentTerminalProvider provider, string? deviceId, string? storeId, string? posId, bool isActive, CancellationToken cancellationToken = default);
    Task<AssignmentResult> UpdateTerminalAsync(int terminalId, PaymentTerminalProvider provider, string? deviceId, string? storeId, string? posId, bool isActive, CancellationToken cancellationToken = default);
    Task<AssignmentResult> AssignAsync(int driverId, int terminalId, CancellationToken cancellationToken = default);
    Task<AssignmentResult> EndAsync(int assignmentId, CancellationToken cancellationToken = default);
    Task<AssignmentResult> DeactivateDriverAsync(int driverId, CancellationToken cancellationToken = default);
    Task<AssignmentResult> DeactivateTerminalAsync(int terminalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverPaymentTerminalAssignment>> GetHistoryAsync(int? driverId = null, int? terminalId = null, CancellationToken cancellationToken = default);
}

public sealed record AssignmentResult(bool Succeeded, DriverPaymentTerminalAssignment? Assignment, string? ErrorMessage)
{
    public static AssignmentResult Success(DriverPaymentTerminalAssignment? assignment = null) => new(true, assignment, null);
    public static AssignmentResult Failure(string errorMessage) => new(false, null, errorMessage);
}
