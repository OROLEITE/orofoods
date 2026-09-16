using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcFileDropErpOrderIntegration(
    IOptions<WmcFileDropOptions> options,
    WmcOrderFileGenerator fileGenerator) : IErpOrderIntegration
{
    public async Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return new ErpOrderResult(false, Error: "Integração WMC por arquivo está desativada.");
        }

        if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
        {
            return new ErpOrderResult(false, Error: "Diretório de exportação WMC não configurado.");
        }

        var fileName = $"WMC_{SafeFileName(order.Number)}.txt";
        var finalPath = Path.Combine(settings.OutputDirectory, fileName);
        if (File.Exists(finalPath))
        {
            return new ErpOrderResult(true, ExternalOrderId: fileName);
        }

        var export = fileGenerator.Build(order);
        if (!export.Succeeded)
        {
            return new ErpOrderResult(false, Error: string.Join(" ", export.Errors));
        }

        Directory.CreateDirectory(settings.OutputDirectory);
        var temporaryPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, export.Content!, cancellationToken);
        File.Move(temporaryPath, finalPath, true);

        return new ErpOrderResult(true, ExternalOrderId: fileName);
    }

    private static string SafeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safe = new string(value.Where(character => !invalidCharacters.Contains(character) && char.IsLetterOrDigit(character)).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "pedido" : safe;
    }
}
