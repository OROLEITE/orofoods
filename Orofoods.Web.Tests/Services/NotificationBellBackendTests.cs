using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class NotificationBellBackendTests
{
    [Fact]
    public async Task Json_poll_is_private_does_not_mark_read_and_never_returns_another_users_items()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Users.AddRange(TestDbContextFactory.CreateOrderCreator("operator"), TestDbContextFactory.CreateOrderCreator("other"));
        db.UserNotifications.AddRange(Item("operator", "/Admin/WhatsApp?id=42"), Item("other", "/private"));
        await db.SaveChangesAsync();
        var controller = Controller(new UserNotificationService(db));

        var payload = Payload(await controller.Index(default));

        Assert.Equal(1, payload.GetProperty("unreadCount").GetInt32());
        var item = Assert.Single(payload.GetProperty("items").EnumerateArray());
        Assert.Equal("/Admin/WhatsApp?id=42", item.GetProperty("actionUrl").GetString());
        Assert.False(item.TryGetProperty("userId", out _));
        Assert.Contains("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.All(await db.UserNotifications.AsNoTracking().ToListAsync(), x => Assert.Null(x.ReadAt));
    }

    [Fact]
    public async Task Read_cannot_mark_or_disclose_another_users_notification()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Users.AddRange(TestDbContextFactory.CreateOrderCreator("operator"), TestDbContextFactory.CreateOrderCreator("other"));
        var foreign = Item("other", "/secret");
        db.UserNotifications.Add(foreign);
        await db.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await Controller(new UserNotificationService(db)).Read(foreign.Id, default));
        db.ChangeTracker.Clear();
        Assert.Null((await db.UserNotifications.SingleAsync()).ReadAt);
    }

    [Fact]
    public async Task Read_is_persisted_idempotently_and_does_not_read_the_whatsapp_conversation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Users.Add(TestDbContextFactory.CreateOrderCreator("operator"));
        var item = Item("operator", "/Admin/WhatsApp?id=42");
        var sibling = Item("operator", "/Admin/WhatsApp");
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999999999", UnreadCount = 7, Status = WhatsAppConversationStatus.Pending };
        db.AddRange(item, sibling, conversation);
        await db.SaveChangesAsync();
        var controller = Controller(new UserNotificationService(db));

        var payload = Payload(await controller.Read(item.Id, default));
        Assert.Equal(1, payload.GetProperty("unreadCount").GetInt32());
        Assert.Equal("/Admin/WhatsApp?id=42", payload.GetProperty("actionUrl").GetString());
        db.ChangeTracker.Clear();
        var firstRead = (await db.UserNotifications.SingleAsync(x => x.Id == item.Id)).ReadAt;
        Assert.NotNull(firstRead);
        await controller.Read(item.Id, default);
        db.ChangeTracker.Clear();
        Assert.Equal(firstRead, (await db.UserNotifications.SingleAsync(x => x.Id == item.Id)).ReadAt);
        Assert.Null((await db.UserNotifications.SingleAsync(x => x.Id == sibling.Id)).ReadAt);
        var savedConversation = await db.WhatsAppConversations.SingleAsync();
        Assert.Equal(7, savedConversation.UnreadCount);
        Assert.Equal(WhatsAppConversationStatus.Pending, savedConversation.Status);
    }

    [Fact]
    public async Task Read_all_persists_only_the_current_users_unread_items_and_keeps_existing_read_time()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Users.AddRange(TestDbContextFactory.CreateOrderCreator("operator"), TestDbContextFactory.CreateOrderCreator("other"));
        var oldTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var alreadyRead = Item("operator", "/");
        alreadyRead.ReadAt = oldTime;
        db.UserNotifications.AddRange(alreadyRead, Item("operator", "/"), Item("operator", "/"), Item("other", "/"));
        await db.SaveChangesAsync();

        var payload = Payload(await Controller(new UserNotificationService(db)).ReadAll(default));
        Assert.Equal(0, payload.GetProperty("unreadCount").GetInt32());
        db.ChangeTracker.Clear();
        Assert.All(await db.UserNotifications.Where(x => x.UserId == "operator").ToListAsync(), x => Assert.NotNull(x.ReadAt));
        Assert.Equal(oldTime, (await db.UserNotifications.SingleAsync(x => x.Id == alreadyRead.Id)).ReadAt);
        Assert.Null((await db.UserNotifications.SingleAsync(x => x.UserId == "other")).ReadAt);
    }

    [Fact]
    public async Task Legacy_whatsapp_notification_resolves_conversation_from_its_exact_external_message()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Users.Add(TestDbContextFactory.CreateOrderCreator("operator"));
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999988888" };
        var notification = Item("operator", "/Admin/WhatsApp");
        notification.Type = UserNotificationType.WhatsAppMessageReceived;
        notification.DeduplicationKey = "WHATSAPP_MESSAGE:wamid.old-notification";
        db.AddRange(conversation, notification, new WhatsAppMessage { Conversation = conversation, ExternalMessageId = "wamid.old-notification", TextBody = "Legado" });
        await db.SaveChangesAsync();
        var payload = Payload(await Controller(new UserNotificationService(db)).Read(notification.Id, default));
        Assert.Equal($"/Admin/WhatsApp?id={conversation.Id}", payload.GetProperty("actionUrl").GetString());
    }

    [Theory]
    [InlineData("https://evil.test/")]
    [InlineData("//evil.test/")]
    [InlineData("/\\evil.test/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/path\nLocation: evil")]
    public async Task Unsafe_stored_links_are_never_exposed_as_navigation_targets(string unsafeUrl)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Users.Add(TestDbContextFactory.CreateOrderCreator("operator"));
        var item = Item("operator", unsafeUrl);
        db.UserNotifications.Add(item);
        await db.SaveChangesAsync();
        var controller = Controller(new UserNotificationService(db));

        var feed = Payload(await controller.Index(default));
        Assert.Equal("/Admin/Notifications", feed.GetProperty("items")[0].GetProperty("actionUrl").GetString());
        var read = Payload(await controller.Read(item.Id, default));
        Assert.Equal("/Admin/Notifications", read.GetProperty("actionUrl").GetString());
    }

    [Fact]
    public async Task Existing_form_posts_keep_redirects_and_missing_identity_is_forbidden()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Users.Add(TestDbContextFactory.CreateOrderCreator("operator"));
        var item = Item("operator", "/");
        db.UserNotifications.Add(item);
        await db.SaveChangesAsync();
        var controller = Controller(new UserNotificationService(db));
        controller.Request.Headers.Accept = "text/html";
        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(await controller.Read(item.Id, default)).ActionName);
        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(await controller.ReadAll(default)).ActionName);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        Assert.IsType<ForbidResult>(await controller.Index(default));
        Assert.IsType<ForbidResult>(await controller.Read(item.Id, default));
        Assert.IsType<ForbidResult>(await controller.ReadAll(default));
    }

    [Fact]
    public void Notification_endpoints_allow_operator_and_require_antiforgery_for_writes()
    {
        var policy = Assert.Single(typeof(NotificationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Contains(ApplicationRoles.Operator, policy.Roles!.Split(','));
        Assert.DoesNotContain(ApplicationRoles.Customer, policy.Roles.Split(','));
        foreach (var action in new[] { nameof(NotificationsController.Read), nameof(NotificationsController.ReadAll) })
        {
            var method = typeof(NotificationsController).GetMethod(action)!;
            Assert.NotEmpty(method.GetCustomAttributes(typeof(HttpPostAttribute), true));
            Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
        }
    }

    private static UserNotification Item(string userId, string url) => new()
    {
        UserId = userId, Title = "Mensagem", Message = "Nova mensagem", ActionUrl = url,
        Type = UserNotificationType.WhatsAppMessageReceived
    };

    private static NotificationsController Controller(UserNotificationService service)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "operator"), new Claim(ClaimTypes.Role, ApplicationRoles.Operator)
            ], "test"))
        };
        http.Request.Headers.Accept = "application/json";
        var controller = new NotificationsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = http,
                RouteData = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor { ControllerName = "Notifications", ActionName = "Index" }
            }
        };
        controller.Url = new NotificationUrlHelper(controller.ControllerContext);
        return controller;
    }

    private sealed class NotificationUrlHelper(ActionContext context) : UrlHelper(context)
    {
        public override string? Action(UrlActionContext actionContext) => "/Admin/Notifications";
    }

    private static JsonElement Payload(IActionResult result) =>
        JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
}
