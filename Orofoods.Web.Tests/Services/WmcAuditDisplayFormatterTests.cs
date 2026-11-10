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
    public void Sanitize_redacts_xml_sensitive_elements_by_local_name_and_all_attributes()
    {
        const string response = "<root xmlns:ns=\"urn:test\"><ns:password>xml-secret</ns:password><password value=\"attribute-secret\" /></root>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("xml-secret", sanitized);
        Assert.DoesNotContain("attribute-secret", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_sensitive_xml_content_when_body_is_truncated()
    {
        const string response = "<root><ns:password>truncated-xml-secret";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("truncated-xml-secret", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_sensitive_xml_value_when_opening_tag_is_truncated()
    {
        const string response = "<password value=\"credential-secret\"";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("credential-secret", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
    }

    [Fact]
    public void Sanitize_uses_sensitive_key_normalization_for_truncated_compound_xml_names()
    {
        const string response = "<ns:customerName>Jane Doe";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("Jane Doe", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_xml_fragments_embedded_in_plain_text()
    {
        const string response = "WMC error: <password>embedded-xml-secret</password>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("embedded-xml-secret", sanitized);
        Assert.Contains("WMC error:", sanitized);
    }

    [Fact]
    public void Sanitize_does_not_treat_quoted_greater_than_as_an_xml_tag_boundary()
    {
        const string response = "WMC error: <password note=\"x/>\">quoted-xml-secret</password>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("quoted-xml-secret", sanitized);
        Assert.Contains("WMC error:", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_to_end_when_xml_close_may_be_inside_cdata()
    {
        const string response = "WMC error: <password><![CDATA[prefix</password>suffix]]></password>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("suffix", sanitized);
        Assert.Contains("WMC error:", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_xml_fragments_in_document_cdata_text_and_attribute_values()
    {
        const string response = "<response><![CDATA[<password>cdata-secret</password>]]><message>&lt;password&gt;text-secret&lt;/password&gt;</message><item detail=\"&lt;password&gt;attribute-secret&lt;/password&gt;\" /></response>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("cdata-secret", sanitized);
        Assert.DoesNotContain("text-secret", sanitized);
        Assert.DoesNotContain("attribute-secret", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_double_encoded_markup_in_xml_text_and_attribute_values()
    {
        const string response = "<response><message>&amp;lt;password&amp;gt;double-text-secret&amp;lt;/password&amp;gt;</message><item detail=\"&amp;lt;password&amp;gt;double-attribute-secret&amp;lt;/password&amp;gt;\" /></response>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("double-text-secret", sanitized);
        Assert.DoesNotContain("double-attribute-secret", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_xml_fragments_inside_comments()
    {
        const string response = "<response><!-- <password>comment-secret</password> --></response>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("comment-secret", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_entity_encoded_markup_in_xml_comments_and_processing_instructions()
    {
        const string response = "<response><!-- &lt;password&gt;encoded-comment-secret&lt;/password&gt; --><?wmc &lt;password&gt;encoded-pi-secret&lt;/password&gt;?></response>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("encoded-comment-secret", sanitized);
        Assert.DoesNotContain("encoded-pi-secret", sanitized);
    }

    [Fact]
    public void Sanitize_keeps_redacted_comments_and_processing_instructions_well_formed()
    {
        const string response = "<response><!-- &lt;password/&gt;&#45;&#45; --><?wmc &lt;password/&gt;?&gt; ?></response>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("password", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
        Assert.NotNull(System.Xml.Linq.XDocument.Parse(sanitized!));
    }

    [Fact]
    public void Sanitize_redacts_double_entity_encoded_xml_markup_in_comments()
    {
        const string response = "<response><!-- &amp;lt;password&amp;gt;double-encoded-secret&amp;lt;/password&amp;gt; --></response>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("double-encoded-secret", sanitized);
    }

    [Fact]
    public void Sanitize_continues_decoding_after_redacting_raw_xml_in_the_same_value()
    {
        const string response = "<response><!-- <password/> &amp;lt;password&amp;gt;mixed-encoded-secret&amp;lt;/password&amp;gt; --></response>";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("mixed-encoded-secret", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_entity_encoded_xml_fragments_in_plain_and_json_text()
    {
        const string plainText = "WMC error: &lt;password&gt;entity-plain-secret&lt;/password&gt;";
        const string json = "{\"message\":\"WMC error: &lt;password&gt;entity-json-secret&lt;/password&gt;\"}";

        var sanitizedPlainText = WmcAuditDisplayFormatter.Sanitize(plainText);
        var sanitizedJson = WmcAuditDisplayFormatter.Sanitize(json);

        Assert.DoesNotContain("entity-plain-secret", sanitizedPlainText);
        Assert.DoesNotContain("entity-json-secret", sanitizedJson);
    }

    [Fact]
    public void Sanitize_redacts_xml_fragments_inside_json_message_values()
    {
        const string response = "{\"message\":\"WMC error: <password>json-xml-secret</password>\"}";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("json-xml-secret", sanitized);
        Assert.Contains("WMC error:", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_xml_fragments_inside_top_level_json_string_values()
    {
        const string response = "\"<password>scalar-json-xml-secret</password>\"";

        var sanitized = WmcAuditDisplayFormatter.Sanitize(response);

        Assert.DoesNotContain("scalar-json-xml-secret", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
    }

    [Fact]
    public void Sanitize_redacts_whole_unquoted_assignment_until_a_safe_delimiter()
    {
        const string assignment = "password=correct horse battery staple, status=failed";

        var sanitizedAssignment = WmcAuditDisplayFormatter.Sanitize(assignment);

        Assert.DoesNotContain("correct", sanitizedAssignment);
        Assert.DoesNotContain("horse", sanitizedAssignment);
        Assert.DoesNotContain("battery", sanitizedAssignment);
        Assert.Contains("status=failed", sanitizedAssignment);
    }

    [Fact]
    public void Sanitize_redacts_entire_unsupported_authorization_header_value()
    {
        const string authorization = "Authorization: Digest realm=\"wmc\", nonce=\"nonce-secret\", response=\"response-secret\"";

        var sanitizedAuthorization = WmcAuditDisplayFormatter.Sanitize(authorization);

        Assert.DoesNotContain("nonce-secret", sanitizedAuthorization);
        Assert.DoesNotContain("response-secret", sanitizedAuthorization);
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
