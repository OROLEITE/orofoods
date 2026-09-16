using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models;

namespace Orofoods.Web.Tests.Models;

public class StatusDisplayExtensionsTests
{
    [Theory]
    [InlineData(OrderStatus.Received, "Recebido")]
    [InlineData(OrderStatus.UnderReview, "Em analise")]
    [InlineData(OrderStatus.OutForDelivery, "Em rota de entrega")]
    [InlineData(OrderStatus.Cancelled, "Cancelado")]
    public void Order_status_has_portuguese_label(OrderStatus status, string expected)
    {
        Assert.Equal(expected, status.ToDisplayName());
    }

    [Theory]
    [InlineData(IntegrationStatus.Pending, "Pendente")]
    [InlineData(IntegrationStatus.Processing, "Processando")]
    [InlineData(IntegrationStatus.Succeeded, "Integrado")]
    [InlineData(IntegrationStatus.Failed, "Falhou")]
    public void Integration_status_has_portuguese_label(IntegrationStatus status, string expected)
    {
        Assert.Equal(expected, status.ToDisplayName());
    }
}
