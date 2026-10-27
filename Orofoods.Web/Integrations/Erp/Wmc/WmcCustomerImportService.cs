using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public enum WmcCustomerImportDisposition
{
    Create,
    Update,
    Ignore,
    Conflict
}

public sealed record WmcCustomerImportPreviewRow(
    string WmcCode,
    string LegalName,
    string TradeName,
    string Cnpj,
    WmcCustomerImportDisposition Disposition,
    string? Reason = null);

public sealed record WmcCustomerImportPreview(
    string SellerCode,
    int RecordsRead,
    int NewCustomers,
    int ExistingCustomers,
    int Ignored,
    int Conflicts,
    IReadOnlyList<WmcCustomerImportPreviewRow> Rows,
    string? ErrorMessage = null);

public sealed record WmcCustomerImportResult(
    int RecordsRead,
    int Created,
    int Updated,
    int Ignored,
    int Conflicts,
    string? ErrorMessage = null);

public sealed class WmcCustomerImportService(
    ApplicationDbContext db,
    IWmcCustomerReader customerReader,
    ILogger<WmcCustomerImportService> logger,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<string>> GetSellerCodesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return (await customerReader.GetSellerCodesAsync(cancellationToken))
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(NormalizeCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao carregar códigos de vendedores WMC.");
            return [];
        }
    }

    public async Task<WmcCustomerImportPreview> PreviewAsync(string sellerCode, CancellationToken cancellationToken = default)
    {
        sellerCode = NormalizeCode(sellerCode);
        if (string.IsNullOrWhiteSpace(sellerCode))
            return EmptyPreview(sellerCode, "Informe o código do vendedor WMC.");

        try
        {
            var rows = await customerReader.GetBySellerAsync(sellerCode, cancellationToken);
            var candidates = rows
                .Where(row => string.Equals(row.Situacao, "A", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(row.Restricao, "F", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var customers = await db.Customers.AsNoTracking().ToListAsync(cancellationToken);
            var byCode = customers
                .Where(customer => !string.IsNullOrWhiteSpace(customer.WmcCode))
                .GroupBy(customer => NormalizeCode(customer.WmcCode!), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            var byCnpj = customers
                .Where(customer => IsValidCnpj(customer.Cnpj))
                .GroupBy(customer => NormalizeCnpj(customer.Cnpj), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var duplicateCodes = candidates
                .Where(row => !string.IsNullOrWhiteSpace(row.CodCliente))
                .GroupBy(row => NormalizeCode(row.CodCliente), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var duplicateCnpjs = candidates
                .Where(row => IsValidCnpj(row.Cnpj))
                .GroupBy(row => NormalizeCnpj(row.Cnpj), StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);

            var previewRows = new List<WmcCustomerImportPreviewRow>();
            foreach (var row in candidates)
            {
                var disposition = Classify(row, byCode, byCnpj, duplicateCodes, duplicateCnpjs);
                previewRows.Add(disposition);
            }

            return new WmcCustomerImportPreview(
                sellerCode,
                rows.Count,
                previewRows.Count(row => row.Disposition == WmcCustomerImportDisposition.Create),
                previewRows.Count(row => row.Disposition == WmcCustomerImportDisposition.Update || row.Disposition == WmcCustomerImportDisposition.Ignore),
                rows.Count - candidates.Count + previewRows.Count(row => row.Disposition == WmcCustomerImportDisposition.Ignore),
                previewRows.Count(row => row.Disposition == WmcCustomerImportDisposition.Conflict),
                previewRows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao gerar prévia de clientes WMC para o vendedor {SellerCode}.", sellerCode);
            return EmptyPreview(sellerCode, "Não foi possível consultar os clientes WMC.");
        }
    }

    public async Task<WmcCustomerImportResult> ImportAsync(string sellerCode, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(sellerCode, cancellationToken);
        if (preview.ErrorMessage is not null)
            return new(preview.RecordsRead, 0, 0, preview.Ignored, 0, preview.ErrorMessage);

        var run = new WmcSyncRun
        {
            StartedAt = timeProvider.GetUtcNow().UtcDateTime,
            InitialLoad = false,
            Status = preview.Conflicts > 0 ? "Failed" : "Running",
            FailedStage = preview.Conflicts > 0 ? "CustomersBySeller" : null,
            CustomersRead = preview.RecordsRead,
            ErrorMessage = preview.Conflicts > 0 ? $"{preview.Conflicts} conflito(s) cadastral(is)." : null
        };
        db.WmcSyncRuns.Add(run);

        if (preview.Conflicts > 0)
        {
            run.FinishedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            return new(preview.RecordsRead, 0, 0, preview.Ignored, preview.Conflicts, "A importação foi bloqueada por conflitos cadastrais.");
        }

        var sourceRows = await customerReader.GetBySellerAsync(preview.SellerCode, cancellationToken);
        var customers = await db.Customers.ToListAsync(cancellationToken);
        var byCode = customers.Where(customer => !string.IsNullOrWhiteSpace(customer.WmcCode))
            .ToDictionary(customer => NormalizeCode(customer.WmcCode!), StringComparer.OrdinalIgnoreCase);
        var byCnpj = customers.Where(customer => IsValidCnpj(customer.Cnpj))
            .ToDictionary(customer => NormalizeCnpj(customer.Cnpj), StringComparer.Ordinal);
        var created = 0;
        var updated = 0;
        var ignored = preview.Ignored;

        foreach (var row in sourceRows.Where(row => IsEligible(row)))
        {
            var cnpj = NormalizeCnpj(row.Cnpj);
            var code = NormalizeCode(row.CodCliente);
            var existing = byCode.GetValueOrDefault(code) ?? byCnpj.GetValueOrDefault(cnpj);
            if (existing is null)
            {
                var customer = new Customer
                {
                    WmcCode = code,
                    LegalName = row.Nome.Trim(),
                    TradeName = row.Fantasia.Trim(),
                    Cnpj = cnpj,
                    Status = CustomerStatus.Pending,
                    IsActive = false
                };
                db.Customers.Add(customer);
                byCode[code] = customer;
                byCnpj[cnpj] = customer;
                created++;
                continue;
            }

            var changed = false;
            if (!string.Equals(existing.LegalName, row.Nome.Trim(), StringComparison.Ordinal))
            {
                existing.LegalName = row.Nome.Trim();
                changed = true;
            }
            if (!string.Equals(existing.TradeName, row.Fantasia.Trim(), StringComparison.Ordinal))
            {
                existing.TradeName = row.Fantasia.Trim();
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(existing.WmcCode))
            {
                existing.WmcCode = code;
                changed = true;
            }
            if (changed) updated++; else ignored++;
        }

        run.Status = "Succeeded";
        run.FinishedAt = timeProvider.GetUtcNow().UtcDateTime;
        run.CustomersRead = preview.RecordsRead;
        await db.SaveChangesAsync(cancellationToken);
        return new(preview.RecordsRead, created, updated, ignored, 0);
    }

    private static WmcCustomerImportPreviewRow Classify(
        WmcCustomerRecord row,
        IReadOnlyDictionary<string, List<Customer>> byCode,
        IReadOnlyDictionary<string, List<Customer>> byCnpj,
        IReadOnlySet<string> duplicateCodes,
        IReadOnlySet<string> duplicateCnpjs)
    {
        if (!IsValidCnpj(row.Cnpj))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Ignore, "CNPJ inválido.");
        if (string.IsNullOrWhiteSpace(row.CodCliente) || string.IsNullOrWhiteSpace(row.Nome) || string.IsNullOrWhiteSpace(row.Fantasia))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Ignore, "Campo obrigatório ausente.");
        if (row.Nome.Length > 160 || row.Fantasia.Length > 120 || NormalizeCnpj(row.Cnpj).Length > 18)
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Ignore, "Campo excede o limite do cadastro Orofoods.");
        if (duplicateCodes.Contains(NormalizeCode(row.CodCliente)) || duplicateCnpjs.Contains(NormalizeCnpj(row.Cnpj)))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Conflict, "Registro duplicado na consulta WMC.");

        var codeMatches = byCode.GetValueOrDefault(NormalizeCode(row.CodCliente)) ?? [];
        var cnpjMatches = byCnpj.GetValueOrDefault(NormalizeCnpj(row.Cnpj)) ?? [];
        if (codeMatches.Count > 1 || cnpjMatches.Count > 1 || (codeMatches.Count == 1 && cnpjMatches.Count == 1 && codeMatches[0].Id != cnpjMatches[0].Id))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Conflict, "Código WMC e CNPJ apontam para cadastros diferentes.");
        if (codeMatches.Count == 1 && !string.Equals(NormalizeCnpj(codeMatches[0].Cnpj), NormalizeCnpj(row.Cnpj), StringComparison.Ordinal))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Conflict, "O código WMC já está associado a outro CNPJ.");
        if (cnpjMatches.Count == 1 && !string.IsNullOrWhiteSpace(cnpjMatches[0].WmcCode)
            && !string.Equals(NormalizeCode(cnpjMatches[0].WmcCode), NormalizeCode(row.CodCliente), StringComparison.OrdinalIgnoreCase))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Conflict, "O CNPJ já está associado a outro código WMC.");
        if (codeMatches.Count == 1 || cnpjMatches.Count == 1)
        {
            var customer = codeMatches.SingleOrDefault() ?? cnpjMatches.Single();
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj,
                string.Equals(customer.LegalName, row.Nome.Trim(), StringComparison.Ordinal)
                    && string.Equals(customer.TradeName, row.Fantasia.Trim(), StringComparison.Ordinal)
                    ? WmcCustomerImportDisposition.Ignore
                    : WmcCustomerImportDisposition.Update);
        }
        return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Create);
    }

    private static bool IsEligible(WmcCustomerRecord row) =>
        string.Equals(row.Situacao, "A", StringComparison.OrdinalIgnoreCase)
        && string.Equals(row.Restricao, "F", StringComparison.OrdinalIgnoreCase)
        && IsValidCnpj(row.Cnpj)
        && !string.IsNullOrWhiteSpace(row.CodCliente)
        && !string.IsNullOrWhiteSpace(row.Nome)
        && !string.IsNullOrWhiteSpace(row.Fantasia);

    private static WmcCustomerImportPreview EmptyPreview(string sellerCode, string error) =>
        new(sellerCode, 0, 0, 0, 0, 0, [], error);

    private static string NormalizeCode(string? value) => value?.Trim() ?? "";
    private static string NormalizeCnpj(string value) => new(value.Where(char.IsDigit).ToArray());

    private static bool IsValidCnpj(string? value)
    {
        var digits = NormalizeCnpj(value ?? "");
        if (digits.Length != 14 || digits.Distinct().Count() == 1) return false;
        var first = CheckDigit(digits[..12], [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = CheckDigit(digits[..12] + first, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return digits[12] - '0' == first && digits[13] - '0' == second;
    }

    private static int CheckDigit(string value, int[] weights)
    {
        var sum = value.Select((digit, index) => (digit - '0') * weights[index]).Sum();
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
