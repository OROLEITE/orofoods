using System.ComponentModel.DataAnnotations;
using System.Reflection;
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
}
