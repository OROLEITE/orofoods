using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Threading.RateLimiting;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Authorization;
using Orofoods.Web.Data;
using AppDataProtectionOptions = Orofoods.Web.Models.Configuration.DataProtectionOptions;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Services.Reports;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Infrastructure;
using Orofoods.Web.Infrastructure.Logging;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var fileLoggingEnabled = builder.Configuration.GetValue<bool>("FileLogging:Enabled");
if (fileLoggingEnabled)
{
    var fileLogDirectory = builder.Configuration["FileLogging:Directory"] ?? "Logs";
    var resolvedFileLogDirectory = Path.IsPathFullyQualified(fileLogDirectory)
        ? fileLogDirectory
        : Path.Combine(builder.Environment.ContentRootPath, fileLogDirectory);
    var fileLogLevel = builder.Configuration.GetValue("FileLogging:MinimumLevel", LogLevel.Information);
    builder.Logging.AddDailyFile(resolvedFileLogDirectory, fileLogLevel);
}
var dataProtectionSettings = builder.Configuration.GetSection("DataProtection").Get<AppDataProtectionOptions>() ?? new AppDataProtectionOptions();
var applicationName = dataProtectionSettings.ApplicationName;
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(applicationName))
{
    throw new InvalidOperationException("DataProtection:ApplicationName deve estar configurado fora de Development.");
}

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName(string.IsNullOrWhiteSpace(applicationName) ? "Orofoods.Web" : applicationName);
if (builder.Environment.IsDevelopment())
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys")));
}
else
{
    var azureSettings = dataProtectionSettings.Azure;
    var azureEnabled = azureSettings is not null && azureSettings.Enabled;
    if (azureEnabled)
    {
        var blobUri = azureSettings!.BlobUri;
        var keyVaultKeyIdentifier = azureSettings.KeyVaultKeyIdentifier;
        if (string.IsNullOrWhiteSpace(blobUri) || string.IsNullOrWhiteSpace(keyVaultKeyIdentifier) || string.IsNullOrWhiteSpace(applicationName))
        {
            throw new InvalidOperationException("DataProtection:Azure habilitado exige ApplicationName, BlobUri e KeyVaultKeyIdentifier configurados para produção.");
        }

        var azureCredential = new DefaultAzureCredential();
        dataProtection
            .PersistKeysToAzureBlobStorage(new Uri(blobUri), azureCredential)
            .ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyIdentifier), azureCredential);
    }

    var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
    if (!string.IsNullOrWhiteSpace(keyDirectory) && Path.IsPathFullyQualified(keyDirectory))
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Configure um provedor de proteção de chaves compatível com o ambiente de produção.");
        }

        dataProtection
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
            .ProtectKeysWithDpapi();
    }
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<PostgreSqlApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<ApplicationDbContext>(provider => provider.GetRequiredService<PostgreSqlApplicationDbContext>());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<PostgreSqlApplicationDbContext>()
    .AddSignInManager<ActiveUserSignInManager>();
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Defina Jwt:Key por variável de ambiente ou User Secrets.");
builder.Services.AddAuthentication().AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"], ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ValidateLifetime = true };
});
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/CustomerRegistration/Pending";
});

