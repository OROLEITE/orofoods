using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public enum WmcCustomerImportDisposition
{
    Create,
    Update,
    Existing,
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

public sealed record WmcCustomerRegularizationRow(string WmcCode, string SellerCode, bool Eligible, string? Reason = null);

public sealed record WmcCustomerRegularizationPreview(
    int PendingCustomers,
    int EligibleCustomers,
    int BlockedCustomers,
    IReadOnlyList<WmcCustomerRegularizationRow> Rows,
    string? ErrorMessage = null);

public sealed record WmcCustomerRegularizationResult(int Updated, int Blocked, string? ErrorMessage = null);

public sealed class WmcCustomerImportService(
    ApplicationDbContext db,
    IWmcCustomerReader customerReader,
    ILogger<WmcCustomerImportService> logger,
    TimeProvider timeProvider,
    IOptions<WmcCustomerImportOptions>? importOptions = null)
{
    private readonly WmcCustomerImportOptions options = importOptions?.Value ?? new();
    private readonly bool readinessConfigured = importOptions is not null;

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
            var candidates = rows.Where(IsEligibleStatusAndRestriction).ToList();
            var customers = await db.Customers.AsNoTracking().ToListAsync(cancellationToken);
            var readiness = await ResolveCommercialReadinessAsync(sellerCode, cancellationToken);
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

            var previewRows = rows
                .Select(row =>
                {
                    if (!IsEligibleStatusAndRestriction(row))
                        return ClassifyExcluded(row);

                    var disposition = Classify(row, byCode, byCnpj, duplicateCodes, duplicateCnpjs);
                    if (!readiness.IsReady)
                    {
                        return disposition with
                        {
                            Disposition = WmcCustomerImportDisposition.Conflict,
                            Reason = readiness.Reason
                        };
                    }

                    var existing = byCode.GetValueOrDefault(NormalizeCode(row.CodCliente))?.SingleOrDefault()
                        ?? byCnpj.GetValueOrDefault(NormalizeCnpj(row.Cnpj))?.SingleOrDefault();
                    if (existing is not null && existing.Status == CustomerStatus.Pending
                        && existing.SalesRepresentativeId is int existingSellerId
                        && existingSellerId != readiness.SellerId)
                    {
                        return disposition with
                        {
                            Disposition = WmcCustomerImportDisposition.Conflict,
                            Reason = "Cliente pendente já possui outro vendedor comercial configurado."
                        };
                    }

                    return disposition;
                })
                .ToList();

            return new WmcCustomerImportPreview(
                sellerCode,
                rows.Count,
                previewRows.Count(row => row.Disposition == WmcCustomerImportDisposition.Create),
                previewRows.Count(row => row.Disposition is WmcCustomerImportDisposition.Update or WmcCustomerImportDisposition.Existing),
                previewRows.Count(row => row.Disposition == WmcCustomerImportDisposition.Ignore),
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

        var readiness = await ResolveCommercialReadinessAsync(preview.SellerCode, cancellationToken);
        if (!readiness.IsReady)
            return new(preview.RecordsRead, 0, 0, preview.Ignored, preview.Conflicts + preview.Rows.Count, readiness.Reason);

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
                    Status = CustomerStatus.Approved,
                    IsActive = true,
                    ApprovedAt = timeProvider.GetUtcNow().UtcDateTime,
                    SalesRepresentativeId = readiness.SellerId == 0 ? null : readiness.SellerId,
                    PriceTableId = readiness.PriceTableId == 0 ? null : readiness.PriceTableId
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
            if (existing.Status == CustomerStatus.Pending)
            {
                if (existing.SalesRepresentativeId is null && readiness.SellerId > 0)
                {
                    existing.SalesRepresentativeId = readiness.SellerId;
                    changed = true;
                }
                if (existing.PriceTableId is null && readiness.PriceTableId > 0)
                {
                    existing.PriceTableId = readiness.PriceTableId;
                    changed = true;
                }
                existing.Status = CustomerStatus.Approved;
                existing.IsActive = true;
                existing.ApprovedAt ??= timeProvider.GetUtcNow().UtcDateTime;
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

    public async Task<WmcCustomerRegularizationPreview> PreviewPendingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = await db.Customers.AsNoTracking()
                .Where(customer => customer.Status == CustomerStatus.Pending && customer.WmcCode != null)
                .ToListAsync(cancellationToken);
            var sourceRows = await customerReader.GetAllAsync(cancellationToken);
            var rows = new List<WmcCustomerRegularizationRow>();
            foreach (var customer in pending)
            {
                var source = sourceRows.FirstOrDefault(row => string.Equals(NormalizeCode(row.CodCliente), NormalizeCode(customer.WmcCode), StringComparison.OrdinalIgnoreCase));
                if (source is null)
                {
                    rows.Add(new(customer.WmcCode!, "", false, "Cliente não localizado no espelho WMC."));
                    continue;
                }
                if (!IsEligible(source))
                {
                    rows.Add(new(customer.WmcCode!, NormalizeCode(source.CodVendedor), false, "Cliente não atende SITUACAO='A', RESTRICAO='F' e CNPJ válido."));
                    continue;
                }
                var readiness = await ResolveCommercialReadinessAsync(NormalizeCode(source.CodVendedor), cancellationToken);
                rows.Add(new(customer.WmcCode!, NormalizeCode(source.CodVendedor), readiness.IsReady, readiness.Reason));
            }
            return new(pending.Count, rows.Count(row => row.Eligible), rows.Count(row => !row.Eligible), rows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao gerar prévia de regularização de clientes WMC pendentes.");
            return new(0, 0, 0, [], "Não foi possível gerar a prévia de regularização.");
        }
    }

    public async Task<WmcCustomerRegularizationResult> RegularizePendingAsync(bool confirm, CancellationToken cancellationToken = default)
    {
        if (!confirm)
            return new(0, 0, "Confirme explicitamente a regularização após revisar a prévia.");
        var preview = await PreviewPendingAsync(cancellationToken);
        if (preview.ErrorMessage is not null || preview.BlockedCustomers > 0)
            return new(0, preview.BlockedCustomers, preview.ErrorMessage ?? "A regularização foi bloqueada para os clientes sem vendedor ou tabela válidos.");

        var sourceRows = await customerReader.GetAllAsync(cancellationToken);
        var pending = await db.Customers.Where(customer => customer.Status == CustomerStatus.Pending && customer.WmcCode != null).ToListAsync(cancellationToken);
        var updated = 0;
        foreach (var customer in pending)
        {
            var source = sourceRows.First(row => string.Equals(NormalizeCode(row.CodCliente), NormalizeCode(customer.WmcCode), StringComparison.OrdinalIgnoreCase));
            var readiness = await ResolveCommercialReadinessAsync(NormalizeCode(source.CodVendedor), cancellationToken);
            if (!readiness.IsReady || !IsEligible(source)) continue;
            if (customer.SalesRepresentativeId is null) customer.SalesRepresentativeId = readiness.SellerId;
            if (customer.PriceTableId is null) customer.PriceTableId = readiness.PriceTableId;
            customer.Status = CustomerStatus.Approved;
            customer.IsActive = true;
            customer.ApprovedAt ??= timeProvider.GetUtcNow().UtcDateTime;
            updated++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return new(updated, 0);
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
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(row.CodCliente)) missing.Add("CODCLIENTE");
            if (string.IsNullOrWhiteSpace(row.Nome)) missing.Add("NOME");
            if (string.IsNullOrWhiteSpace(row.Fantasia)) missing.Add("FANTASIA");
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Ignore, $"Campo ausente: {string.Join(", ", missing)}.");
        }
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
                    ? WmcCustomerImportDisposition.Existing
                    : WmcCustomerImportDisposition.Update);
        }
        return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Create);
    }

    private async Task<CommercialReadiness> ResolveCommercialReadinessAsync(string sellerCode, CancellationToken cancellationToken)
    {
        if (!readinessConfigured)
            return new(true, null);

        var sellers = await db.SalesRepresentatives.AsNoTracking()
            .Where(item => item.WmcCode != null)
            .ToListAsync(cancellationToken);
        var matches = sellers.Where(item => string.Equals(NormalizeCode(item.WmcCode), sellerCode, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
            return CommercialReadiness.Blocked($"Nenhum vendedor ativo está configurado com WmcCode '{sellerCode}'. Cadastre esse código no Admin antes de importar.");
        if (matches.Count > 1)
            return CommercialReadiness.Blocked($"Mais de um vendedor está configurado com WmcCode '{sellerCode}'. Corrija a duplicidade no Admin antes de importar.");
        if (!matches[0].IsActive)
            return CommercialReadiness.Blocked("O vendedor WMC configurado está inativo.");

        if (string.IsNullOrWhiteSpace(options.PriceTableName))
            return CommercialReadiness.Blocked("A identificação da TABELA BIMBO não está configurada neste ambiente.");

        var tables = await db.PriceTables.AsNoTracking().ToListAsync(cancellationToken);
        var tableMatches = tables.Where(item => string.Equals(NormalizeTableName(item.Name), NormalizeTableName(options.PriceTableName), StringComparison.Ordinal)).ToList();
        if (tableMatches.Count != 1)
            return CommercialReadiness.Blocked("A TABELA BIMBO não existe ou está ambígua neste ambiente.");
        if (!tableMatches[0].IsActive)
            return CommercialReadiness.Blocked("A TABELA BIMBO configurada está inativa.");
        return new(true, null, matches[0].Id, tableMatches[0].Id);
    }

    private static string NormalizeTableName(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private sealed record CommercialReadiness(bool IsReady, string? Reason, int SellerId = 0, int PriceTableId = 0)
    {
        public static CommercialReadiness Blocked(string reason) => new(false, reason);
    }

    private static bool IsEligible(WmcCustomerRecord row) =>
        IsEligibleStatusAndRestriction(row)
        && IsValidCnpj(row.Cnpj)
        && !string.IsNullOrWhiteSpace(row.CodCliente)
        && !string.IsNullOrWhiteSpace(row.Nome)
        && !string.IsNullOrWhiteSpace(row.Fantasia);

    private static bool IsEligibleStatusAndRestriction(WmcCustomerRecord row) =>
        string.Equals(row.Situacao, "A", StringComparison.OrdinalIgnoreCase)
        && string.Equals(row.Restricao, "F", StringComparison.OrdinalIgnoreCase);

    private static WmcCustomerImportPreviewRow ClassifyExcluded(WmcCustomerRecord row)
    {
        if (!string.Equals(row.Situacao, "A", StringComparison.OrdinalIgnoreCase))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Ignore, "SITUACAO diferente de A.");
        if (!string.Equals(row.Restricao, "F", StringComparison.OrdinalIgnoreCase))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Ignore, "RESTRICAO diferente de F.");
        if (!IsValidCnpj(row.Cnpj))
            return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Ignore, "CNPJ invalido ou ausente.");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(row.CodCliente)) missing.Add("CODCLIENTE");
        if (string.IsNullOrWhiteSpace(row.Nome)) missing.Add("NOME");
        if (string.IsNullOrWhiteSpace(row.Fantasia)) missing.Add("FANTASIA");
        return new(row.CodCliente, row.Nome, row.Fantasia, row.Cnpj, WmcCustomerImportDisposition.Ignore,
            missing.Count > 0 ? $"Campo ausente: {string.Join(", ", missing)}." : "Dados fora dos limites do cadastro Orofoods.");
    }

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
