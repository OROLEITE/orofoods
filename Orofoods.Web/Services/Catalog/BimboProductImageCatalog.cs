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
                "/images/products/bimbo-catalog/503768-tradicional-corte-duplo.webp",
                "https://www.demarchiparana.com.br/1401-thickbox_default/pao-supremo-gergelim-cd-pc15-cx12.webp",
                "https://www.bimboqsrbrasil.com.br/produto/pao-tradicional-corte-duplo/",
                "DeMarchi Parana, distribuidor Bimbo QSR",
                "Foto de pao redondo com gergelim e corte duplo; modelo confirmado na pagina oficial Bimbo QSR", 1.10m),
            ["503838"] = new(
                "Mini Brioche Corte Total",
                "/images/products/bimbo-illustrative/01-brioche-liso.webp",
                "Ilustração gerada a partir do conjunto de imagens ilustrativas autorizado pelo usuário",
                "https://www.bimboqsrbrasil.com.br/produto/mini-brioche-corte-total/",
                "Ilustração gerada para Orofoods",
                "Imagem ilustrativa de pão brioche; não representa escala ou SKU exatos", 1.15m, true, true),
            ["504342"] = new(
                "Classico Australiano",
                "/images/products/bimbo-catalog/504342-classico-australiano.webp",
                "https://loja.burgerfoods.com.br/29-large_default/pao-bqb-hb-classico-australiano-cong-bimbo.webp",
                "https://loja.burgerfoods.com.br/pao-de-hamburguer/pao-bqb-hb-classico-australiano-cong-bimbo-41",
                "Burger Foods, distribuidor Bimbo QSR",
                "Foto de pao BQB Classico Australiano identificada pelo distribuidor", 1.45m, true),
            ["503833"] = new(
                "Classico Brioche",
                "/images/products/bimbo-illustrative/06-brioche.png",
                "Ilustração gerada a partir do conjunto de imagens ilustrativas autorizado pelo usuário",
                "https://loja.burgerfoods.com.br/pao-de-hamburguer/pao-bqb-classico-brioche-ct-cong-bimbo-14",
                "Ilustração gerada para Orofoods",
                "Imagem ilustrativa de pão tipo brioche; não representa SKU exato", 1.15m, true, true),
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
                "Foto de pao BQB HB Smart sem gergelim identificada pelo distribuidor", 1.40m),
            ["503777"] = new(
                "Smart com gergelim",
                "/images/products/bimbo-illustrative/03-gergelim.png",
                "Ilustração gerada a partir do conjunto de imagens ilustrativas autorizado pelo usuário",
                "https://www.demarchiparana.com.br/pao-hamburger/pao-smart-gergelim-ct-pc15-cx18-863",
                "Ilustração gerada para Orofoods",
                "Imagem ilustrativa de pão com gergelim; não representa SKU exato", 1.05m, true, true),
            ["504338"] = new(
                "Max Brioche",
                "/images/products/bimbo-catalog/504338-max-brioche.png",
                "https://polinaalimentos.com.br/wdadmin/uploads/vitrine_produto/pao-max-brioche-202403121412.png",
                "https://polinaalimentos.com.br/produto/pao-max-brioche-23",
                "Polina Alimentos, distribuidor Bimbo QSR",
                "Foto Max Brioche identificada pelo distribuidor e compativel com a especificacao Bimbo QSR de 85 g", 0.90m),
            ["503775"] = new(
                "Tradicional com gergelim",
                "/images/products/bimbo-illustrative/03-gergelim.png",
                "Ilustração gerada a partir do conjunto de imagens ilustrativas autorizado pelo usuário",
                "https://www.focatto.com/pao-hamburger/pao-monaco-gergelim-ct-pc12-cx16-72",
                "Ilustração gerada para Orofoods",
                "Imagem ilustrativa de pão com gergelim; não representa SKU exato", 1.05m, true, true),
            ["503805"] = new(
                "Tradicional",
                "/images/products/bimbo-illustrative/04-tradicional.png",
                "Ilustração gerada a partir do conjunto de imagens ilustrativas autorizado pelo usuário",
                "https://www.focatto.com/pao-hamburger/pao-monaco-ct-pc12-cx16-50",
                "Ilustração gerada para Orofoods",
                "Imagem ilustrativa de pão tradicional liso; não representa SKU exato", 1.05m, true, true)
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
