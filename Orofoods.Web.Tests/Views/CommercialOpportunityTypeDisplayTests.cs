using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Tests.Views;

public class CommercialOpportunityTypeDisplayTests
{
    [Theory]
    [InlineData(CrmOpportunityType.Repurchase, "Recompra")]
    [InlineData(CrmOpportunityType.NewProduct, "Novo produto")]
    [InlineData(CrmOpportunityType.CrossSell, "Venda cruzada")]
    [InlineData(CrmOpportunityType.CustomerRecovery, "Recuperação de cliente")]
    [InlineData(CrmOpportunityType.Proposal, "Proposta")]
    [InlineData(CrmOpportunityType.Other, "Outro")]
    public void Opportunity_type_has_a_portuguese_display_name_and_unchanged_internal_value(
        CrmOpportunityType type,
        string expectedLabel)
    {
        var attribute = typeof(CrmOpportunityType)
            .GetField(type.ToString())?
            .GetCustomAttribute<DisplayAttribute>();

        Assert.Equal(expectedLabel, attribute?.GetName());
        Assert.Equal(expectedLabel, type.ToDisplayName());
        Assert.Equal(Enum.GetName(type), type.ToString());
    }

    [Fact]
    public void Opportunity_type_labels_are_shared_across_create_list_and_customer_details()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var createView = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Opportunities", "Create.cshtml"));
        var listView = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Opportunities", "Index.cshtml"));
        var customerDetailsView = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Customers", "Details.cshtml"));

        Assert.Contains("Html.GetEnumSelectList<Orofoods.Web.Models.Commercial.CrmOpportunityType>()", createView);
        Assert.Contains("@opportunity.Type.ToDisplayName()", listView);
        Assert.Contains("@opportunity.Type.ToDisplayName()", customerDetailsView);
    }

    [Fact]
    public void Opportunity_type_numeric_values_remain_the_existing_select_and_storage_values()
    {
        Assert.Equal(
            new[] { 0, 1, 2, 3, 4, 5 },
            Enum.GetValues<CrmOpportunityType>().Select(type => (int)type));
    }

    [Fact]
    public void Opportunity_list_uses_compact_cards_with_grouped_status_actions_and_natural_height()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Opportunities", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-opportunities.css"));

        Assert.Contains("class=\"admin-opportunity-card\"", view);
        Assert.Contains("class=\"admin-opportunity-card__value\"", view);
        Assert.Contains("class=\"admin-opportunity-stage-form\"", view);
        Assert.Contains("admin-opportunity-empty-action", view);
        Assert.Contains("asp-action=\"ChangeStage\"", view);
        Assert.Contains(".admin-opportunities-page .admin-opportunity-card", styles);
        Assert.Contains(".admin-opportunities-page .admin-opportunity-card__value", styles);

        var cardRule = Regex.Match(
            styles,
            @"\.admin-opportunities-page \.admin-opportunity-card\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline);

        Assert.True(cardRule.Success);
        Assert.DoesNotMatch(@"\b(?:height|min-height|max-height)\s*:", cardRule.Groups["body"].Value);
    }
}
