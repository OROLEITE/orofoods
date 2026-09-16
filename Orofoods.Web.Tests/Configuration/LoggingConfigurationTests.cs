using System.Text.Json;

namespace Orofoods.Web.Tests.Configuration;

public sealed class LoggingConfigurationTests
{
    [Fact]
    public void Production_logging_suppresses_informational_entity_framework_commands()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectPath, "appsettings.json")));

        var level = configuration.RootElement
            .GetProperty("Logging")
            .GetProperty("LogLevel")
            .GetProperty("Microsoft.EntityFrameworkCore")
            .GetString();

        Assert.Equal("Warning", level);
    }
}
