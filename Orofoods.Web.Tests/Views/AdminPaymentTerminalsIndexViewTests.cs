namespace Orofoods.Web.Tests.Views;

public sealed class AdminPaymentTerminalsIndexViewTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Mercado_pago_terminal_action_is_staging_admin_and_state_gated_with_antiforgery()
    {
        var view = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Areas", "Admin", "Views", "PaymentTerminals", "Index.cshtml"));

        Assert.Contains("Terminais Mercado Pago", view, StringComparison.Ordinal);
        Assert.Contains("Model.IsStaging && User.IsInRole(\"Administrador\")", view, StringComparison.Ordinal);
        Assert.Contains("User.IsInRole(\"Administrador\")", view, StringComparison.Ordinal);
        Assert.Contains("MercadoPagoPointTerminalDiscovery.AuthorizedStagingTerminalId", view, StringComparison.Ordinal);
        Assert.Contains("terminal.OperatingMode == \"STANDALONE\"", view, StringComparison.Ordinal);
        Assert.Contains("terminal.OperatingMode == \"PDV\"", view, StringComparison.Ordinal);
        Assert.Contains("isAuthorizedTerminal && terminal.OperatingMode == \"STANDALONE\" && User.IsInRole(\"Administrador\")", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"SetMercadoPagoTerminalOperatingModeToPdv\"", view, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-antiforgery=\"true\"", view, StringComparison.Ordinal);
        Assert.Contains("Deseja alterar a maquininha NEWLAND_N950__N950NCD600484709 de STANDALONE para PDV?", view, StringComparison.Ordinal);
        Assert.Contains("Terminal em modo PDV", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Mercado_pago_terminal_view_does_not_render_credentials_or_call_external_api()
    {
        var view = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Areas", "Admin", "Views", "PaymentTerminals", "Index.cshtml"));

        Assert.DoesNotContain("AccessToken", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MercadoPagoPoint__AccessToken", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api.mercadopago.com", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/v1/orders", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Staging_one_real_point_test_is_fixed_admin_only_and_shows_only_safe_order_fields()
    {
        var view = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Areas", "Admin", "Views", "PaymentTerminals", "Index.cshtml"));

        Assert.Contains("PointStagingOneRealTestEnabled", view, StringComparison.Ordinal);
        Assert.Contains("StagingOneRealTestAttemptStarted", view, StringComparison.Ordinal);
        Assert.Contains("Enviar teste de R$ 1,00", view, StringComparison.Ordinal);
        Assert.Contains("StartStagingOneRealPointTest", view, StringComparison.Ordinal);
        Assert.Contains("RefreshStagingOneRealPointTestStatus", view, StringComparison.Ordinal);
        Assert.Contains("StagingOneRealTestOrderId", view, StringComparison.Ordinal);
        Assert.Contains("StagingOneRealTestStatus", view, StringComparison.Ordinal);
        Assert.Contains("StagingOneRealTestMessage", view, StringComparison.Ordinal);
        Assert.DoesNotContain("terminal_id", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Idempotency-Key", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"amount\"", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Staging_one_real_point_test_flag_defaults_to_false()
    {
        var appSettings = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "appsettings.json"));

        Assert.Contains("\"PointStagingOneRealTestEnabled\": false", appSettings, StringComparison.Ordinal);
    }
}
