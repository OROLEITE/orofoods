namespace Orofoods.Web.Tests.Infrastructure;

public sealed class ApplicationSecurityConfigurationTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Authentication_api_requires_lockout_aware_sign_in_and_approved_customer_access()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Controllers", "Api", "V1", "AuthController.cs"));

        Assert.Contains("[EnableRateLimiting(\"auth\")]", source);
        Assert.Contains("CheckPasswordSignInAsync", source);
        Assert.Contains("HasApprovedCustomerAccessAsync", source);
    }

    [Fact]
    public void Application_uses_partitioned_auth_rate_limiting_and_non_verbose_health_check()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Program.cs"));

        Assert.Contains("AddPolicy(\"auth\"", source);
        Assert.Contains("RateLimitPartition.GetFixedWindowLimiter", source);
        Assert.Contains("new { status = \"healthy\" }", source);
        Assert.DoesNotContain("database = \"connected\"", source);
        Assert.DoesNotContain("erp = erpStatus", source);
    }

    [Fact]
    public void Production_uses_restricted_forwarded_headers_before_https_redirection()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Program.cs"));

        Assert.Contains("ForwardedHeadersOptions", source);
        Assert.Contains("XForwardedFor | ForwardedHeaders.XForwardedProto", source);
        Assert.Contains("10.0.0.0/8", source);
        Assert.Contains("172.16.0.0/12", source);
        Assert.Contains("192.168.0.0/16", source);
        Assert.True(source.IndexOf("app.UseForwardedHeaders();", StringComparison.Ordinal) < source.IndexOf("app.UseHttpsRedirection();", StringComparison.Ordinal));
        Assert.DoesNotContain("IsolatedDataProtectionProbe", source);
        Assert.DoesNotContain("AzureIdentityDiagnostics", source);
    }

    [Fact]
    public void Production_data_protection_uses_an_external_protected_key_directory()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Program.cs"));

        Assert.Contains("DataProtection:KeyDirectory", source);
        Assert.Contains("ProtectKeysWithDpapi", source);
    }

    [Fact]
    public void Data_protection_configures_local_development_keys_and_validated_azure_blob_key_vault_storage()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Program.cs"));

        Assert.Contains("if (builder.Environment.IsDevelopment())", source);
        Assert.Contains("PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, \"DataProtectionKeys\")))", source);
        Assert.Contains("string.IsNullOrWhiteSpace(blobUri) || string.IsNullOrWhiteSpace(keyVaultKeyIdentifier)", source);
        Assert.Contains("DataProtection:Azure habilitado exige ApplicationName, BlobUri e KeyVaultKeyIdentifier", source);
        Assert.Contains("new DefaultAzureCredential()", source);
        Assert.DoesNotContain("new ClientSecretCredential", source);
        Assert.Contains("PersistKeysToAzureBlobStorage(new Uri(blobUri), azureCredential)", source);
        Assert.Contains("ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyIdentifier), azureCredential)", source);
    }

    [Fact]
    public void Card_payment_types_never_expose_a_full_card_number_or_security_code_field()
    {
        var forbidden = new[] { "cardnumber", "pan", "cvv", "securitycode" };

        foreach (var type in new[]
                 {
                     typeof(Orofoods.Web.Models.Payments.Payment),
                     typeof(Orofoods.Web.Services.Payments.CreateCreditCardPaymentRequest),
                     typeof(Orofoods.Web.ViewModels.CreateCardAttemptRequest)
                 })
        {
            foreach (var property in type.GetProperties())
            {
                var normalized = property.Name.ToLowerInvariant();
                Assert.DoesNotContain(forbidden, term => normalized.Contains(term));
            }
        }
    }

    [Fact]
    public void Payment_orchestration_and_gateway_never_log_the_card_token_or_security_code()
    {
        var orchestrationSource = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Services", "Payments", "PaymentOrchestrationService.cs"));
        var gatewaySource = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Services", "Payments", "MercadoPagoPaymentGateway.cs"));

        foreach (var source in new[] { orchestrationSource, gatewaySource })
        {
            var logLines = source.Split('\n').Where(line => line.Contains("Log"));
            foreach (var line in logLines)
            {
                Assert.DoesNotContain("CardToken", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("SecurityCode", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("cvv", line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Card_payment_flow_never_references_the_wmc_erp_integration()
    {
        foreach (var file in new[] { "PaymentOrchestrationService.cs", "MercadoPagoPaymentGateway.cs" })
        {
            var source = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Services", "Payments", file));
            Assert.DoesNotContain("IErpOrderIntegration", source);
            Assert.DoesNotContain("Wmc", source);
        }

        var controllerSource = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Controllers", "Api", "V1", "PaymentsController.cs"));
        Assert.DoesNotContain("IErpOrderIntegration", controllerSource);
        Assert.DoesNotContain("Wmc", controllerSource);
    }
}
