using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcFileDropErpOrderIntegration(
    IOptions<WmcFileDropOptions> options,
    WmcOrderFileGenerator fileGenerator,
    WmcExportAuditService auditService) : IErpOrderIntegration
{
    public async Task<ErpOrderResult> SendOrderAsync(
        Order order,
        CancellationToken cancellationToken = default,
        Guid? attemptId = null)
    {
        if (order.Status != OrderStatus.Approved)
        {
            return new ErpOrderResult(false, Error: "Somente pedidos aprovados podem ser exportados para o WMC.");
        }

        var id = attemptId is null || attemptId == Guid.Empty ? Guid.NewGuid() : attemptId.Value;
        var fileName = $"WMC_{SafeFileName(order.Number)}.txt";
        DateTime? generatedAt = null;
        try
        {
            var settings = options.Value;
            if (!settings.Enabled)
            {
                return await FailAsync(order, id, fileName, generatedAt, "Integração WMC por arquivo está desativada.", cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
            {
                return await FailAsync(order, id, fileName, generatedAt, "Diretório de exportação WMC não configurado.", cancellationToken);
            }

            var export = fileGenerator.Build(order);
            if (!export.Succeeded)
            {
                return await FailAsync(order, id, fileName, generatedAt, string.Join(" ", export.Errors), cancellationToken);
            }

            generatedAt = DateTime.UtcNow;
            await auditService.RecordAttemptAsync(CreateAudit(order, id, fileName, WmcExportOutcome.Generated, generatedAt), cancellationToken);

            var outputDirectory = settings.OutputDirectory;
            var finalPath = Path.Combine(outputDirectory, fileName);
            Directory.CreateDirectory(outputDirectory);

            if (File.Exists(finalPath))
            {
                var existingContent = await File.ReadAllTextAsync(finalPath, cancellationToken);
                if (!string.Equals(existingContent, export.Content, StringComparison.Ordinal))
                {
                    return await FailAsync(order, id, fileName, generatedAt, "O arquivo WMC existente não corresponde ao conteúdo esperado para este pedido.", cancellationToken);
                }
            }
            else
            {
                var temporaryPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    await File.WriteAllTextAsync(temporaryPath, export.Content!, cancellationToken);
                    try
                    {
                        File.Move(temporaryPath, finalPath);
                    }
                    catch (IOException) when (File.Exists(finalPath))
                    {
                        var existingContent = await File.ReadAllTextAsync(finalPath, cancellationToken);
                        if (!string.Equals(existingContent, export.Content, StringComparison.Ordinal))
                        {
                            return await FailAsync(order, id, fileName, generatedAt, "O arquivo WMC existente não corresponde ao conteúdo esperado para este pedido.", cancellationToken);
                        }
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }

            await auditService.RecordAttemptAsync(CreateAudit(order, id, fileName, WmcExportOutcome.Available, generatedAt), cancellationToken);
            return new ErpOrderResult(true, ExternalOrderId: fileName);
        }
        catch (OperationCanceledException)
        {
            await TryRecordFailureAsync(order, id, fileName, generatedAt, "Tentativa cancelada antes da disponibilização do arquivo WMC.");
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var summary = exception switch
            {
                UnauthorizedAccessException => "Sem permissão para disponibilizar o arquivo WMC.",
                IOException => "Falha de leitura ou gravação do arquivo WMC.",
                _ => "Falha inesperada ao disponibilizar o arquivo WMC."
            };
            return await FailAsync(order, id, fileName, generatedAt, summary, cancellationToken);
        }
    }

    private Task<ErpOrderResult> FailAsync(
        Order order,
        Guid attemptId,
        string fileName,
        DateTime? generatedAt,
        string error,
        CancellationToken cancellationToken) =>
        RecordFailureAsync(order, attemptId, fileName, generatedAt, error, cancellationToken);

    private async Task<ErpOrderResult> RecordFailureAsync(
        Order order,
        Guid attemptId,
        string fileName,
        DateTime? generatedAt,
        string error,
        CancellationToken cancellationToken)
    {
        await auditService.RecordAttemptAsync(CreateAudit(order, attemptId, fileName, WmcExportOutcome.Failed, generatedAt, error), cancellationToken);
        return new ErpOrderResult(false, ExternalOrderId: fileName, Error: error);
    }

    private async Task TryRecordFailureAsync(Order order, Guid attemptId, string fileName, DateTime? generatedAt, string error)
    {
        try
        {
            await RecordFailureAsync(order, attemptId, fileName, generatedAt, error, CancellationToken.None);
        }
        catch
        {
            // Preserve the cancellation while keeping an already-recorded generated state when storage is unavailable.
        }
    }

    private static WmcExportAudit CreateAudit(
        Order order,
        Guid attemptId,
        string fileName,
        WmcExportOutcome outcome,
        DateTime? generatedAt,
        string? error = null) => new()
        {
            OrderId = order.Id,
            AttemptId = attemptId,
            Source = WmcExportSource.Automatic,
            Outcome = outcome,
            ExportedAt = DateTime.UtcNow,
            GeneratedAt = generatedAt,
            FileName = fileName,
            Succeeded = outcome is WmcExportOutcome.Generated or WmcExportOutcome.Available,
            Error = error
        };

    private static string SafeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safe = new string(value.Where(character => !invalidCharacters.Contains(character) && char.IsLetterOrDigit(character)).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "pedido" : safe;
    }
}
