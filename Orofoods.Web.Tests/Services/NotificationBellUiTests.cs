using System.Text.Json;
using Jint;

namespace Orofoods.Web.Tests.Services;

public class NotificationBellUiTests
{
    [Fact]
    public async Task Bell_polls_posts_before_navigation_recovers_and_rejects_stale_responses()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var script = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "wwwroot", "js", "notification-bell.js"));
        var harness = File.ReadAllText(Path.Combine(root, "Orofoods.Web.Tests", "Services", "notification-bell.harness.cjs"));
        var result = await new Engine().EvaluateAsync(harness + "\nrunNotificationBellTests(" + JsonSerializer.Serialize(script) + ")");
        Assert.Equal("Notification bell behavior passed", result.AsString());
    }
}
