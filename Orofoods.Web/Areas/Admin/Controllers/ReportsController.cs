using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Reports;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public class ReportsController(ApplicationDbContext db, ReportService reportService) : Controller
{
    public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo, int? customerId, int? productId, int? categoryId, int? salesRepresentativeId)
    {
        if (dateFrom is null && dateTo is null) { dateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); dateTo = DateTime.Today; }
        var filter = new ReportFilter(dateFrom, dateTo, customerId, productId, categoryId, salesRepresentativeId);
        ViewBag.Customers = new SelectList(await db.Customers.AsNoTracking().OrderBy(x => x.TradeName).ToListAsync(), "Id", "TradeName", customerId);
        ViewBag.Products = new SelectList(await db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(), "Id", "Name", productId);
        ViewBag.Categories = new SelectList(await db.ProductCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync(), "Id", "Name", categoryId);
        ViewBag.SalesRepresentatives = new SelectList(await db.SalesRepresentatives.AsNoTracking().OrderBy(x => x.Name).ToListAsync(), "Id", "Name", salesRepresentativeId);
        return View(new AdminReportViewModel { Filter = filter, Report = await reportService.BuildAsync(filter) });
    }
}
