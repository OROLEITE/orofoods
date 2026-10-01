using System.Data;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.Services.Payments;

public sealed class DriverPaymentTerminalService(ApplicationDbContext db, TimeProvider timeProvider) : IDriverPaymentTerminalService
{
    private const string AssignmentConflictMessage = "Este terminal foi associado por outra operação. Atualize a página e tente novamente.";
    private const string TerminalConflictMessage = "Não foi possível salvar o terminal devido a um conflito com outro cadastro.";

    public Task<AssignmentResult> CreateTerminalAsync(
        PaymentTerminalProvider provider,
        string? deviceId,
        string? storeId,
        string? posId,
        bool isActive,
        CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var terminal = new PaymentTerminal
            {
                Provider = provider,
                DeviceId = deviceId,
                StoreId = storeId,
                PosId = posId,
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.PaymentTerminals.Add(terminal);
            await db.SaveChangesAsync(cancellationToken);
            return AssignmentResult.Success();
        }, TerminalConflictMessage, cancellationToken);

    public Task<AssignmentResult> UpdateTerminalAsync(
        int terminalId,
        PaymentTerminalProvider provider,
        string? deviceId,
        string? storeId,
        string? posId,
        bool isActive,
        CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var terminal = await db.PaymentTerminals.SingleOrDefaultAsync(x => x.Id == terminalId, cancellationToken);
            if (terminal is null) return AssignmentResult.Failure("Terminal não encontrado.");

            var duplicate = deviceId is not null && await db.PaymentTerminals.AnyAsync(
                x => x.Id != terminalId && x.Provider == provider && x.DeviceId == deviceId,
                cancellationToken);
            if (duplicate) return AssignmentResult.Failure("Este Device ID já está cadastrado.");

            var hasHistory = await db.DriverPaymentTerminalAssignments.AnyAsync(
                x => x.PaymentTerminalId == terminalId,
                cancellationToken);
            if (hasHistory && (terminal.Provider != provider || terminal.DeviceId != deviceId || terminal.StoreId != storeId || terminal.PosId != posId))
            {
                return AssignmentResult.Failure("Os identificadores de um terminal com histórico não podem ser alterados. Cadastre outro terminal para um novo dispositivo.");
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            terminal.Provider = provider;
            terminal.DeviceId = deviceId;
            terminal.StoreId = storeId;
            terminal.PosId = posId;
            terminal.IsActive = isActive;
            terminal.UpdatedAt = now;

            if (!isActive)
            {
                var assignments = await db.DriverPaymentTerminalAssignments
                    .Where(x => x.PaymentTerminalId == terminalId && x.EndedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var assignment in assignments) assignment.EndedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
            return AssignmentResult.Success();
        }, TerminalConflictMessage, cancellationToken);

    public Task<AssignmentResult> AssignAsync(int driverId, int terminalId, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == driverId, cancellationToken);
            if (driver is null) return AssignmentResult.Failure("Motorista não encontrado.");
            if (!driver.IsActive) return AssignmentResult.Failure("Motorista inativo não pode receber uma maquininha.");

            var terminal = await db.PaymentTerminals.SingleOrDefaultAsync(x => x.Id == terminalId, cancellationToken);
            if (terminal is null) return AssignmentResult.Failure("Terminal não encontrado.");
            if (!terminal.IsActive) return AssignmentResult.Failure("Terminal inativo não pode ser associado.");

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var assignment = new DriverPaymentTerminalAssignment
            {
                DriverId = driver.Id,
                Driver = driver,
                PaymentTerminalId = terminal.Id,
                PaymentTerminal = terminal,
                StartedAt = now,
                CreatedAt = now
            };
            db.DriverPaymentTerminalAssignments.Add(assignment);
            await db.SaveChangesAsync(cancellationToken);
            return AssignmentResult.Success(assignment);
        }, AssignmentConflictMessage, cancellationToken);

    public Task<AssignmentResult> EndAsync(int assignmentId, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var assignment = await db.DriverPaymentTerminalAssignments
                .SingleOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);
            if (assignment is null) return AssignmentResult.Failure("Associação não encontrada.");
            if (assignment.EndedAt is not null) return AssignmentResult.Failure("A associação já foi encerrada.");

            assignment.EndedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            return AssignmentResult.Success(assignment);
        }, AssignmentConflictMessage, cancellationToken);

    public Task<AssignmentResult> DeactivateDriverAsync(int driverId, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == driverId, cancellationToken);
            if (driver is null) return AssignmentResult.Failure("Motorista não encontrado.");

            var now = timeProvider.GetUtcNow().UtcDateTime;
            driver.IsActive = false;
            driver.UpdatedAt = now;
            var assignments = await db.DriverPaymentTerminalAssignments
                .Where(x => x.DriverId == driverId && x.EndedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var assignment in assignments) assignment.EndedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return AssignmentResult.Success();
        }, AssignmentConflictMessage, cancellationToken);

    public Task<AssignmentResult> DeactivateTerminalAsync(int terminalId, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var terminal = await db.PaymentTerminals.SingleOrDefaultAsync(x => x.Id == terminalId, cancellationToken);
            if (terminal is null) return AssignmentResult.Failure("Terminal não encontrado.");

            var now = timeProvider.GetUtcNow().UtcDateTime;
            terminal.IsActive = false;
            terminal.UpdatedAt = now;
            var assignments = await db.DriverPaymentTerminalAssignments
                .Where(x => x.PaymentTerminalId == terminalId && x.EndedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var assignment in assignments) assignment.EndedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return AssignmentResult.Success();
        }, AssignmentConflictMessage, cancellationToken);

    public async Task<IReadOnlyList<DriverPaymentTerminalAssignment>> GetHistoryAsync(int? driverId = null, int? terminalId = null, CancellationToken cancellationToken = default)
    {
        var query = db.DriverPaymentTerminalAssignments.AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.PaymentTerminal)
            .AsQueryable();
        if (driverId is not null) query = query.Where(x => x.DriverId == driverId.Value);
        if (terminalId is not null) query = query.Where(x => x.PaymentTerminalId == terminalId.Value);
        return await query.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
    }

    private async Task<AssignmentResult> InSerializableTransactionAsync(
        Func<Task<AssignmentResult>> operation,
        string conflictMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var result = await operation();
            if (!result.Succeeded) return result;
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (DatabaseWriteConflict.IsExpected(exception))
        {
            return AssignmentResult.Failure(conflictMessage);
        }
    }
}
