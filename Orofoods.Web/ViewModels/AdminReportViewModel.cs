using Orofoods.Web.Services.Reports;

namespace Orofoods.Web.ViewModels;

public sealed class AdminReportViewModel
{
    public required ReportFilter Filter { get; init; }
    public required SalesReport Report { get; init; }
}
