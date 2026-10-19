namespace Orofoods.Web.Services.Catalog;

public sealed record OfficialBimboProductImage(
    string ModelName,
    string ImageUrl,
    string ProductPageUrl,
    string SourceName,
    string ConfirmationStatus);

public static class OfficialBimboProductImageCatalog
{
    private static readonly IReadOnlyDictionary<string, OfficialBimboProductImage> ConfirmedImages =
        new Dictionary<string, OfficialBimboProductImage>(StringComparer.Ordinal)
        {
            ["503768"] = new(
                "Tradicional Corte Duplo",
                "https://bbrwebsiapscp19.azurewebsites.net/wp-content/uploads/2017/06/FT-P%C3%A3o-Supremo-GCD_Rev12_503500010-503500083-503500023-1-300x167.jpg",
                "https://www.bimboqsrbrasil.com.br/produto/pao-tradicional-corte-duplo/",
                "Bimbo QSR Brasil",
                "Confirmado pela página oficial; arquivo usa nome técnico legado"),
            ["503838"] = new(
                "Mini Brioche Corte Total",
                "https://www.bimboqsrbrasil.com.br/wp-content/uploads/2018/06/pao_mini_brioche-1.jpg",
                "https://www.bimboqsrbrasil.com.br/produto/mini-brioche-corte-total/",
                "Bimbo QSR Brasil",
                "Confirmado pela página oficial"),
            ["504342"] = new(
                "Clássico Australiano",
                "https://loja.burgerfoods.com.br/29-large_default/pao-bqb-hb-classico-australiano-cong-bimbo.webp",
                "https://loja.burgerfoods.com.br/pao-de-hamburguer/pao-bqb-hb-classico-australiano-cong-bimbo-41",
                "Burger Foods",
                "Confirmado por distribuidor Bimbo QSR"),
            ["503833"] = new(
                "Clássico Brioche",
                "https://loja.burgerfoods.com.br/41-thickbox_default/pao-bqb-classico-brioche-ct-cong-bimbo.webp",
                "https://loja.burgerfoods.com.br/pao-de-hamburguer/pao-bqb-classico-brioche-ct-cong-bimbo-14",
                "Burger Foods",
                "Confirmado por distribuidor Bimbo QSR"),
            ["503811"] = new(
                "Hot Dog",
                "https://loja.burgerfoods.com.br/104-thickbox_default/pao-bqb-hot-dog-cong-bimbo.webp",
                "https://loja.burgerfoods.com.br/hot-dog/pao-bqb-hot-dog-cong-bimbo-1",
                "Burger Foods",
                "Confirmado por distribuidor Bimbo QSR"),
            ["503813"] = new(
                "Smart",
                "https://bbrwebsiapscp19.azurewebsites.net/wp-content/uploads/2017/05/FT-P%C3%A3o-Riviera-CT_Rev02_503500106-503500107-503500105-1-300x167.jpg",
                "https://www.bimboqsrbrasil.com.br/produto/pao-smart/",
                "Bimbo QSR Brasil",
                "Confirmado pela página oficial; arquivo usa nome técnico legado"),
            ["503777"] = new(
                "Smart com gergelim",
                "https://bbrwebsiapscp19.azurewebsites.net/wp-content/uploads/2017/05/FT-P%C3%A3o-Riviera-G-CT_Rev02_503500126-503500127-503500125-1-300x167.jpg",
                "https://www.bimboqsrbrasil.com.br/produto/pao-smart-gergelim/",
                "Bimbo QSR Brasil",
                "Confirmado pela página oficial; arquivo usa nome técnico legado"),
            ["504338"] = new(
                "Pão Max Brioche",
                "https://bbrwebsiapscp19.azurewebsites.net/wp-content/uploads/2017/10/FT-P%C3%A3o-Brioche-Max-CT_Rev02_503500247-503500245-2.jpg",
                "https://www.bimboqsrbrasil.com.br/produto/pao-max-brioche/",
                "Bimbo QSR Brasil",
                "Confirmado pela página oficial"),
            ["503775"] = new(
                "Tradicional com gergelim",
                "https://bbrwebsiapscp19.azurewebsites.net/wp-content/uploads/2017/05/FT-P%C3%A3o-M%C3%B4naco-G-CT_Rev02_503500116-503500115-1-300x167.jpg",
                "https://www.bimboqsrbrasil.com.br/produto/pao-tradicional-gergelim/",
                "Bimbo QSR Brasil",
                "Confirmado pela página oficial; arquivo usa nome técnico legado"),
            ["503805"] = new(
                "Tradicional",
                "https://bbrwebsiapscp19.azurewebsites.net/wp-content/uploads/2017/06/FT-P%C3%A3o-M%C3%B4naco-CT_Rev01_503500044-503500045-1-300x167.jpg",
                "https://www.bimboqsrbrasil.com.br/produto/pao-tradicional/",
                "Bimbo QSR Brasil",
                "Confirmado pela página oficial; arquivo usa nome técnico legado")
        };

    public static OfficialBimboProductImage? Find(string? wmcCode)
    {
        var normalizedCode = wmcCode?.Trim();
        return normalizedCode is not null && ConfirmedImages.TryGetValue(normalizedCode, out var image)
            ? image
            : null;
    }
}
