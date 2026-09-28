namespace Orofoods.Web.Tests.Configuration;

public class MercadoPagoConfigurationTests
{
    private static readonly string ProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Program_registers_gateway_validator_and_orchestration_with_official_base_address()
    {
        var program = File.ReadAllText(Path.Combine(ProjectPath, "Program.cs"));

        Assert.Contains("Configure<MercadoPagoOptions>(builder.Configuration.GetSection(MercadoPagoOptions.SectionName))", program);
        Assert.Contains("AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>", program);
        Assert.Contains("AddScoped<IMercadoPagoWebhookSignatureValidator, MercadoPagoWebhookSignatureValidator>", program);
        Assert.Contains("AddScoped<IPaymentApprovalHandler, NoOpPaymentApprovalHandler>", program);
        Assert.Contains("AddScoped<PaymentOrchestrationService>", program);
    }

    [Fact]
    public void Default_options_use_the_official_mercado_pago_orders_api_base_address()
    {
        var options = new Orofoods.Web.Services.Payments.MercadoPagoOptions();

        Assert.Equal(new Uri("https://api.mercadopago.com/"), options.BaseAddress);
    }

    [Fact]
    public void Tracked_settings_files_do_not_contain_mercado_pago_secret_values()
    {
        var settingsFiles = new[] { "appsettings.json", "appsettings.Development.json", "appsettings.Development.example.json" }
            .Select(name => Path.Combine(ProjectPath, name))
            .Where(File.Exists);

        foreach (var file in settingsFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("AccessToken", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WebhookSecret", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PublicKey", content, StringComparison.OrdinalIgnoreCase);
        }
    }
}
