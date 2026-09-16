namespace Orofoods.Web.Tests.Configuration;

public class HealthCheckConfigurationTests
{
    [Fact]
    public void Health_endpoint_checks_database_connectivity_without_exposing_infrastructure_details()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var program = File.ReadAllText(Path.Combine(projectPath, "Program.cs"));

        Assert.Contains("CanConnectAsync", program);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", program);
        Assert.Contains("new { status = \"healthy\" }", program);
        Assert.DoesNotContain("homologation-file-drop", program);
        Assert.DoesNotContain("erp =", program);
    }
}
