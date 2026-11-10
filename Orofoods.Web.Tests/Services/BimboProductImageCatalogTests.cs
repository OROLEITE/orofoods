using Orofoods.Web.Services.Catalog;

namespace Orofoods.Web.Tests.Services;

public class BimboProductImageCatalogTests
{
    [Fact]
    public void Every_wmc_mapping_uses_its_transparent_png_and_local_source_by_wmc()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var catalog = BimboProductImageCatalog.All;

        Assert.Equal(
            ["503768", "503775", "503777", "503805", "503811", "503813", "503833", "503838", "504338", "504342"],
            catalog.Keys.Order(StringComparer.Ordinal));

        var expectedImages = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["503768"] = "/images/products/bimbo-catalog-transparent/503768_corte_duplo.png",
            ["503775"] = "/images/products/bimbo-catalog-transparent/503775_tradicional_gergelim.png",
            ["503777"] = "/images/products/bimbo-catalog-transparent/503777_smart_gergelim.png",
            ["503805"] = "/images/products/bimbo-catalog-transparent/503805_tradicional.png",
            ["503811"] = "/images/products/bimbo-catalog-transparent/503811_hot_dog.png",
            ["503813"] = "/images/products/bimbo-catalog-transparent/503813_smart.png",
            ["503833"] = "/images/products/bimbo-catalog-transparent/503833_brioche_class.png",
            ["503838"] = "/images/products/bimbo-catalog-transparent/503838_mini_brioche.png",
            ["504338"] = "/images/products/bimbo-catalog-transparent/504338_brioche_max.png",
            ["504342"] = "/images/products/bimbo-catalog-transparent/504342_australiano.png"
        };

        foreach (var (wmcCode, image) in catalog)
        {
            Assert.Equal(expectedImages[wmcCode], image.ImageUrl);
            var imagePath = Path.Combine(projectPath, "wwwroot", image.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(imagePath), $"The packaged image for WMC {wmcCode} is missing.");
            var bytes = File.ReadAllBytes(imagePath);
            Assert.True(bytes.Length > 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e && bytes[3] == 0x47,
                $"The image for WMC {wmcCode} is not a PNG.");
            Assert.True(image.IsIllustrative, $"The generated image for WMC {wmcCode} must be marked illustrative.");
            Assert.True(image.SourceImageUrl.StartsWith("ProdutoTransparente/", StringComparison.Ordinal)
                || Uri.TryCreate(image.SourceImageUrl, UriKind.Absolute, out var sourceUri) && sourceUri.Scheme == Uri.UriSchemeHttps);
            Assert.True(image.UseCatalogImage);
            Assert.False(string.IsNullOrWhiteSpace(image.SourceName));
            Assert.False(string.IsNullOrWhiteSpace(image.ConfirmationStatus));
            Assert.InRange(image.VisualScale, 0.85m, 1.65m);
        }
    }

    [Theory]
    [InlineData("503768", "Tradicional Corte Duplo")]
    [InlineData("503838", "Mini Brioche Corte Total")]
    [InlineData("504342", "Classico Australiano")]
    [InlineData("503833", "Classico Brioche")]
    [InlineData("503811", "Hot Dog")]
    [InlineData("503813", "Smart")]
    [InlineData("503777", "Smart com gergelim")]
    [InlineData("504338", "Max Brioche")]
    [InlineData("503775", "Tradicional com gergelim")]
    [InlineData("503805", "Tradicional")]
    public void Find_returns_the_expected_model_by_wmc(string wmcCode, string expectedModel)
    {
        var image = BimboProductImageCatalog.Find(wmcCode);

        Assert.NotNull(image);
        Assert.Equal(expectedModel, image.ModelName);
    }

    [Fact]
    public void Australiano_uses_the_confirmed_catalog_photo_ahead_of_manual_media()
    {
        Assert.True(BimboProductImageCatalog.Find("504342")!.UseCatalogImage);
        Assert.All(BimboProductImageCatalog.All, image => Assert.True(image.Value.UseCatalogImage));
    }

    [Theory]
    [InlineData("504342", "/images/products/bimbo-catalog-transparent/504342_australiano.png")]
    [InlineData("503811", "/images/products/bimbo-catalog-transparent/503811_hot_dog.png")]
    [InlineData("503768", "/images/products/bimbo-catalog-transparent/503768_corte_duplo.png")]
    public void User_provided_product_photos_are_selected_by_wmc_without_additional_zoom(string wmcCode, string expectedImageUrl)
    {
        var image = BimboProductImageCatalog.Find(wmcCode)!;

        Assert.Equal(expectedImageUrl, image.ImageUrl);
        Assert.True(image.UseCatalogImage);
        Assert.True(image.IsIllustrative);
        Assert.Equal(1m, image.VisualScale);
    }

    [Fact]
    public void Illustrative_images_are_local_and_explicitly_marked()
    {
        var illustrativeCodes = new[] { "503775", "503777", "503805", "503833", "503838", "503768", "503811", "503813", "504338", "504342" };

        foreach (var code in illustrativeCodes)
        {
            var image = BimboProductImageCatalog.Find(code)!;
            var illustrativeProperty = image.GetType().GetProperty("IsIllustrative");

            Assert.NotNull(illustrativeProperty);
            Assert.True((bool)illustrativeProperty!.GetValue(image)!);
            Assert.StartsWith("/images/products/bimbo-catalog-transparent/", image.ImageUrl, StringComparison.Ordinal);
            Assert.True(image.UseCatalogImage);
        }
    }
}
