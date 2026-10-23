using System.Security.Claims;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Validation;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Storage;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminProductsRenderedPostTests
{
    [Fact]
    public async Task New_product_get_renders_the_shared_product_form()
    {
        await using var harness = await ProductEditHarness.CreateAsync();
        await harness.SeedProductAsync();

        using var response = await harness.Client.GetAsync("/Admin/Products/Edit");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Novo produto", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Sku\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"BasePrice\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Ean13\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Dun14\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"CommercialUnitCode\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"LogisticsUnitCode\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"PackagesPerCase\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_post_binds_false_includeInactive_and_persists_inactive_product()
    {
        await using var harness = await ProductEditHarness.CreateAsync();
        var (productId, categoryId) = await harness.SeedProductAsync();

        using var response = await harness.Client.GetAsync($"/Admin/Products/Edit?id={productId}&q=BIMBO&includeInactive=false&page=2");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var editForm = Regex.Match(html, "<form method=\"post\" class=\"admin-form-card[\\s\\S]*?</form>", RegexOptions.IgnoreCase).Value;
        Assert.NotEmpty(editForm);
        foreach (var hiddenField in new[] { "Description", "PromotionalPrice", "IsPromotional", "IsFeatured", "Accent" })
            Assert.DoesNotContain($"name=\"{hiddenField}\"", editForm, StringComparison.Ordinal);
        var inputValues = Regex.Matches(editForm, "<input[^>]*>", RegexOptions.IgnoreCase)
            .Select(match => match.Value)
            .Where(tag => Regex.IsMatch(tag, "\\bname=\"[^\"]+\"", RegexOptions.IgnoreCase))
            .Select(tag => (Name: Attribute(tag, "name"), Value: Attribute(tag, "value")))
            .Where(field => field.Name is not "__RequestVerificationToken" and not "__Invariant")
            .ToArray();
        Assert.DoesNotContain(inputValues, field => field.Value == "value");
        Assert.Contains("name=\"ProductCategoryId\"", editForm, StringComparison.Ordinal);
        var categorySelect = Regex.Match(editForm, "<select[^>]*name=\"ProductCategoryId\"[\\s\\S]*?</select>", RegexOptions.IgnoreCase).Value;
        Assert.Contains("<option selected=\"selected\" value=\"1\">", categorySelect, StringComparison.Ordinal);
        var includeInactiveInput = Regex.Match(editForm, "<input[^>]*name=\"includeInactive\"[^>]*>", RegexOptions.IgnoreCase).Value;

        var token = Attribute(Regex.Match(editForm, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase).Value, "value");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Id", productId.ToString()),
            new("q", "BIMBO"),
            new("includeInactive", Attribute(includeInactiveInput, "value")),
            new("page", "2"),
            new("Sku", "WMC-504342"),
            new("WmcCode", "504342"),
            new("Brand", "BIMBO"),
            new("Name", "BIMBO produto de teste"),
            new("ProductCategoryId", categoryId.ToString()),
            new("BasePrice", "1"),
            new("UnitsPerCase", "0"),
            new("MinimumCases", "1"),
            new("Ean13", ""),
            new("Dun14", ""),
            new("CommercialUnitCode", ""),
            new("LogisticsUnitCode", ""),
            new("PackagesPerCase", ""),
            new("SubstituteProductId", ""),
            new("IsActive", "false"),
            new("IsAvailable", "false"),
            new("__Invariant", "UnitsPerCase"),
            new("__Invariant", "MinimumCases"),
            new("__Invariant", "PackagesPerCase")
        };
        using var post = new HttpRequestMessage(HttpMethod.Post, $"/Admin/Products/Edit?id={productId}")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            post.Headers.Add("Cookie", string.Join("; ", cookies.Select(cookie => cookie.Split(';')[0])));
        using var posted = await harness.Client.SendAsync(post);
        var postBody = await posted.Content.ReadAsStringAsync();
        Assert.True(posted.StatusCode == HttpStatusCode.Redirect, $"POST returned {(int)posted.StatusCode}. ModelState: {string.Join(" | ", harness.Capture.ModelStateErrors)}; Form: {string.Join(" | ", harness.Capture.FormValues)}; Body: {postBody[..Math.Min(500, postBody.Length)]}");
        Assert.Empty(harness.Capture.ModelStateErrors);
        Assert.Equal("false", Attribute(includeInactiveInput, "value"));
        Assert.Equal("BIMBO", harness.Capture.FormValues.Single(value => value.StartsWith("q=", StringComparison.Ordinal))[2..]);
        Assert.Equal("false", harness.Capture.FormValues.Single(value => value.StartsWith("includeInactive=", StringComparison.Ordinal))["includeInactive=".Length..]);
        Assert.Equal("2", harness.Capture.FormValues.Single(value => value.StartsWith("page=", StringComparison.Ordinal))[5..]);
        var redirect = posted.Headers.Location!.ToString();
        Assert.Contains("q=BIMBO", redirect, StringComparison.Ordinal);
        Assert.Contains("includeInactive=False", redirect, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("page=2", redirect, StringComparison.Ordinal);
        var saved = await harness.ReloadProductAsync(productId);
        Assert.False(saved.IsActive);
        Assert.Equal(1m, saved.BasePrice);
        Assert.Equal("Descrição comercial preservada", saved.Description);
        Assert.Equal(0.625m, saved.Weight);
        Assert.Equal("PCT", saved.Unit);
        Assert.Equal(6, saved.UnitsPerPackage);
        Assert.Equal(0.75m, saved.PromotionalPrice);
        Assert.True(saved.IsFeatured);
        Assert.True(saved.IsPromotional);
        Assert.Equal("#123456", saved.Accent);
    }

    private static string Attribute(string tag, string attribute)
    {
        var match = Regex.Match(tag, $"\\b{Regex.Escape(attribute)}=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : string.Empty;
    }

    private sealed class ProductEditHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IHost _host;

        public HttpClient Client { get; }
        public BindingCapture Capture { get; }

        private ProductEditHarness(SqliteConnection connection, IHost host, BindingCapture capture)
        {
            _connection = connection;
            _host = host;
            Capture = capture;
            Client = host.GetTestClient();
        }

        public static async Task<ProductEditHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var capture = new BindingCapture();
            var host = await new HostBuilder()
                .UseEnvironment("Test")
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddDistributedMemoryCache();
                        services.AddSession();
                        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
                        services.AddAuthentication("AdminTest")
                            .AddScheme<AuthenticationSchemeOptions, AdminTestAuthenticationHandler>("AdminTest", _ => { });
                        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                        services.AddAuthorization();
                        services.AddControllersWithViews(options => AdminModelBindingMessages.Configure(options))
                            .AddApplicationPart(typeof(ProductsController).Assembly);
                        services.AddSingleton(capture);
                        services.Configure<MvcOptions>(options => options.Filters.Add(new BindingCaptureFilter(capture)));
                        services.AddScoped<AdminCatalogService>();
                        services.AddScoped<CustomerAccessService>();
                        services.AddScoped<AdminCustomerContextService>();
                        services.AddScoped<PriceService>();
                        services.AddScoped<CartService>();
                        services.AddScoped<UserNotificationService>();
                        services.AddSingleton(Mock.Of<IProductImageStorage>());
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseSession();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapControllerRoute(
                            name: "areas",
                            pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"));
                    }))
                .StartAsync();

            await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options))
                await db.Database.EnsureCreatedAsync();

            return new ProductEditHarness(connection, host, capture);
        }

        public async Task<(int ProductId, int CategoryId)> SeedProductAsync()
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
            var category = new ProductCategory { Name = "Categoria teste", Slug = "categoria-teste", IsActive = true };
            var product = new Product
            {
                Sku = "WMC-504342",
                WmcCode = "504342",
                Name = "BIMBO produto de teste",
                Brand = "BIMBO",
                ProductCategory = category,
                Unit = "PCT",
                BasePrice = 1.00m,
                Description = "Descrição comercial preservada",
                Weight = 0.625m,
                UnitsPerPackage = 6,
                PromotionalPrice = 0.75m,
                IsFeatured = true,
                IsPromotional = true,
                Accent = "#123456",
                IsActive = false,
                IsAvailable = false
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return (product.Id, category.Id);
        }

        public async Task<Product> ReloadProductAsync(int id)
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
            return await db.Products.AsNoTracking().SingleAsync(product => product.Id == id);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _host.StopAsync();
            _host.Dispose();
            await _connection.DisposeAsync();
        }
    }

    private sealed class BindingCapture
    {
        public IReadOnlyList<string> FormValues { get; set; } = [];
        public IReadOnlyList<string> ModelStateErrors { get; set; } = [];
    }

    private sealed class BindingCaptureFilter(BindingCapture capture) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (HttpMethods.IsPost(context.HttpContext.Request.Method)
                && context.RouteData.Values.GetValueOrDefault("controller")?.ToString() == "Products"
                && context.RouteData.Values.GetValueOrDefault("action")?.ToString() == "Edit")
            {
                var form = await context.HttpContext.Request.ReadFormAsync();
                capture.FormValues = form.SelectMany(pair => pair.Value.Select(value => $"{pair.Key}={value}")).ToArray();
                capture.ModelStateErrors = context.ModelState
                    .Where(pair => pair.Value?.Errors.Count > 0)
                    .SelectMany(pair => pair.Value!.Errors.Select(error => $"{pair.Key}: {error.ErrorMessage}"))
                    .ToArray();
            }
            await next();
        }
    }

    private sealed class AdminTestAuthenticationHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-test"),
                new Claim(ClaimTypes.Name, "admin-test"),
                new Claim(ClaimTypes.Role, "Administrador")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
