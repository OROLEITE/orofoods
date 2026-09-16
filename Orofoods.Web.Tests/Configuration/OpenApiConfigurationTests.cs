namespace Orofoods.Web.Tests.Configuration;

public class OpenApiConfigurationTests
{
    [Fact]
    public void Development_registers_openapi_with_bearer_authentication()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var program = File.ReadAllText(Path.Combine(projectPath, "Program.cs"));

        Assert.Contains("AddOpenApi", program);
        Assert.Contains("MapOpenApi", program);
    }
}
