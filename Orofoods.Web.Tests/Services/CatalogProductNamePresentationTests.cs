using Orofoods.Web.Services.Catalog;

namespace Orofoods.Web.Tests.Services;

public sealed class CatalogProductNamePresentationTests
{
    [Theory]
    [InlineData("BIMBO Pao de Forma", "Pao de Forma")]
    [InlineData("bimbo    Pao de Forma 500g", "Pao de Forma 500g")]
    [InlineData("BiMbO\tPao de Forma", "Pao de Forma")]
    [InlineData("BIMBO— Pao de Forma", "— Pao de Forma")]
    [InlineData("BIMBO", "")]
    [InlineData("BIMBOLANDIA Pao", "BIMBOLANDIA Pao")]
    [InlineData("Pao BIMBO", "Pao BIMBO")]
    [InlineData("BIMBO Pao  de Forma", "Pao  de Forma")]
    public void DisplayName_removes_only_a_leading_whole_BIMBO_prefix(string name, string expected)
    {
        Assert.Equal(expected, CatalogProductNamePresentation.DisplayName(name));
    }
}
