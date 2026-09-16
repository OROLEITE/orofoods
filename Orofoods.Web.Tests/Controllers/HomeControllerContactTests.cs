using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Controllers;
using Orofoods.Web.Infrastructure;
using Orofoods.Web.Models;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class HomeControllerContactTests
{
    [Fact]
    public async Task Contact_post_returns_view_when_model_state_is_invalid()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = new HomeController(db);
        controller.ModelState.AddModelError(nameof(ContactViewModel.AcceptPrivacyPolicy), "required");

        var result = await controller.Contact(new ContactViewModel());

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await db.ContactMessages.ToListAsync());
    }

    [Fact]
    public async Task Contact_post_persists_valid_message_and_redirects()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = new HomeController(db)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
        var input = new ContactViewModel
        {
            Name = "Ana Souza", Email = "ana@burger.com", Phone = "(19) 3000-1000",
            City = "Campinas", Message = "Quero conhecer a Orofoods.", AcceptPrivacyPolicy = true
        };

        var result = await controller.Contact(input);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotEqual(default, (await db.ContactMessages.SingleAsync()).PrivacyConsentAt);
    }

    [Fact]
    public async Task Error_uses_the_correlation_identifier_as_the_customer_reference()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.ItemName] = "e72342b5-8f4f-4daa-a096-8172e89e9968";
        var controller = new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal("e72342b5-8f4f-4daa-a096-8172e89e9968", model.RequestId);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
