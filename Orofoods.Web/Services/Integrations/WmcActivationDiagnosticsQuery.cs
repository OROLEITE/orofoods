using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Services.Integrations;

public sealed class WmcActivationDiagnosticsQuery(
    ApplicationDbContext db,
    IOptions<WmcJsonOrderOptions> wmcJsonOrderOptions,
    IOptions<WmcFileDropOptions> wmcFileDropOptions)
{
    public async Task<WmcActivationDiagnostics> ReadAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = await db.Orders
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                MaxOrderId = (int?)group.Max(order => order.Id),
                TotalOrderCount = group.LongCount()
            })
            .SingleOrDefaultAsync(cancellationToken);
        var jsonOptions = wmcJsonOrderOptions.Value;

        return new WmcActivationDiagnostics(
            aggregates?.MaxOrderId ?? 0,
            aggregates?.TotalOrderCount ?? 0,
            jsonOptions.AutomaticWorkerStartAfterOrderId,
            jsonOptions.Enabled,
            jsonOptions.AutomaticWorkerEnabled,
            wmcFileDropOptions.Value.Enabled);
    }
}
