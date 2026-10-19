using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Controllers;

public class HomeController(
    ApplicationDbContext db,
    SellerCatalogService? sellerCatalogService = null,
    CustomerAccessService? customerAccessService = null,
    PriceService? priceService = null,
    CartService? cartService = null,
    AdminCustomerContextService? adminCustomerContextService = null) : Controller
{
    public async Task<IActionResult> Index()
    {
        var publicProducts = db.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.IsAvailable && (product.WmcCode == null || (product.IsWmcActive && product.WmcStockAvailable && product.WmcInitialLoadReady)));

        var categories = await db.ProductCategories
            .AsNoTracking()
            .Where(category => category.IsActive && publicProducts.Any(product => product.ProductCategoryId == category.Id))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Take(8)
            .ToListAsync();

        var products = await publicProducts
            .Include(product => product.ProductCategory)
            .Include(product => product.Images)
            .OrderByDescending(product => product.Brand == "BIMBO" && product.Images.Any())
            .ThenByDescending(product => product.IsFeatured)
            .ThenBy(product => product.Name)
            .Take(4)
            .ToListAsync();

        return View(new PublicHomeViewModel
        {
            Categories = categories,
            Products = products
        });
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
    public async Task<IActionResult> Products(string? search, string? category, int? customerId = null)
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
                || x.Brand.ToLower().Contains(normalizedSearch)
                || (x.WmcCode != null && x.WmcCode.ToLower().Contains(normalizedSearch))
                || (x.Ean != null && x.Ean.ToLower().Contains(normalizedSearch))
                || (x.Ean13 != null && x.Ean13.ToLower().Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.ProductCategory != null && x.ProductCategory.Slug == category);
        }

        var products = await query.OrderBy(x => x.Name).ToListAsync();
        var productIds = products.Select(product => product.Id).ToArray();
        var inventory = await db.ProductInventories.AsNoTracking()
            .Where(item => productIds.Contains(item.ProductId))
            .ToDictionaryAsync(item => item.ProductId, item => item.AvailableQuantity);
        foreach (var product in products)
        {
            product.IsCommerciallyAvailable = product.IsActive && product.IsAvailable
                && (product.WmcCode is null || (product.IsWmcActive && product.WmcStockAvailable && product.WmcInitialLoadReady))
                && inventory.TryGetValue(product.Id, out var availableQuantity)
                && availableQuantity >= product.MinimumSalesQuantity;
        }

        ViewBag.AvailableQuantities = inventory;
        ViewBag.CustomerPurchaseAuthorized = false;
        ViewBag.CustomerId = customerId;
        ViewBag.Prices = new Dictionary<int, decimal>();
        ViewBag.CartQuantity = 0;
        ViewBag.CartProductQuantities = new Dictionary<int, int>();
        Orofoods.Web.Models.Customers.Customer? purchaseCustomer = null;
        if (User.Identity?.IsAuthenticated == true && customerAccessService is not null
            && User.IsInRole("Cliente") && !User.IsInRole("Vendedor"))
        {
            purchaseCustomer = await customerAccessService.GetApprovedCustomerAsync(
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        }
        else if (User.Identity?.IsAuthenticated == true && customerAccessService is not null
            && adminCustomerContextService is not null && User.IsInRole("Administrador"))
        {
            var selectedCustomerId = adminCustomerContextService.GetSelectedCustomerId(
                HttpContext.Session,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            if (selectedCustomerId is not null)
            {
                purchaseCustomer = await customerAccessService.GetApprovedCustomerByIdAsync(selectedCustomerId.Value);
            }
        }

        if (purchaseCustomer is not null)
        {
            ViewBag.CustomerPurchaseAuthorized = true;
            ViewBag.Prices = priceService is null
                ? new Dictionary<int, decimal>()
                : await priceService.GetPricesAsync(purchaseCustomer.Id, productIds);

            if (cartService is not null)
            {
                var cart = await cartService.GetAsync(purchaseCustomer.Id, HttpContext.Session);
                ViewBag.CartQuantity = cart.Items.Sum(item => item.Quantity);
                ViewBag.CartProductQuantities = cart.Items.ToDictionary(item => item.ProductId, item => item.Quantity);
            }
        }

        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Vendedor")
            && customerId is > 0 && sellerCatalogService is not null)
        {
            var sellerProducts = await sellerCatalogService.GetSelectableProductsByIdsAsync(User, customerId.Value, productIds);
            ViewBag.SellerProducts = sellerProducts?.ToDictionary(product => product.Id);
        }
        else
        {
            ViewBag.SellerProducts = new Dictionary<int, Orofoods.Web.Services.Sellers.SellerCatalogProductRow>();
        }

        return View(new PublicProductCatalogViewModel
        {
            Products = products,
            Categories = await db.ProductCategories.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(),
            Search = search,
            Category = category
        });
    }

    [HttpGet("/produtos/{id:int}")]
    public async Task<IActionResult> Product(int id, int? customerId = null)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (product is null)
        {
            return NotFound();
        }

        var inventory = await db.ProductInventories.AsNoTracking().SingleOrDefaultAsync(item => item.ProductId == product.Id);
        product.IsCommerciallyAvailable = product.IsActive && product.IsAvailable
            && (product.WmcCode is null || (product.IsWmcActive && product.WmcStockAvailable && product.WmcInitialLoadReady))
            && inventory is not null && inventory.AvailableQuantity >= product.MinimumSalesQuantity;

        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Cliente") && customerAccessService is not null)
        {
            var customer = await customerAccessService.GetApprovedCustomerAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            if (customer is not null)
            {
                ViewBag.CustomerPurchaseAuthorized = true;
                ViewBag.CustomerPrice = priceService is null ? null : await priceService.GetPriceAsync(customer.Id, product.Id);
            }
        }

        ViewBag.SellerCustomerId = customerId;
        if (customerId is > 0 && User.IsInRole("Vendedor") && sellerCatalogService is not null)
        {
            var selectedProducts = await sellerCatalogService.GetSelectableProductsByIdsAsync(User, customerId.Value, [id]);
            ViewBag.SellerProduct = selectedProducts?.SingleOrDefault();
        }

        return View(product);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var correlationId = HttpContext.Items[Infrastructure.CorrelationIdMiddleware.ItemName] as string;
        return View(new ErrorViewModel { RequestId = correlationId ?? Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
