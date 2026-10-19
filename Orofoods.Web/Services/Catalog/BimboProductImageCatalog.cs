namespace Orofoods.Web.Services.Catalog;

public sealed record BimboProductImage(
    string ModelName,
    string ImageUrl,
    string SourceImageUrl,
    string ProductPageUrl,
    string SourceName,
    string ConfirmationStatus,
    decimal VisualScale = 1m,
    bool UseCatalogImage = false);

public static class BimboProductImageCatalog
{
    private static readonly IReadOnlyDictionary<string, BimboProductImage> ConfirmedImages =
        new Dictionary<string, BimboProductImage>(StringComparer.Ordinal)
        {
            ["503768"] = new(
                "Tradicional Corte Duplo",
                "/images/products/bimbo-catalog/503768-tradicional-corte-duplo.webp",
                "https://www.demarchiparana.com.br/1401-thickbox_default/pao-supremo-gergelim-cd-pc15-cx12.webp",
                "https://www.bimboqsrbrasil.com.br/produto/pao-tradicional-corte-duplo/",
                "DeMarchi Parana, distribuidor Bimbo QSR",
                "Foto de pao redondo com gergelim e corte duplo; modelo confirmado na pagina oficial Bimbo QSR", 1.10m),
            ["503838"] = new(
                "Mini Brioche Corte Total",
                "/images/products/bimbo-catalog/503838-mini-brioche.jpg",
                "https://www.bimboqsrbrasil.com.br/wp-content/uploads/2018/06/pao_mini_brioche-1.jpg",
                "https://www.bimboqsrbrasil.com.br/produto/mini-brioche-corte-total/",
                "Bimbo QSR Brasil",
                "Foto de pao mini brioche, conferida visualmente na pagina oficial", 1.30m),
            ["504342"] = new(
                "Classico Australiano",
                "/images/products/bimbo-catalog/504342-classico-australiano.webp",
                "https://loja.burgerfoods.com.br/29-large_default/pao-bqb-hb-classico-australiano-cong-bimbo.webp",
                "https://loja.burgerfoods.com.br/pao-de-hamburguer/pao-bqb-hb-classico-australiano-cong-bimbo-41",
                "Burger Foods, distribuidor Bimbo QSR",
                "Foto de pao BQB Classico Australiano identificada pelo distribuidor", 1.30m, true),
            ["503833"] = new(
                "Classico Brioche",
                "/images/products/bimbo-catalog/503833-classico-brioche.webp",
                "https://loja.burgerfoods.com.br/41-thickbox_default/pao-bqb-classico-brioche-ct-cong-bimbo.webp",
                "https://loja.burgerfoods.com.br/pao-de-hamburguer/pao-bqb-classico-brioche-ct-cong-bimbo-14",
                "Burger Foods, distribuidor Bimbo QSR",
                "Foto de pao BQB Classico Brioche identificada pelo distribuidor", 1.65m),
            ["503811"] = new(
                "Hot Dog",
                "/images/products/bimbo-catalog/503811-hot-dog.webp",
                "https://loja.burgerfoods.com.br/104-thickbox_default/pao-bqb-hot-dog-cong-bimbo.webp",
                "https://loja.burgerfoods.com.br/hot-dog/pao-bqb-hot-dog-cong-bimbo-1",
                "Burger Foods, distribuidor Bimbo QSR",
                "Foto de pao BQB Hot Dog identificada pelo distribuidor", 1.30m),
            ["503813"] = new(
                "Smart",
                "/images/products/bimbo-catalog/503813-smart.webp",
                "https://www.k2distribuidora.com.br/43-large_default/pao-bqb-hb-smart-cxg.webp",
                "https://www.k2distribuidora.com.br/inicio/pao-bqb-hb-smart-cxg-1",
                "K2 Distribuidora, distribuidor Bimbo QSR",
                "Foto de pao BQB HB Smart sem gergelim identificada pelo distribuidor", 1.25m),
            ["503777"] = new(
                "Smart com gergelim",
                "/images/products/bimbo-catalog/503777-smart-gergelim.webp",
                "https://www.demarchiparana.com.br/1155-thickbox_default/pao-smart-gergelim-ct-pc15-cx18.webp",
                "https://www.demarchiparana.com.br/pao-hamburger/pao-smart-gergelim-ct-pc15-cx18-863",
                "DeMarchi Parana, distribuidor Bimbo QSR",
                "Foto de pao Smart com gergelim identificada pelo distribuidor", 1.00m),
            ["504338"] = new(
                "Max Brioche",
                "/images/products/bimbo-catalog/504338-max-brioche.png",
                "https://polinaalimentos.com.br/wdadmin/uploads/vitrine_produto/pao-max-brioche-202403121412.png",
                "https://polinaalimentos.com.br/produto/pao-max-brioche-23",
                "Polina Alimentos, distribuidor Bimbo QSR",
                "Foto Max Brioche identificada pelo distribuidor e compativel com a especificacao Bimbo QSR de 85 g", 0.90m),
            ["503775"] = new(
                "Tradicional com gergelim",
                "/images/products/bimbo-catalog/503775-tradicional-gergelim.webp",
                "https://www.focatto.com/1751-large_default/pao-monaco-gergelim-ct-pc12-cx16.webp",
                "https://www.focatto.com/pao-hamburger/pao-monaco-gergelim-ct-pc12-cx16-72",
                "Focatto, distribuidor Bimbo QSR",
                "Foto de pao Monaco com gergelim, 75 g e 11 cm; modelo tradicional com gergelim Bimbo QSR", 0.90m),
            ["503805"] = new(
                "Tradicional",
                "/images/products/bimbo-catalog/503805-tradicional.webp",
                "https://www.focatto.com/1747-large_default/pao-monaco-ct-pc12-cx16.jpg",
                "https://www.focatto.com/pao-hamburger/pao-monaco-ct-pc12-cx16-50",
                "Focatto, distribuidor Bimbo QSR",
                "Foto de pao Monaco sem gergelim, 75 g e 11 cm; modelo tradicional Bimbo QSR", 0.90m)
        };

    public static IReadOnlyDictionary<string, BimboProductImage> All => ConfirmedImages;

    public static BimboProductImage? Find(string? wmcCode)
    {
        var normalizedCode = wmcCode?.Trim();
        return normalizedCode is not null && ConfirmedImages.TryGetValue(normalizedCode, out var image)
            ? image
            : null;
    }
}
