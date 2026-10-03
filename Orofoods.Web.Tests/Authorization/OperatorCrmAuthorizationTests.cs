using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Storage;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Authorization;

public class OperatorCrmAuthorizationTests
{
    [Fact]
    public void WhatsApp_controller_explicitly_allows_operator_but_user_crud_stays_admin_only()
    {
        var whatsAppRoles = typeof(WhatsAppController).GetCustomAttribute<AuthorizeAttribute>()?.Roles;
        var userRoles = typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>()?.Roles;

        Assert.Contains("Operador", whatsAppRoles);
        Assert.Contains("Administrador", whatsAppRoles);
        Assert.Contains("Vendedor", whatsAppRoles);
        Assert.Contains("GerenteComercial", whatsAppRoles);
        Assert.Equal("Administrador", userRoles);
    }

    [Fact]
    public void No_other_admin_controller_grants_operator_role()
    {
        var adminControllers = typeof(WhatsAppController).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(WhatsAppController).Namespace && type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controller in adminControllers.Where(type => type != typeof(WhatsAppController)))
        {
            var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();
            Assert.True(authorize is not null, $"{controller.Name} lacks controller-level authorization.");
            Assert.DoesNotContain("Operador", authorize!.Roles ?? string.Empty);
        }
    }

    [Fact]
    public async Task Operator_sees_shared_conversation_queue_and_can_reopen_closed_conversation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var assignedAgent = TestDbContextFactory.CreateOrderCreator("staff-agent");
        db.Users.Add(assignedAgent);
        var first = new WhatsAppConversation { PhoneNumber = "+5511999990001", AssignedUserId = assignedAgent.Id };
        var second = new WhatsAppConversation { PhoneNumber = "+5511999990002", Status = WhatsAppConversationStatus.Closed, AssignedUserId = assignedAgent.Id };
        db.WhatsAppConversations.AddRange(first, second);
        await db.SaveChangesAsync();
        var controller = CreateController(db, "operator-id", "Operador");

        var result = Assert.IsType<ViewResult>(await controller.Index(null, CancellationToken.None));
        var model = Assert.IsType<WhatsAppInboxViewModel>(result.Model);
        Assert.Equal(2, model.Conversations.Count);
        var reopen = typeof(WhatsAppController).GetMethod("Reopen");
        Assert.NotNull(reopen);
        Assert.NotNull(reopen!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        var action = (Task<IActionResult>)reopen.Invoke(controller, [second.Id, CancellationToken.None])!;
        Assert.IsType<RedirectToActionResult>(await action);
        Assert.Equal(WhatsAppConversationStatus.Pending, await db.WhatsAppConversations.Where(x => x.Id == second.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Operator_updates_expose_shared_messages_and_media()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var assignedAgent = TestDbContextFactory.CreateOrderCreator("other-agent");
        db.Users.Add(assignedAgent);
        var conversation = new WhatsAppConversation { PhoneNumber = "+5511999990003", AssignedUserId = assignedAgent.Id };
        db.WhatsAppConversations.Add(conversation);
        await db.SaveChangesAsync();
        var message = new WhatsAppMessage
        {
            ConversationId = conversation.Id,
            Direction = WhatsAppMessageDirection.Inbound,
            Type = WhatsAppMessageType.Image,
            MimeType = "image/jpeg",
            MediaState = WhatsAppMediaState.Available,
            MediaStorageReference = "test-media-ref",
            Status = WhatsAppMessageStatus.Delivered
        };
        db.WhatsAppMessages.Add(message);
        await db.SaveChangesAsync();
        var mediaStorage = new Mock<IWhatsAppMediaStorage>();
        mediaStorage.Setup(storage => storage.OpenReadAsync("test-media-ref", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppMediaReadResult(new MemoryStream([1]), "image/jpeg", "image.jpg", 1));
        var controller = CreateController(db, "operator-id", "Operador", mediaStorage.Object);

        var updates = Assert.IsType<JsonResult>(await controller.Updates(conversation.Id, CancellationToken.None));
        var payload = updates.Value!;
        var conversations = (System.Collections.IEnumerable)payload.GetType().GetProperty("conversations")!.GetValue(payload)!;
        Assert.Single(conversations.Cast<object>());
        var messages = (System.Collections.IEnumerable)payload.GetType().GetProperty("messages")!.GetValue(payload)!;
        Assert.Single(messages.Cast<object>());

        var media = Assert.IsType<FileStreamResult>(await controller.Media(message.Id, CancellationToken.None));
        Assert.Equal("image/jpeg", media.ContentType);
    }

    [Fact]
    public void Operator_navigation_has_only_the_WhatsApp_destination()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Views/Shared/_AdminNavigation.cshtml"));
        var navigation = File.ReadAllText(path);
        Assert.Contains("isOperator", navigation);
        Assert.Contains("if (isOperator)", navigation);
        Assert.Contains("asp-controller=\"WhatsApp\"", navigation);
        Assert.Contains("else if (isCommercialUser)", navigation);
    }

    [Fact]
    public void WhatsApp_view_offers_reopen_for_closed_threads_and_hides_customer_details_from_operator()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Areas/Admin/Views/WhatsApp/Index.cshtml"));
        var view = File.ReadAllText(path);

        Assert.Contains("else if (selected.Status == WhatsAppConversationStatus.Closed)", view);
        Assert.Contains("asp-action=\"Reopen\"", view);
        Assert.Contains("if (!User.IsInRole(ApplicationRoles.Operator))", view);
        Assert.Contains("asp-action=\"Details\"", view);
    }

    private static WhatsAppController CreateController(ApplicationDbContext db, string userId, string role, IWhatsAppMediaStorage? mediaStorage = null)
    {
        var controller = new WhatsAppController(db, new SalesRepresentativeAccessService(db),
            Mock.Of<IWhatsAppBusinessGateway>(), mediaStorage)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "test"))
                }
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
        return controller;
    }
}