builder.Services.AddScoped<CustomerAccessService>();
builder.Services.AddScoped<SalesRepresentativeAccessService>();
builder.Services.AddScoped<AdminCustomerContextService>();
builder.Services.AddScoped<CustomerApprovalService>();
builder.Services.AddScoped<CustomerRegistrationService>();
builder.Services.Configure<PaymentEligibilityOptions>(builder.Configuration.GetSection(PaymentEligibilityOptions.SectionName));
builder.Services.AddScoped<IPaymentEligibilityService, PaymentEligibilityService>();
builder.Services.AddScoped<PriceService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<FrequentProductService>();
builder.Services.AddScoped<CustomerDashboardService>();
builder.Services.AddScoped<SavedOrderService>();
builder.Services.AddScoped<AssistedOrderService>();
builder.Services.AddScoped<AdminCatalogService>();
builder.Services.AddScoped<AdminOrderService>();
builder.Services.AddScoped<AdminCommercialService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ApiTokenService>();
builder.Services.AddScoped<OrderIntegrationService>();
builder.Services.AddScoped<OrderReservationService>();
builder.Services.Configure<CrmOptions>(builder.Configuration.GetSection(CrmOptions.SectionName));
builder.Services.AddScoped<CommercialAttentionService>();
builder.Services.AddScoped<CrmOpportunityService>();
builder.Services.AddScoped<UserNotificationService>();
builder.Services.AddScoped<WhatsAppConversationService>();
builder.Services.Configure<WhatsAppBusinessOptions>(builder.Configuration.GetSection(WhatsAppBusinessOptions.SectionName));
builder.Services.AddHttpClient<IWhatsAppBusinessGateway, MetaWhatsAppBusinessGateway>((sp, client) =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/");
    client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<WhatsAppBusinessOptions>>().Value.RequestTimeoutSeconds);
});
builder.Services.AddScoped<WmcExportAuditService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddSingleton<IBoletoProvider, PendingBoletoProvider>();
builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>((sp, client) =>
{
    client.BaseAddress = sp.GetRequiredService<IOptions<MercadoPagoOptions>>().Value.BaseAddress;
});
builder.Services.AddScoped<IMercadoPagoWebhookSignatureValidator, MercadoPagoWebhookSignatureValidator>();
builder.Services.AddScoped<IPaymentApprovalHandler, NoOpPaymentApprovalHandler>();
builder.Services.AddScoped<PaymentOrchestrationService>();
builder.Services.AddSingleton<WmcOrderFileGenerator>();
builder.Services.Configure<WmcFileDropOptions>(builder.Configuration.GetSection(WmcFileDropOptions.SectionName));
builder.Services.AddScoped<IErpOrderIntegration, WmcFileDropErpOrderIntegration>();
builder.Services.AddHostedService<ErpRetryBackgroundService>();
builder.Services.Configure<WmcFirebirdOptions>(builder.Configuration.GetSection(WmcFirebirdOptions.SectionName));
builder.Services.Configure<WmcSyncOptions>(builder.Configuration.GetSection(WmcSyncOptions.SectionName));
builder.Services.AddScoped<IWmcConnectionFactory, WmcFirebirdConnectionFactory>();
builder.Services.AddScoped<IWmcFirebirdReader, WmcFirebirdReader>();
builder.Services.AddScoped<IWmcSchemaInspector, WmcSchemaInspector>();
builder.Services.AddScoped<IWmcCustomerReader, WmcCustomerReader>();
builder.Services.AddScoped<IWmcProductReader, WmcProductReader>();
builder.Services.AddScoped<IWmcSellerReader, UndiscoveredWmcSellerReader>();
builder.Services.AddScoped<WmcSyncService>();
builder.Services.AddSingleton<WmcSyncCoordinator>();
builder.Services.AddHostedService<WmcSyncWorker>();
builder.Services.AddHealthChecks().AddCheck<WmcFirebirdHealthCheck>("wmc-firebird");
builder.Services.AddScoped<IAuthorizationHandler, ApprovedCustomerHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        OrofoodsPolicies.ApprovedCustomer,
        policy => policy.RequireAuthenticatedUser().AddRequirements(new ApprovedCustomerRequirement()));
});
builder.Services.AddControllersWithViews();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var brazilianPortuguese = new CultureInfo("pt-BR");
    options.DefaultRequestCulture = new RequestCulture(brazilianPortuguese);
    options.SupportedCultures = [brazilianPortuguese];
    options.SupportedUICultures = [brazilianPortuguese];
});
builder.Services.AddOpenApi();
builder.Services.AddSession();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRequestLocalization();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<AuthenticatedResponseCacheMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapRazorPages()
    .WithStaticAssets();

app.MapGet("/health", async (
    HttpResponse response,
    ApplicationDbContext db,
    CancellationToken cancellationToken) =>
{
    response.Headers.CacheControl = "no-store";

    try
    {
        var databaseOnline = await db.Database.CanConnectAsync(cancellationToken);
        return databaseOnline
            ? Results.Ok(new { status = "healthy" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapHealthChecks("/health/wmc").RequireAuthorization(policy => policy.RequireRole("Administrador"));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (app.Environment.IsDevelopment())
    {
        db.Database.EnsureCreated();
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }
    else
    {
        await db.Database.MigrateAsync();
    }
}

app.Run();
