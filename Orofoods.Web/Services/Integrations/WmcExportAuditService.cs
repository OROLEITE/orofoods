using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Orofoods.Web.Services.Integrations;

public sealed class WmcExportAuditService(ApplicationDbContext db)
{
    public async Task RecordAttemptAsync(WmcExportAudit attempt, CancellationToken cancellationToken = default)
    {
        if (attempt.AttemptId is null || attempt.AttemptId == Guid.Empty)
        {
            throw new ArgumentException("Uma chave de tentativa WMC é obrigatória.", nameof(attempt));
        }

        attempt.ExportedAt = DateTime.UtcNow;
        attempt.Error = SummarizeError(attempt.Error);
        attempt.Succeeded = attempt.Outcome is WmcExportOutcome.Generated or WmcExportOutcome.Available;

        var existing = await db.WmcExportAudits.SingleOrDefaultAsync(
            audit => audit.OrderId == attempt.OrderId && audit.AttemptId == attempt.AttemptId,
            cancellationToken);
        if (existing is null)
        {
            db.WmcExportAudits.Add(attempt);
        }
        else
        {
            existing.Source = attempt.Source;
            existing.Outcome = attempt.Outcome;
            existing.ExportedByUserId = attempt.ExportedByUserId;
            existing.ExportedByEmail = attempt.ExportedByEmail;
            existing.ExportedAt = attempt.ExportedAt;
            existing.GeneratedAt ??= attempt.GeneratedAt;
            existing.FileName = attempt.FileName;
            existing.Succeeded = attempt.Succeeded;
            existing.Error = attempt.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordAsync(
        int orderId,
        string? exportedByUserId,
        string? exportedByEmail,
        string? fileName,
        bool succeeded,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await RecordAttemptAsync(new WmcExportAudit
        {
            OrderId = orderId,
            AttemptId = Guid.NewGuid(),
            Source = WmcExportSource.Manual,
            Outcome = succeeded ? WmcExportOutcome.Generated : WmcExportOutcome.Failed,
            ExportedByUserId = exportedByUserId,
            ExportedByEmail = exportedByEmail,
            FileName = fileName,
            Succeeded = succeeded,
            Error = error,
            GeneratedAt = succeeded ? now : null
        }, cancellationToken);
    }

    private static string? SummarizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        var summary = Regex.Replace(error, @"\s+", " ").Trim();
        return summary.Length <= 500 ? summary : summary[..497] + "...";
    }
}
