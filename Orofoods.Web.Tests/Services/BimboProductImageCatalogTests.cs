using Orofoods.Web.Services.Catalog;
using System.Buffers.Binary;

namespace Orofoods.Web.Tests.Services;

public class BimboProductImageCatalogTests
{
    [Fact]
    public void Every_wmc_mapping_uses_a_packaged_local_image_and_a_confirming_source()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var catalog = BimboProductImageCatalog.All;

        Assert.Equal(
            ["503768", "503775", "503777", "503805", "503811", "503813", "503833", "503838", "504338", "504342"],
            catalog.Keys.Order(StringComparer.Ordinal));

        foreach (var (wmcCode, image) in catalog)
        {
            Assert.StartsWith("/images/products/", image.ImageUrl, StringComparison.Ordinal);
            Assert.DoesNotContain("https://", image.ImageUrl, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(projectPath, "wwwroot", image.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))),
                $"The packaged image for WMC {wmcCode} is missing.");
            if (image.IsIllustrative)
            {
                var bytes = File.ReadAllBytes(Path.Combine(projectPath, "wwwroot", image.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                var dimensions = ReadDimensions(bytes);
                Assert.Equal(800, dimensions.Width);
                Assert.Equal(600, dimensions.Height);
            }
            if (!image.IsIllustrative)
            {
                Assert.StartsWith("https://", image.SourceImageUrl, StringComparison.Ordinal);
            }
            Assert.StartsWith("https://", image.ProductPageUrl, StringComparison.Ordinal);
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
        Assert.All(BimboProductImageCatalog.All.Where(image => image.Key != "504342" && !image.Value.IsIllustrative),
            image => Assert.False(image.Value.UseCatalogImage));
    }

    [Fact]
    public void Illustrative_images_are_local_and_explicitly_marked()
    {
        var illustrativeCodes = new[] { "503775", "503777", "503805", "503833", "503838" };

        foreach (var code in illustrativeCodes)
        {
            var image = BimboProductImageCatalog.Find(code)!;
            var illustrativeProperty = image.GetType().GetProperty("IsIllustrative");

            Assert.NotNull(illustrativeProperty);
            Assert.True((bool)illustrativeProperty!.GetValue(image)!);
            Assert.StartsWith("/images/products/bimbo-illustrative/", image.ImageUrl, StringComparison.Ordinal);
            Assert.True(image.UseCatalogImage);
        }
    }

    private static (int Width, int Height) ReadDimensions(byte[] bytes)
    {
        if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e && bytes[3] == 0x47)
        {
            return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        }

        if (bytes.Length >= 30 && bytes.AsSpan(12, 4).SequenceEqual("VP8 "u8))
        {
            return (bytes[26] | ((bytes[27] & 0x3f) << 8), bytes[28] | ((bytes[29] & 0x3f) << 8));
        }

        throw new InvalidDataException("Unsupported illustrative image format.");
    }
}
