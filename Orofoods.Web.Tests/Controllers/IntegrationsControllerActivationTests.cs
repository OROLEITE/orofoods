using Microsoft.Extensions.DependencyInjection;
using Orofoods.Web.Areas.Admin.Controllers;

namespace Orofoods.Web.Tests.Controllers;

public sealed class IntegrationsControllerActivationTests
{
    [Fact]
    public void Integrations_controller_has_an_unambiguous_mvc_activation_constructor_for_index()
    {
        var factory = ActivatorUtilities.CreateFactory(typeof(IntegrationsController), Type.EmptyTypes);

        Assert.NotNull(factory);
    }
}
