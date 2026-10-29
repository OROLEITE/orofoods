using Orofoods.Web.Services.Integrations;

namespace Orofoods.Web.Tests.Services;

public class WmcAuditDisplayFormatterTests
{
    [Fact]
    public void Sanitize_redacts_credentials_and_customer_fields_from_json_but_keeps_diagnostic_text()
    {
        const string response = """{"message":"HTTP 200 recebido","password":"senha-secreta","serial":"serial-secreto","access_token":"token-secreto","email":"cliente@example.test","cnpj":"12.345.678/0001-99","telefone":"(11) 99999-1234"}""";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.Contains("HTTP 200 recebido", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
        Assert.DoesNotContain("senha-secreta", sanitized);
        Assert.DoesNotContain("serial-secreto", sanitized);
        Assert.DoesNotContain("token-secreto", sanitized);
        Assert.DoesNotContain("cliente@example.test", sanitized);
        Assert.DoesNotContain("12.345.678/0001-99", sanitized);
        Assert.DoesNotContain("99999-1234", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_authorization_and_personal_values_in_plain_text()
    {
        const string response = "Falha 401 Authorization: Bearer bearer-secret CPF 123.456.789-09 contato pessoa@example.test telefone (21) 98888-7777";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.Contains("Falha 401", sanitized);
        Assert.DoesNotContain("bearer-secret", sanitized);
        Assert.DoesNotContain("123.456.789-09", sanitized);
        Assert.DoesNotContain("pessoa@example.test", sanitized);
        Assert.DoesNotContain("98888-7777", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_nested_customer_objects_and_user_credentials_in_json()
    {
        const string response = """{"client":{"name":"Jane Doe","email":"jane@example.test","address":{"street":"Rua Privada"}},"userName":"seller-login","credential":"credential-secret","message":"Pedido localizado"}""";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.Contains("Pedido localizado", sanitized);
        Assert.DoesNotContain("Jane Doe", sanitized);
        Assert.DoesNotContain("jane@example.test", sanitized);
        Assert.DoesNotContain("Rua Privada", sanitized);
        Assert.DoesNotContain("seller-login", sanitized);
        Assert.DoesNotContain("credential-secret", sanitized);
    }

    [Fact]
    public void Sanitize_returns_null_for_missing_response()
    {
        Assert.Null(WmcAuditDisplayFormatter.Sanitize(null));
        Assert.Null(WmcAuditDisplayFormatter.Sanitize(" \r\n "));
    }

    [Fact]
    public void FormatTimestamp_displays_utc_and_brasilia_with_explicit_offset()
    {
        var display = WmcAuditDisplayFormatter.FormatTimestamp(
            new DateTime(2026, 10, 29, 15, 15, 0, DateTimeKind.Utc));

        Assert.Equal("2026-10-29 15:15:00 +00:00 UTC", display.Utc);
        Assert.Equal("29/10/2026 12:15:00 -03:00 (America/Sao_Paulo)", display.Brasilia);
    }

    [Fact]
    public void FormatTimestamp_treats_unspecified_database_timestamp_as_utc()
    {
        var display = WmcAuditDisplayFormatter.FormatTimestamp(
            new DateTime(2026, 10, 29, 15, 15, 0, DateTimeKind.Unspecified));

        Assert.Equal("2026-10-29 15:15:00 +00:00 UTC", display.Utc);
        Assert.StartsWith("29/10/2026 12:15:00 -03:00", display.Brasilia);
    }
}
