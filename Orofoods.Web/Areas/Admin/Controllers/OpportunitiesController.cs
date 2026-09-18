using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador,Vendedor,GerenteComercial")]
public class OpportunitiesController(CrmOpportunityService service) : Controller
{
    public async Task<IActionResult> Index(CrmOpportunityStage? stage, int? customerId) => View(await service.ListAsync(User, customerId, stage));

    [HttpGet]
    public IActionResult Create(int customerId, CrmOpportunitySource source = CrmOpportunitySource.Manual) => View(new CrmOpportunityFormViewModel { CustomerId = customerId, Source = source, Type = source == CrmOpportunitySource.RepurchaseAlert ? CrmOpportunityType.Repurchase : CrmOpportunityType.Other });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CrmOpportunityFormViewModel input)
    {
        if (!ModelState.IsValid) return View(input);
        try
        {
            await service.CreateAsync(User, input.CustomerId, input.Title, input.Type, input.Source, input.EstimatedValue, input.ExpectedCloseAt, input.Notes, input.AssignedUserId);
            return RedirectToAction("Details", "Customers", new { area = "Admin", id = input.CustomerId });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStage(int id, CrmOpportunityStage stage, string? lostReason, string? outcome)
    {
        try { await service.ChangeStageAsync(User, id, stage, lostReason, outcome); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { TempData["OpportunityError"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}

public sealed class CrmOpportunityFormViewModel
{
    public int CustomerId { get; set; }
    public string Title { get; set; } = "";
    public CrmOpportunityType Type { get; set; }
    public CrmOpportunitySource Source { get; set; }
    public decimal? EstimatedValue { get; set; }
    public DateTime? ExpectedCloseAt { get; set; }
    public string? Notes { get; set; }
    public string? AssignedUserId { get; set; }
}
