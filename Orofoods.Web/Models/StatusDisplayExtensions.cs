using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models;

public static class StatusDisplayExtensions
{
    public static string ToDisplayName(this OrderStatus status) => status switch
    {
        OrderStatus.Draft => "Rascunho",
        OrderStatus.Received => "Recebido",
        OrderStatus.UnderReview => "Em analise",
        OrderStatus.Approved => "Aprovado",
        OrderStatus.Picking => "Em separacao",
        OrderStatus.Invoiced => "Faturado",
        OrderStatus.OutForDelivery => "Em rota de entrega",
        OrderStatus.Delivered => "Entregue",
        OrderStatus.Cancelled => "Cancelado",
        _ => status.ToString()
    };

    public static string ToDisplayName(this IntegrationStatus status) => status switch
    {
        IntegrationStatus.Pending => "Pendente",
        IntegrationStatus.Processing => "Processando",
        IntegrationStatus.Succeeded => "Integrado",
        IntegrationStatus.Failed => "Falhou",
        _ => status.ToString()
    };
}
