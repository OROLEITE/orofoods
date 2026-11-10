using Microsoft.AspNetCore.Mvc;

namespace Orofoods.Web.Models.Validation;

public static class AdminModelBindingMessages
{
    private static readonly IReadOnlyDictionary<string, string> ProductFieldNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ProductCategoryId"] = "categoria",
        ["BasePrice"] = "preço base",
        ["PromotionalPrice"] = "preço promocional",
        ["UnitsPerCase"] = "unidades por caixa",
        ["MinimumCases"] = "mínimo de caixas",
        ["PackagesPerCase"] = "pacotes por caixa",
        ["SubstituteProductId"] = "produto substituto",
        ["Weight"] = "peso"
    };

    public static void Configure(MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var messages = options.ModelBindingMessageProvider;
        messages.SetAttemptedValueIsInvalidAccessor((value, field) =>
            $"O valor '{value}' não é válido para o campo {GetDisplayName(field)}.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value =>
            $"O valor '{value}' não é válido.");
        messages.SetValueMustBeANumberAccessor(field =>
            $"Informe um número válido para {GetDisplayName(field)}.");
        messages.SetValueMustNotBeNullAccessor(field =>
            $"Informe um valor para {GetDisplayName(field)}.");
        messages.SetMissingBindRequiredValueAccessor(field =>
            $"O campo {GetDisplayName(field)} é obrigatório.");
        messages.SetMissingKeyOrValueAccessor(() => "Informe um valor válido.");
    }

    private static string GetDisplayName(string field)
    {
        var propertyName = field.Split('.').Last();
        return ProductFieldNames.TryGetValue(propertyName, out var displayName) ? displayName : propertyName;
    }
}
