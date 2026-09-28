using Orofoods.Web.Models.Payments;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.ViewModels;

public class PixPaymentViewModelTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.CreateCustomTimeZone(
        "America/Sao_Paulo-Test",
        TimeSpan.FromHours(-3),
        "America/Sao_Paulo-Test",
        "America/Sao_Paulo-Test");

    [Fact]
    public void Pending_pix_is_presented_with_brazilian_amount_and_local_expiration()
    {
        var payment = CreatePayment(PaymentStatus.Pending);

        var result = PixPaymentViewModel.FromPayment(payment, SaoPaulo);

        Assert.Equal(41, result.PaymentId);
        Assert.Equal(57, result.OrderId);
        Assert.Equal("Aguardando pagamento", result.StatusLabel);
        Assert.Contains("R$", result.FormattedAmount);
        Assert.Contains("119,90", result.FormattedAmount);
        Assert.Equal(new DateTime(2026, 9, 17, 17, 51, 0), result.ExpiresAtLocal);
        Assert.Equal("17/09/2026 às 17:51", result.FormattedExpiration);
        Assert.Equal("data:image/png;base64,base64-persistido", result.QrCodeDataUri);
    }

    [Theory]
    [InlineData(PaymentStatus.Approved, "Pagamento confirmado")]
    [InlineData(PaymentStatus.Paid, "Pagamento confirmado")]
    [InlineData(PaymentStatus.Rejected, "Pagamento não aprovado")]
    [InlineData(PaymentStatus.Expired, "PIX expirado")]
    public void Payment_status_uses_the_customer_facing_label(PaymentStatus status, string expected)
    {
        var result = PixPaymentViewModel.FromPayment(CreatePayment(status), SaoPaulo);

        Assert.Equal(expected, result.StatusLabel);
    }

    [Fact]
    public void Pix_without_qr_code_does_not_build_an_image_data_uri()
    {
        var payment = CreatePayment(PaymentStatus.Pending);
        payment.PixQrCodeBase64 = null;

        var result = PixPaymentViewModel.FromPayment(payment, SaoPaulo);

        Assert.False(result.HasQrCode);
        Assert.Null(result.QrCodeDataUri);
        Assert.True(result.HasCopyPaste);
    }

    private static Payment CreatePayment(PaymentStatus status) => new()
    {
        Id = 41,
        OrderId = 57,
        CustomerId = 3,
        Method = PaymentMethodType.Pix,
        Amount = 119.90m,
        Status = status,
        PixCopyPaste = "codigo-persistido",
        PixQrCodeBase64 = "base64-persistido",
        ExpiresAt = new DateTime(2026, 9, 17, 20, 51, 0, DateTimeKind.Utc)
    };
}
