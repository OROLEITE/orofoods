using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Tests.Services;

public sealed class CnpjValidatorTests
{
    [Theory]
    [InlineData("03.932.292/0001-99", "03932292000199")]
    [InlineData("03932292000199", "03932292000199")]
    [InlineData("00.000.000/E08G-12", "00000000E08G12")]
    [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
    public void Normalizes_valid_numeric_and_alphanumeric_cnpj(string input, string expected)
    {
        Assert.True(CnpjValidator.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("03.932.292/0001-98")]
    [InlineData("00.000.000/E08G-13")]
    [InlineData("12.ABC.345/01DE-34")]
    [InlineData("11.111.111/1111-11")]
    [InlineData("12.ABC.345/01D!-35")]
    [InlineData("12.ABC.345/01DE-3A")]
    [InlineData("1234567890123")]
    [InlineData("12.abc.345/01de-35-extra")]
    public void Rejects_invalid_check_digits_and_formats(string input)
    {
        Assert.False(CnpjValidator.TryNormalize(input, out _));
    }

    [Fact]
    public void Formats_normalized_alphanumeric_cnpj()
    {
        Assert.Equal("12.ABC.345/01DE-35", CnpjValidator.Format("12ABC34501DE35"));
    }
}
