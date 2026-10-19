using Orofoods.Web.Services.Catalog;

namespace Orofoods.Web.Tests.Services;

public class OfficialBimboProductImageCatalogTests
{
    [Theory]
    [InlineData("503768", "Tradicional Corte Duplo", "FT-P%C3%A3o-Supremo-GCD_Rev12_503500010-503500083-503500023-1-300x167.jpg")]
    [InlineData("503838", "Mini Brioche Corte Total", "pao_mini_brioche-1.jpg")]
    [InlineData("504342", "Clássico Australiano", "pao-bqb-hb-classico-australiano-cong-bimbo.webp")]
    [InlineData("503833", "Clássico Brioche", "pao-bqb-classico-brioche-ct-cong-bimbo.webp")]
    [InlineData("503811", "Hot Dog", "pao-bqb-hot-dog-cong-bimbo.webp")]
    [InlineData("503813", "Smart", "FT-P%C3%A3o-Riviera-CT_Rev02_503500106-503500107-503500105-1-300x167.jpg")]
    [InlineData("503777", "Smart com gergelim", "FT-P%C3%A3o-Riviera-G-CT_Rev02_503500126-503500127-503500125-1-300x167.jpg")]
    [InlineData("504338", "Pão Max Brioche", "FT-P%C3%A3o-Brioche-Max-CT_Rev02_503500247-503500245-2.jpg")]
    [InlineData("503775", "Tradicional com gergelim", "FT-P%C3%A3o-M%C3%B4naco-G-CT_Rev02_503500116-503500115-1-300x167.jpg")]
    [InlineData("503805", "Tradicional", "FT-P%C3%A3o-M%C3%B4naco-CT_Rev01_503500044-503500045-1-300x167.jpg")]
    public void Has_a_confirmed_image_for_each_bimbo_catalog_code(
        string wmcCode,
        string expectedModel,
        string expectedImageFile)
    {
        var image = OfficialBimboProductImageCatalog.Find(wmcCode);

        Assert.NotNull(image);
        Assert.Equal(expectedModel, image.ModelName);
        Assert.EndsWith(expectedImageFile, image.ImageUrl, StringComparison.Ordinal);
        Assert.StartsWith("https://", image.ImageUrl, StringComparison.Ordinal);
        Assert.StartsWith("https://", image.ProductPageUrl, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(image.SourceName));
        Assert.False(string.IsNullOrWhiteSpace(image.ConfirmationStatus));
    }
}
