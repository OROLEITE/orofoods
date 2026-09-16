using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var products = await db.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.IsAvailable)
            .Include(product => product.ProductCategory)
            .Include(product => product.Images)
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.Name)
            .Take(8)
            .ToListAsync();

        return View(products);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("/sobre")]
    public IActionResult About() => View();

    [HttpGet("/como-funciona")]
    public IActionResult HowItWorks() => View();

    [HttpGet("/termos")]
    public IActionResult Terms() => View();

    [HttpGet("/contato")]
    public IActionResult Contact() => View(new ContactViewModel());

    [HttpPost("/contato")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactViewModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        db.ContactMessages.Add(new ContactMessage
        {
            Name = input.Name,
            Company = input.Company,
            Phone = input.Phone,
            WhatsApp = input.WhatsApp,
            Email = input.Email,
            City = input.City,
            Message = input.Message,
            PrivacyConsentAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        TempData["ContactSuccess"] = "Recebemos sua mensagem. Nossa equipe retornará em breve.";
        return RedirectToAction(nameof(Contact));
    }

    [HttpGet("/produtos")]
    public async Task<IActionResult> Products(string? search, string? category)
    {
        IQueryable<Product> query = db.Products
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.ProductCategory)
            .Include(x => x.Images);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(normalizedSearch)
                || x.Sku.ToLower().Contains(normalizedSearch)
                || x.Brand.ToLower().Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.ProductCategory != null && x.ProductCategory.Slug == category);
        }

        return View(new PublicProductCatalogViewModel
        {
            Products = await query.OrderBy(x => x.Name).ToListAsync(),
            Categories = await db.ProductCategories.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(),
            Search = search,
            Category = category
        });
    }

    [HttpGet("/produtos/{id:int}")]
    public async Task<IActionResult> Product(int id)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive);

        return product is null ? NotFound() : View(product);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var correlationId = HttpContext.Items[Infrastructure.CorrelationIdMiddleware.ItemName] as string;
        return View(new ErrorViewModel { RequestId = correlationId ?? Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
