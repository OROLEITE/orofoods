using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Catalog;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class Gs1BarcodeAttribute(int length) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString())) return ValidationResult.Success;

        var barcode = value.ToString()!.Trim();
        if (barcode.Length != length || barcode.Any(character => character is < '0' or > '9'))
            return new ValidationResult($"O código deve conter exatamente {length} dígitos.");

        var sum = 0;
        var firstWeight = barcode.Length % 2 == 0 ? 3 : 1;
        for (var index = 0; index < barcode.Length - 1; index++)
        {
            var digit = barcode[index] - '0';
            sum += digit * (index % 2 == 0 ? firstWeight : firstWeight == 3 ? 1 : 3);
        }

        var checkDigit = (10 - sum % 10) % 10;
        return checkDigit == barcode[^1] - '0'
            ? ValidationResult.Success
            : new ValidationResult("O dígito verificador GS1 é inválido.");
    }
}
