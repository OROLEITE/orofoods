namespace Orofoods.Web.Services.Customers;

public static class CnpjValidator
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var input = value.Trim().ToUpperInvariant();
        string candidate;
        if (input.Length == 18)
        {
            if (input[2] != '.' || input[6] != '.' || input[10] != '/' || input[15] != '-')
            {
                return false;
            }

            candidate = input[..2] + input[3..6] + input[7..10] + input[11..15] + input[16..18];
        }
        else if (input.Length == 14)
        {
            candidate = input;
        }
        else
        {
            return false;
        }

        for (var index = 0; index < 12; index++)
        {
            var character = candidate[index];
            if (!char.IsAsciiDigit(character) && character is not (>= 'A' and <= 'Z'))
            {
                return false;
            }
        }

        if (!char.IsAsciiDigit(candidate[12]) || !char.IsAsciiDigit(candidate[13]))
        {
            return false;
        }

        if (candidate[..12].All(character => character == candidate[0]))
        {
            return false;
        }

        var firstDigit = CalculateCheckDigit(candidate.AsSpan(0, 12));
        var secondDigit = CalculateCheckDigit((candidate[..12] + (char)('0' + firstDigit)).AsSpan());
        if (candidate[12] - '0' != firstDigit || candidate[13] - '0' != secondDigit)
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static string Format(string normalized)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        if (normalized.Length != 14)
        {
            throw new ArgumentException("CNPJ normalizado deve conter 14 caracteres.", nameof(normalized));
        }

        return $"{normalized[..2]}.{normalized[2..5]}.{normalized[5..8]}/{normalized[8..12]}-{normalized[12..14]}";
    }

    private static int CalculateCheckDigit(ReadOnlySpan<char> value)
    {
        var sum = 0;
        var weight = 2;
        for (var index = value.Length - 1; index >= 0; index--)
        {
            sum += (value[index] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }

        var remainder = sum % 11;
        return remainder is 0 or 1 ? 0 : 11 - remainder;
    }
}
