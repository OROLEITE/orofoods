using System.Reflection;
using System.Text.RegularExpressions;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Tests.Views;

public class CustomerCommercialOpportunityViewTests
{
    [Fact]
    public void Customer_details_uses_internal_whatsapp_navigation_without_external_links()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Details.cshtml"));

        Assert.DoesNotContain("wa.me", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("asp-controller=\"WhatsApp\"", view);
        Assert.Contains("asp-route-id=\"@Model.WhatsAppConversationId\"", view);
        Assert.Contains("asp-route-customerId=\"@Model.Customer.Id\"", view);
        Assert.Contains("asp-route-markAsRead=\"false\"", view);
        Assert.Contains("Model.HasValidWhatsAppPhone", view);
        Assert.Contains("selectConversation", view);
    }

    [Fact]
    public void Customer_opportunity_cards_keep_internal_stage_values_and_use_display_labels()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Details.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-customers.css"));

        Assert.Contains("class=\"customer-opportunity-card\"", view);
        Assert.Contains("@opportunity.Type.ToDisplayName()", view);
        Assert.Contains("@opportunity.Stage.ToDisplayName()", view);
        Assert.Contains("<option value=\"@opportunity.Stage\">@opportunity.Stage.ToDisplayName()</option>", view);
        Assert.Contains("<option value=\"Won\">Ganha</option>", view);
        Assert.Contains("<option value=\"Lost\">Perdida</option>", view);
        Assert.Contains(".customer-commercial-page .customer-opportunity-card", styles);
        Assert.Contains("@media(max-width:900px)", styles);

        var cardRule = Regex.Match(
            styles,
            @"\.customer-commercial-page \.customer-opportunity-card\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline);
        Assert.True(cardRule.Success);
        Assert.DoesNotMatch(@"\b(?:height|min-height|max-height)\s*:", cardRule.Groups["body"].Value);
    }

    [Fact]
    public void Opportunity_stage_display_helper_covers_every_internal_stage()
    {
        var extensionType = typeof(CrmOpportunityStage).Assembly.GetType("Orofoods.Web.Models.Commercial.CrmOpportunityStageDisplayExtensions");
        Assert.NotNull(extensionType);
        var method = extensionType!.GetMethod("ToDisplayName", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        var expected = new Dictionary<CrmOpportunityStage, string>
        {
            [CrmOpportunityStage.Open] = "Aberta",
            [CrmOpportunityStage.Contacted] = "Em contato",
            [CrmOpportunityStage.Proposal] = "Proposta",
            [CrmOpportunityStage.Negotiation] = "Em negocia\u00e7\u00e3o",
            [CrmOpportunityStage.Won] = "Ganha",
            [CrmOpportunityStage.Lost] = "Perdida",
            [CrmOpportunityStage.Cancelled] = "Cancelada"
        };

        foreach (var pair in expected)
            Assert.Equal(pair.Value, method!.Invoke(null, [pair.Key]));
    }
}
