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
    public void Production_data_protection_uses_an_external_protected_key_directory()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Program.cs"));

        Assert.Contains("DataProtection:KeyDirectory", source);
        Assert.Contains("ProtectKeysWithDpapi", source);
    }
}
