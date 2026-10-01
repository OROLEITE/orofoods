using Orofoods.Web.Services.Payments;
using System.Reflection;
using System.Text.Json;

namespace Orofoods.Web.Tests.Services;

public sealed class DisabledPointPaymentProviderTests
{
    [Fact]
    public async Task CreateTerminalPaymentAsync_ReturnsDisabledWithoutExternalCall()
    {
        var sut = new DisabledPointPaymentProvider();

        var result = await sut.CreateTerminalPaymentAsync(new PointPaymentRequest(
            OrderId: 10,
            PaymentId: 20,
            AssignmentId: 30,
            Amount: 125.50m,
            DeviceId: "device-test",
            StoreId: null,
            PosId: null,
            IdempotencyKey: "test-idempotency-key"));

        Assert.Equal(PointPaymentState.Disabled, result.State);
        Assert.Equal("POINT_INTEGRATION_DISABLED", result.ErrorCode);
        var constructorParameters = typeof(DisabledPointPaymentProvider).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(x => x.GetParameters())
            .ToArray();
        Assert.DoesNotContain(constructorParameters, x => x.ParameterType == typeof(HttpClient) || x.ParameterType == typeof(IHttpClientFactory));
    }

    [Fact]
    public async Task GetPaymentStatusAsync_ReturnsDisabledWithoutExternalCall()
    {
        var sut = new DisabledPointPaymentProvider();

        var result = await sut.GetPaymentStatusAsync("point-payment-test");

        Assert.Equal(PointPaymentState.Disabled, result.State);
        Assert.Null(result.PaymentStatus);
    }

    [Fact]
    public async Task CancelPendingPaymentAsync_ReturnsDisabledWithoutExternalCall()
    {
        var sut = new DisabledPointPaymentProvider();

        var result = await sut.CancelPendingPaymentAsync("point-payment-test");

        Assert.Equal(PointPaymentState.Disabled, result.State);
    }

    [Fact]
    public void DefaultConfiguration_DisablesCardOnDeliveryAndPoint()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Orofoods.Web", "appsettings.json")));
        var payments = document.RootElement.GetProperty("Payments");
        var options = new MercadoPagoPointOptions();

        Assert.False(payments.GetProperty("CardOnDeliveryEnabled").GetBoolean());
        Assert.False(payments.GetProperty("MercadoPagoPointEnabled").GetBoolean());
        Assert.False(options.Enabled);
    }
}
