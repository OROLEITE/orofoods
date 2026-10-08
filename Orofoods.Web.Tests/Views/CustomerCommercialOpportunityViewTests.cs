using System.Reflection;
using System.Text.RegularExpressions;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Tests.Views;

public class CustomerCommercialOpportunityViewTests
{
    [Fact]
    public void Customer_details_prefers_existing_crm_conversation_and_uses_normalized_wa_me_fallback()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Details.cshtml"));

        Assert.Contains("asp-controller=\"WhatsApp\"", view);
        Assert.Contains("asp-route-id=\"@Model.WhatsAppConversationId\"", view);
        Assert.Contains("asp-route-customerId=\"@Model.Customer.Id\"", view);
        Assert.Contains("asp-route-markAsRead=\"false\"", view);
        Assert.Contains("https://wa.me/@whatsappPhoneNumber", view);
        Assert.Contains("Model.WhatsAppPhoneNumber is string whatsappPhoneNumber", view);
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", view);
        Assert.Contains("Cliente sem telefone WhatsApp válido", view);
        Assert.Contains("Model.PhoneNumberForCall is string phoneNumberForCall", view);
        Assert.Contains("href=\"tel:@phoneNumberForCall\"", view);
        Assert.Contains("aria-disabled=\"true\" title=\"Cliente sem telefone válido\"", view);
    }

    [Theory]
    [InlineData("(19) 99876-5432", "(19) 99876-5432")]
    [InlineData("", null)]
    [InlineData("0000000000", null)]
    [InlineData("987654321", null)]
    public void Customer_call_action_only_exposes_valid_normalizable_phone_numbers(
        string phone,
        string? expected)
    {
        var viewModel = new Orofoods.Web.ViewModels.CustomerCommercialViewModel
        {
            Customer = new Orofoods.Web.Models.Customers.Customer
            {
                Phone = phone
            }
        };

        Assert.Equal(expected, viewModel.PhoneNumberForCall);
    }

    [Fact]
    public void Customer_details_loads_page_scoped_responsive_layout_without_fixed_card_heights()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Details.cshtml"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-customer-details.css"));

        Assert.Contains("Values[\"controller\"]?.ToString() == \"Customers\"", layout);
        Assert.Contains("Values[\"action\"]?.ToString() == \"Details\"", layout);
        Assert.Contains("~/css/admin-customer-details.css", layout);
        Assert.Contains(".customer-commercial-page .customer-commercial-hero h1", styles);
        Assert.Contains(".customer-commercial-page .customer-commercial-actions", styles);
        Assert.Contains(".customer-commercial-page .customer-commercial-metrics", styles);
        Assert.Contains(".customer-commercial-page .customer-opportunities-panel", styles);
        Assert.Contains(".customer-commercial-page #overview .customer-detail-grid", styles);
        Assert.Contains(".customer-commercial-page .customer-detail-tabs-shell", styles);
        Assert.Contains("@media (max-width: 900px)", styles);
        Assert.Contains("@media (max-width: 600px)", styles);
        Assert.DoesNotMatch(@"(?:^|[;{])\s*(?:height|max-height)\s*:", styles);
    }

    [Theory]
    [InlineData("(19) 99876-5432", "", "5519998765432")]
    [InlineData("", "+351 912 345 678", "351912345678")]
    [InlineData("987654321", "", null)]
    public void Customer_details_only_exposes_normalized_whatsapp_numbers(
        string phone,
        string whatsapp,
        string? expected)
    {
        var viewModel = new Orofoods.Web.ViewModels.CustomerCommercialViewModel
        {
            Customer = new Orofoods.Web.Models.Customers.Customer
            {
                Phone = phone,
                WhatsApp = whatsapp
            }
        };

        Assert.Equal(expected, viewModel.WhatsAppPhoneNumber);
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
        Assert.Contains("name=\"stage\"", view);
        Assert.Contains("asp-action=\"ChangeStage\" method=\"post\"", view);
        Assert.Contains(">Salvar</button>", view);
        Assert.DoesNotContain("customer-opportunity-card__status", view);
        Assert.Contains(".customer-commercial-page .customer-opportunity-card", styles);
        Assert.Contains("@media(max-width:900px)", styles);

        var pageStyles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-customer-details.css"));
        Assert.Contains("grid-template-columns: minmax(0, 150px) auto", pageStyles);
        Assert.Contains(".customer-commercial-page .customer-opportunity-stage-form select", pageStyles);

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
