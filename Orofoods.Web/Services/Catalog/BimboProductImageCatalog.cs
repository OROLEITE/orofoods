namespace Orofoods.Web.Services.Catalog;

public sealed record BimboProductImage(
    string ModelName,
    string ImageUrl,
    string SourceImageUrl,
    string ProductPageUrl,
    string SourceName,
    string ConfirmationStatus,
    decimal VisualScale = 1m,
    bool UseCatalogImage = false,
    bool IsIllustrative = false);

public static class BimboProductImageCatalog
{
    private static readonly IReadOnlyDictionary<string, BimboProductImage> ConfirmedImages =
        new Dictionary<string, BimboProductImage>(StringComparer.Ordinal)
        {
            ["503768"] = new(
                "Tradicional Corte Duplo",
                "/images/products/bimbo-catalog-transparent/503768_corte_duplo.png",
                "ProdutoTransparente/503768_corte_duplo.png",
                "https://www.bimboqsrbrasil.com.br/produto/pao-tradicional-corte-duplo/",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 503768", 1.00m, true, false),
            ["503838"] = new(
                "Mini Brioche Corte Total",
                "/images/products/bimbo-catalog-transparent/503838_mini_brioche.png",
                "ProdutoTransparente/503838_mini_brioche.png",
                "https://www.bimboqsrbrasil.com.br/produto/mini-brioche-corte-total/",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 503838", 1.00m, true, false),
            ["504342"] = new(
                "Classico Australiano",
                "/images/products/bimbo-catalog-transparent/504342_australiano.png",
                "ProdutoTransparente/504342_australiano.png",
                "https://loja.burgerfoods.com.br/pao-de-hamburguer/pao-bqb-hb-classico-australiano-cong-bimbo-41",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 504342", 1.00m, true, false),
            ["503833"] = new(
                "Classico Brioche",
                "/images/products/bimbo-catalog-transparent/503833_brioche_class.png",
                "ProdutoTransparente/503833_brioche_class.png",
                "https://loja.burgerfoods.com.br/pao-de-hamburguer/pao-bqb-classico-brioche-ct-cong-bimbo-14",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 503833", 1.00m, true, false),
            ["503811"] = new(
                "Hot Dog",
                "/images/products/bimbo-catalog-transparent/503811_hot_dog.png",
                "ProdutoTransparente/503811_hot_dog.png",
                "https://loja.burgerfoods.com.br/hot-dog/pao-bqb-hot-dog-cong-bimbo-1",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 503811", 1.00m, true, false),
            ["503813"] = new(
                "Smart",
                "/images/products/bimbo-catalog-transparent/503813_smart.png",
                "ProdutoTransparente/503813_smart.png",
                "https://www.k2distribuidora.com.br/inicio/pao-bqb-hb-smart-cxg-1",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 503813", 1.00m, true, false),
            ["503777"] = new(
                "Smart com gergelim",
                "/images/products/bimbo-catalog-transparent/503777_smart_gergelim.png",
                "ProdutoTransparente/503777_smart_gergelim.png",
                "https://www.demarchiparana.com.br/pao-hamburger/pao-smart-gergelim-ct-pc15-cx18-863",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 503777", 1.00m, true, false),
            ["504338"] = new(
                "Max Brioche",
                "/images/products/bimbo-catalog-transparent/504338_brioche_max.png",
                "ProdutoTransparente/504338_brioche_max.png",
                "https://polinaalimentos.com.br/produto/pao-max-brioche-23",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 504338", 1.00m, true, false),
            ["503775"] = new(
                "Tradicional com gergelim",
                "/images/products/bimbo-catalog-transparent/503775_tradicional_gergelim.png",
                "ProdutoTransparente/503775_tradicional_gergelim.png",
                "https://www.focatto.com/pao-hamburger/pao-monaco-gergelim-ct-pc12-cx16-72",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 503775", 1.00m, true, false),
            ["503805"] = new(
                "Tradicional",
                "/images/products/bimbo-catalog-transparent/503805_tradicional.png",
                "ProdutoTransparente/503805_tradicional.png",
                "https://www.focatto.com/pao-hamburger/pao-monaco-ct-pc12-cx16-50",
                "Arquivo PNG transparente fornecido pelo usuário",
                "Imagem fornecida e associada ao WMC 503805", 1.00m, true, false)
        };

    public static IReadOnlyDictionary<string, BimboProductImage> All => ConfirmedImages;

    public static BimboProductImage? Find(string? wmcCode)
    {
        var normalizedCode = wmcCode?.Trim();
        return normalizedCode is not null && ConfirmedImages.TryGetValue(normalizedCode, out var image)
            ? image
            : null;
    }

    public static string FormatVisualScale(decimal visualScale) =>
        visualScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
