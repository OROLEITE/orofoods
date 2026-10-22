using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class WmcBimboImportControllerTests
{
    [Fact]
    public async Task Non_staging_environment_is_forbidden_before_reading_wmc()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var reader = new CountingReader([]);
        var controller = CreateController(db, reader, "Production");

        var result = await controller.WmcBimboImport(["503838"], CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, reader.BrandReadCount);
        Assert.Empty(await db.WmcProductImportRuns.ToListAsync());
    }

    [Fact]
    public async Task Staging_action_reads_only_brand_104_and_returns_a_safe_summary()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var reader = new CountingReader([]);
        var controller = CreateController(db, reader, Environments.Staging);

        var result = await controller.WmcBimboImport(["503838"], CancellationToken.None);

        var response = Assert.IsType<WmcBimboImportResponse>(Assert.IsType<ConflictObjectResult>(result).Value);
        Assert.False(response.Succeeded);
        Assert.Equal("APPROVED_PRODUCT_MISSING", response.ErrorCode);
        Assert.Equal(1, reader.BrandReadCount);
        Assert.Equal((short)104, reader.LastBrandCode);
        Assert.Equal("Rejected", (await db.WmcProductImportRuns.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task Simulation_is_read_only_and_reads_only_brand_104_in_staging()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var reader = new CountingReader([]);
        var controller = CreateController(db, reader, Environments.Staging);

        var result = Assert.IsType<OkObjectResult>(await controller.WmcBimboSimulate(CancellationToken.None));
        var response = Assert.IsType<WmcBimboSimulationResponse>(result.Value);

        Assert.False(response.Succeeded);
        Assert.Equal(10, response.Products.Count);
        Assert.Equal(1, reader.BrandReadCount);
        Assert.Equal((short)104, reader.LastBrandCode);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductInventories.ToListAsync());
        Assert.Empty(await db.WmcProductImportRuns.ToListAsync());
    }

    [Fact]
    public async Task Simulation_is_forbidden_outside_staging_before_reading_wmc()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var reader = new CountingReader([]);
        var controller = CreateController(db, reader, "Production");

        Assert.IsType<ForbidResult>(await controller.WmcBimboSimulate(CancellationToken.None));
        Assert.Equal(0, reader.BrandReadCount);
    }

    [Fact]
    public void Bimbo_import_action_is_posted_and_protected_by_antiforgery_and_admin_role()
    {
        var action = typeof(IntegrationsController).GetMethod("WmcBimboImport");

        Assert.NotNull(action);
        Assert.NotNull(action.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        var authorize = Assert.Single(typeof(IntegrationsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>());
        Assert.Equal("Administrador", authorize.Roles);
    }

    [Fact]
    public void Bimbo_simulate_and_activate_actions_are_posted_and_antiforgery_protected()
    {
        foreach (var name in new[] { "WmcBimboSimulate", "WmcBimboActivate" })
        {
            var action = typeof(IntegrationsController).GetMethod(name);
            Assert.NotNull(action);
            Assert.NotNull(action.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).SingleOrDefault());
            Assert.NotNull(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        }
    }

    private static IntegrationsController CreateController(ApplicationDbContext db, CountingReader reader, string environmentName)
    {
        var service = new WmcBimboStagingImportService(db, reader, NullLogger<WmcBimboStagingImportService>.Instance, TimeProvider.System);
        var controller = new IntegrationsController(
            db,
            null!,
            null!,
            null!,
            Options.Create(new WmcSyncOptions()),
            null!,
            null!,
            NullLogger<IntegrationsController>.Instance,
            new TestHostEnvironment(environmentName),
            service,
            new WmcInitialLoadReadinessReleaseService(db, new TestHostEnvironment(environmentName), Options.Create(new WmcInitialLoadReleaseOptions { Enabled = true }), TimeProvider.System),
            Options.Create(new WmcInitialLoadReleaseOptions { Enabled = true }));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-1")], "test"))
            }
        };
        return controller;
    }

    private sealed class CountingReader(IReadOnlyList<WmcProductRecord> rows) : IWmcProductReader
    {
        public int BrandReadCount { get; private set; }
        public short? LastBrandCode { get; private set; }

        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);

        public Task<IReadOnlyList<WmcProductRecord>> GetAllByBrandAsync(short? brandCode, CancellationToken cancellationToken = default)
        {
            BrandReadCount++;
            LastBrandCode = brandCode;
            return Task.FromResult(rows);
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
