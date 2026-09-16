using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Services.Integrations;

public sealed class WmcExportAuditService(ApplicationDbContext db)
{
    public async Task RecordAsync(
        int orderId,
        string? exportedByUserId,
        string? exportedByEmail,
        string? fileName,
        bool succeeded,
        string? error,
        CancellationToken cancellationToken = default)
    {
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = orderId,
            ExportedByUserId = exportedByUserId,
            ExportedByEmail = exportedByEmail,
            FileName = fileName,
            Succeeded = succeeded,
            Error = error
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
