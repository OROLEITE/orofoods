using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Pricing;

namespace Orofoods.Web.Services.Sellers;

public sealed class SellerOrderDraftService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    SellerCatalogService sellerCatalogService,
    PriceService priceService,
    IPaymentEligibilityService paymentEligibilityService)
{
    private const int MaximumNotesLength = 500;

    public Task<SellerOrderDraftOperationResult> CreateAsync(
        ClaimsPrincipal user,
        SellerOrderDraftInput input,
        CancellationToken cancellationToken = default) =>
        CreateAsync(user, input, Guid.NewGuid(), cancellationToken);

    public async Task<SellerOrderDraftOperationResult> CreateAsync(
        ClaimsPrincipal user,
        SellerOrderDraftInput input,
        Guid createRequestKey,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            return Failure(SellerOrderDraftErrorCodes.InvalidInput, "Os dados do rascunho são inválidos.");
        }

        var ownerUserId = GetUserId(user);
        if (ownerUserId is null || createRequestKey == Guid.Empty)
        {
            return Failure(SellerOrderDraftErrorCodes.InvalidInput, "A chave de criação do rascunho é inválida.");
        }

        var scope = await accessService.GetSellerCartScopeAsync(user, input.CustomerId, cancellationToken);
        if (scope?.SalesRepresentativeId is not int representativeId)
        {
            return Failure(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, "O cliente não pertence à carteira ativa deste vendedor.");
        }

        var existingId = await db.SellerOrderDrafts.AsNoTracking()
            .Where(x => x.OwnerUserId == ownerUserId && x.CreateRequestKey == createRequestKey)
            .Select(x => new { x.Id, x.CustomerId, x.SalesRepresentativeId })
            .SingleOrDefaultAsync(cancellationToken);
        if (existingId is not null)
        {
            if (existingId.CustomerId != input.CustomerId || existingId.SalesRepresentativeId != representativeId)
            {
                return Failure(SellerOrderDraftErrorCodes.CreateRequestKeyConflict,
                    "Esta tela de criação já foi usada. Reabra o rascunho existente ou inicie uma nova criação.");
            }

            var existing = await GetAsync(user, existingId.Id, cancellationToken);
            return existing is null
                ? Failure(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, "O cliente não pertence mais à carteira ativa deste vendedor.")
                : Success(existing);
        }

        var validation = await ValidateDraftFieldsAsync(
            user, input.CustomerId, input.DeliveryAddressId, input.PaymentTermId, input.Notes, input.Items, cancellationToken);
        if (validation.Error is not null)
        {
            return validation.Error;
        }

        var now = DateTime.UtcNow;
        var draft = new SellerOrderDraft
        {
            OwnerUserId = ownerUserId,
            SalesRepresentativeId = representativeId,
            CustomerId = input.CustomerId,
            CreateRequestKey = createRequestKey,
            DeliveryAddressId = input.DeliveryAddressId,
            PaymentTermId = input.PaymentTermId,
            RequestedDeliveryDate = input.RequestedDeliveryDate,
            Notes = input.Notes?.Trim() ?? "",
            Status = SellerOrderDraftStatus.Draft,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
            Items = input.Items.Select(item => new SellerOrderDraftItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                RequestedDiscountAmount = item.RequestedDiscountAmount
            }).ToList()
        };

        db.SellerOrderDrafts.Add(draft);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            DetachDraft(draft);
            var concurrentId = await db.SellerOrderDrafts.AsNoTracking()
                .Where(x => x.OwnerUserId == ownerUserId && x.CreateRequestKey == createRequestKey
                    && x.CustomerId == input.CustomerId && x.SalesRepresentativeId == representativeId)
                .Select(x => (int?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (concurrentId is null)
            {
                throw;
            }

            var existing = await GetAsync(user, concurrentId.Value, cancellationToken);
            return existing is null
                ? Failure(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, "O cliente não pertence mais à carteira ativa deste vendedor.")
                : Success(existing);
        }
        var createdView = await GetAsync(user, draft.Id, cancellationToken);
        return createdView is null
            ? Failure(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, "O acesso à carteira mudou durante a criação do rascunho.")
            : Success(createdView);
    }

    public async Task<SellerOrderDraftView?> GetAsync(
        ClaimsPrincipal user,
        int draftId,
        CancellationToken cancellationToken = default)
    {
        var ownerUserId = GetUserId(user);
        var isAdministrator = user.IsInRole(Models.Identity.ApplicationRoles.Administrator);
        if (ownerUserId is null || draftId <= 0)
        {
            return null;
        }

        var identity = await db.SellerOrderDrafts.AsNoTracking()
            .Where(x => x.Id == draftId && (isAdministrator || x.OwnerUserId == ownerUserId))
            .Select(x => new { x.CustomerId, x.SalesRepresentativeId })
            .SingleOrDefaultAsync(cancellationToken);
        if (identity is null)
        {
            return null;
        }

        var scope = await accessService.GetSellerCartScopeAsync(user, identity.CustomerId, cancellationToken);
        if (scope?.SalesRepresentativeId != identity.SalesRepresentativeId)
        {
            return null;
        }

        var draft = await db.SellerOrderDrafts.AsNoTracking()
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Customer)
            .Include(x => x.DeliveryAddress)
            .Include(x => x.PaymentTerm)
            .Include(x => x.SubmittedOrder)
            .SingleOrDefaultAsync(x => x.Id == draftId
                && (isAdministrator || x.OwnerUserId == ownerUserId)
                && x.SalesRepresentativeId == identity.SalesRepresentativeId, cancellationToken);
        return draft is null ? null : await ToViewAsync(draft, cancellationToken);
    }

    public async Task<IReadOnlyList<SellerOrderDraftView>> ListAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var ownerUserId = GetUserId(user);
        var isAdministrator = user.IsInRole(Models.Identity.ApplicationRoles.Administrator);
        var representative = await accessService.GetActiveRepresentativeAsync(user, cancellationToken);
        if (ownerUserId is null || representative is null)
        {
            return [];
        }

        var drafts = await db.SellerOrderDrafts.AsNoTracking()
            .Where(x => (isAdministrator || x.OwnerUserId == ownerUserId) &&
                x.SalesRepresentativeId == representative.Id &&
                x.Customer != null && x.Customer.IsActive &&
                x.Customer.Status == Models.Customers.CustomerStatus.Approved &&
                x.Customer.SalesRepresentativeId == representative.Id)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Customer)
            .Include(x => x.DeliveryAddress)
            .Include(x => x.PaymentTerm)
            .Include(x => x.SubmittedOrder)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<SellerOrderDraftView>(drafts.Count);
        foreach (var draft in drafts)
        {
            result.Add(await ToViewAsync(draft, cancellationToken));
        }

        return result;
    }

    public async Task<SellerOrderDraftOperationResult> SaveAsync(
        ClaimsPrincipal user,
        int draftId,
        SellerOrderDraftSaveInput input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            return Failure(SellerOrderDraftErrorCodes.InvalidInput, "Os dados do rascunho são inválidos.");
        }

        var loaded = await LoadOwnedDraftAsync(user, draftId, cancellationToken);
        if (loaded.Error is not null)
        {
            return loaded.Error;
        }

        var draft = loaded.Draft!;
        if (draft.Status != SellerOrderDraftStatus.Draft)
        {
            return Failure(SellerOrderDraftErrorCodes.AlreadySubmitted, "O rascunho enviado não pode mais ser editado.");
        }

        if (input.ExpectedVersion != draft.Version)
        {
            return Failure(SellerOrderDraftErrorCodes.ConcurrencyConflict, "O rascunho foi alterado em outra sessão. Reabra-o antes de salvar.");
        }

        if (input.UpsertItems is null)
        {
            return Failure(SellerOrderDraftErrorCodes.InvalidInput, "A lista de produtos do rascunho é inválida.");
        }

        var removedProductIds = input.RemovedProductIds ?? [];
        if (removedProductIds.Any(id => id <= 0) || removedProductIds.Distinct().Count() != removedProductIds.Count
            || removedProductIds.Intersect(input.UpsertItems.Select(item => item.ProductId)).Any())
        {
            return Failure(SellerOrderDraftErrorCodes.InvalidInput,
                "A lista de produtos para atualização e remoção é inválida.");
        }

        var validation = await ValidateDraftFieldsAsync(
            user, draft.CustomerId, input.DeliveryAddressId, input.PaymentTermId, input.Notes, input.UpsertItems, cancellationToken);
        if (validation.Error is not null)
        {
            return validation.Error;
        }

        var currentItems = draft.Items.ToDictionary(x => x.ProductId);
        if (removedProductIds.Any(productId => !currentItems.ContainsKey(productId)))
        {
            return Failure(SellerOrderDraftErrorCodes.ItemNotFound, "Um produto marcado para remoção não existe neste rascunho.");
        }

        foreach (var item in input.UpsertItems)
        {
            if (currentItems.TryGetValue(item.ProductId, out var existing))
            {
                existing.Quantity = item.Quantity;
                existing.RequestedDiscountAmount = item.RequestedDiscountAmount;
            }
            else
            {
                draft.Items.Add(new SellerOrderDraftItem
                {
                    SellerOrderDraftId = draft.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    RequestedDiscountAmount = item.RequestedDiscountAmount
                });
            }
        }

        foreach (var productId in removedProductIds)
        {
            var item = currentItems[productId];
            draft.Items.Remove(item);
            db.SellerOrderDraftItems.Remove(item);
        }

        draft.DeliveryAddressId = input.DeliveryAddressId;
        draft.PaymentTermId = input.PaymentTermId;
        if (input.RequestedDeliveryDate.HasValue)
        {
            draft.RequestedDeliveryDate = input.RequestedDeliveryDate;
        }
        draft.Notes = input.Notes?.Trim() ?? "";
        draft.Version++;
        draft.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            DetachDraft(draft);
            return Failure(SellerOrderDraftErrorCodes.ConcurrencyConflict, "O rascunho foi alterado em outra sessão. Reabra-o antes de salvar.");
        }

        var savedView = await GetAsync(user, draft.Id, cancellationToken);
        return savedView is null
            ? Failure(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, "O acesso à carteira mudou durante a atualização do rascunho.")
            : Success(savedView);
    }

    public async Task<SellerOrderDraftOperationResult> RemoveItemAsync(
        ClaimsPrincipal user,
        int draftId,
        int productId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadOwnedDraftAsync(user, draftId, cancellationToken);
        if (loaded.Error is not null)
        {
            return loaded.Error;
        }

        var draft = loaded.Draft!;
        if (draft.Status != SellerOrderDraftStatus.Draft)
        {
            return Failure(SellerOrderDraftErrorCodes.AlreadySubmitted, "O rascunho enviado não pode mais ser editado.");
        }

        if (expectedVersion != draft.Version)
        {
            return Failure(SellerOrderDraftErrorCodes.ConcurrencyConflict, "O rascunho foi alterado em outra sessão. Reabra-o antes de salvar.");
        }

        var item = draft.Items.SingleOrDefault(x => x.ProductId == productId);
        if (item is null)
        {
            return Failure(SellerOrderDraftErrorCodes.ItemNotFound, "O produto não existe neste rascunho.");
        }

        draft.Items.Remove(item);
        db.SellerOrderDraftItems.Remove(item);
        draft.Version++;
        draft.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            DetachDraft(draft);
            return Failure(SellerOrderDraftErrorCodes.ConcurrencyConflict, "O rascunho foi alterado em outra sessão. Reabra-o antes de salvar.");
        }

        var savedView = await GetAsync(user, draft.Id, cancellationToken);
        return savedView is null
            ? Failure(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, "O acesso à carteira mudou durante a atualização do rascunho.")
            : Success(savedView);
    }

    private async Task<(SellerOrderDraft? Draft, SellerOrderDraftOperationResult? Error)> LoadOwnedDraftAsync(
        ClaimsPrincipal user,
        int draftId,
        CancellationToken cancellationToken)
    {
        var ownerUserId = GetUserId(user);
        var isAdministrator = user.IsInRole(Models.Identity.ApplicationRoles.Administrator);
        if (ownerUserId is null || draftId <= 0)
        {
            return (null, Failure(SellerOrderDraftErrorCodes.DraftNotFound, "Rascunho não encontrado."));
        }

        var identity = await db.SellerOrderDrafts.AsNoTracking()
            .Where(x => x.Id == draftId && (isAdministrator || x.OwnerUserId == ownerUserId))
            .Select(x => new { x.CustomerId, x.SalesRepresentativeId })
            .SingleOrDefaultAsync(cancellationToken);
        if (identity is null)
        {
            return (null, Failure(SellerOrderDraftErrorCodes.DraftNotFound, "Rascunho não encontrado."));
        }

        var scope = await accessService.GetSellerCartScopeAsync(user, identity.CustomerId, cancellationToken);
        if (scope?.SalesRepresentativeId != identity.SalesRepresentativeId)
        {
            return (null, Failure(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, "O cliente não pertence à carteira ativa deste vendedor."));
        }

        var draft = await db.SellerOrderDrafts
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == draftId
                && (isAdministrator || x.OwnerUserId == ownerUserId)
                && x.SalesRepresentativeId == identity.SalesRepresentativeId,
                cancellationToken);
        return draft is null
            ? (null, Failure(SellerOrderDraftErrorCodes.DraftNotFound, "Rascunho não encontrado."))
            : (draft, null);
    }

    private async Task<(bool IsValid, SellerOrderDraftOperationResult? Error)> ValidateDraftFieldsAsync(
        ClaimsPrincipal user,
        int customerId,
        int? deliveryAddressId,
        int? paymentTermId,
        string? notes,
        IReadOnlyList<SellerOrderDraftItemInput> items,
        CancellationToken cancellationToken)
    {
        if (notes?.Length > MaximumNotesLength)
        {
            return (false, Failure(SellerOrderDraftErrorCodes.InvalidInput, $"As observações podem ter no máximo {MaximumNotesLength} caracteres."));
        }

        if (items is null)
        {
            return (false, Failure(SellerOrderDraftErrorCodes.InvalidInput, "A lista de produtos é inválida."));
        }

        if (items.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
        {
            return (false, Failure(SellerOrderDraftErrorCodes.InvalidInput, "Produtos e quantidades inválidos não são permitidos."));
        }

        if (items.Any(x => x.RequestedDiscountAmount < 0))
        {
            return (false, Failure(SellerOrderDraftErrorCodes.InvalidDiscount, "O desconto solicitado não pode ser negativo."));
        }

        if (items.Any(x => x.RequestedDiscountAmount > 0))
        {
            return (false, Failure(SellerOrderDraftErrorCodes.DiscountPolicyNotConfigured,
                "Não há política de desconto autorizada configurada para vendedores; nenhum desconto foi aplicado."));
        }

        if (deliveryAddressId.HasValue)
        {
            var addressIsValid = await db.CustomerAddresses.AsNoTracking().AnyAsync(x =>
                x.Id == deliveryAddressId.Value && x.CustomerId == customerId && x.IsActive,
                cancellationToken);
            if (!addressIsValid)
            {
                return (false, Failure(SellerOrderDraftErrorCodes.InvalidAddress, "Selecione um endereço ativo cadastrado para este cliente."));
            }
        }

        if (paymentTermId.HasValue)
        {
            var paymentEligibility = await paymentEligibilityService.ValidateAsync(customerId, paymentTermId.Value, cancellationToken);
            if (!paymentEligibility.IsAllowed)
            {
                return (false, Failure(SellerOrderDraftErrorCodes.PaymentTermNotAllowed,
                    paymentEligibility.ErrorMessage ?? "Esta condição de pagamento não está disponível para o cliente."));
            }
        }

        if (items.Count > 0)
        {
            var selectableProductIds = await sellerCatalogService.GetSelectableProductIdsAsync(
                user, customerId, items.Select(x => x.ProductId), cancellationToken);
            if (selectableProductIds is null || items.Any(item => !selectableProductIds.Contains(item.ProductId)))
            {
                return (false, Failure(SellerOrderDraftErrorCodes.ProductNotSelectable,
                    "Um ou mais produtos não estão disponíveis no catálogo atual deste vendedor."));
            }

            var wmcCodesByProductId = await db.Products.AsNoTracking()
                .Where(product => items.Select(item => item.ProductId).Contains(product.Id))
                .Select(product => new { product.Id, product.WmcCode })
                .ToDictionaryAsync(product => product.Id, product => product.WmcCode, cancellationToken);
            var duplicateWmcCode = items
                .Select(item => wmcCodesByProductId.GetValueOrDefault(item.ProductId)?.Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicateWmcCode is not null)
            {
                return (false, Failure(SellerOrderDraftErrorCodes.InvalidInput,
                    "Este item já existe no pedido!"));
            }

            var productMinimums = await db.Products.AsNoTracking()
                .Where(x => items.Select(item => item.ProductId).Contains(x.Id))
                .Select(x => new { x.Id, x.MinimumCases, x.CommercialUnitCode })
                .ToDictionaryAsync(x => x.Id, x => x.CommercialUnitCode == "PCT" ? 1 : x.MinimumCases, cancellationToken);
            if (items.Any(item => !productMinimums.TryGetValue(item.ProductId, out var minimum) || item.Quantity < minimum))
            {
                return (false, Failure(SellerOrderDraftErrorCodes.BelowMinimumQuantity,
                    "A quantidade de cada produto deve respeitar o mínimo de caixas cadastrado."));
            }
        }

        return (true, null);
    }

    private async Task<SellerOrderDraftView> ToViewAsync(SellerOrderDraft draft, CancellationToken cancellationToken)
    {
        var prices = await priceService.GetPricesAsync(draft.CustomerId, draft.Items.Select(x => x.ProductId), cancellationToken);
        return new SellerOrderDraftView(
            draft.Id,
            draft.Status,
            draft.OwnerUserId,
            draft.SalesRepresentativeId,
            draft.CustomerId,
            draft.Customer?.TradeName,
            draft.DeliveryAddressId,
            draft.DeliveryAddress?.Label,
            draft.PaymentTermId,
            draft.PaymentTerm?.Name,
            draft.RequestedDeliveryDate,
            draft.Notes,
            draft.Version,
            draft.CreatedAt,
            draft.UpdatedAt,
            draft.SubmittedAt,
            draft.SubmittedOrder?.Id,
            draft.SubmittedOrder?.Number,
            draft.Items.OrderBy(x => x.Id).Select(item => new SellerOrderDraftItemView(
                item.ProductId,
                item.Product?.Sku ?? "",
                item.Product?.Name ?? "",
                item.Product?.UnitDescription ?? "",
                item.Quantity,
                item.RequestedDiscountAmount,
                prices.GetValueOrDefault(item.ProductId, item.Product?.BasePrice ?? 0m),
                item.Product?.MinimumSalesQuantity ?? 1,
                item.Product?.WmcCode)).ToArray());
    }

    private void DetachDraft(SellerOrderDraft draft)
    {
        var entries = db.ChangeTracker.Entries()
            .Where(entry => ReferenceEquals(entry.Entity, draft) || draft.Items.Any(item => ReferenceEquals(item, entry.Entity)))
            .ToArray();
        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    private static string? GetUserId(ClaimsPrincipal user) =>
        (user.IsInRole(Models.Identity.ApplicationRoles.Seller)
            || user.IsInRole(Models.Identity.ApplicationRoles.Administrator))
            ? user.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

    private static SellerOrderDraftOperationResult Success(SellerOrderDraftView draft) => new(true, draft, null, null);

    private static SellerOrderDraftOperationResult Failure(string errorCode, string message) => new(false, null, errorCode, message);
}

public sealed record SellerOrderDraftInput(
    int CustomerId,
    int? DeliveryAddressId,
    int? PaymentTermId,
    DateTime? RequestedDeliveryDate,
    string? Notes,
    IReadOnlyList<SellerOrderDraftItemInput> Items);

public sealed record SellerOrderDraftSaveInput(
    int ExpectedVersion,
    int? DeliveryAddressId,
    int? PaymentTermId,
    DateTime? RequestedDeliveryDate,
    string? Notes,
    IReadOnlyList<SellerOrderDraftItemInput> UpsertItems,
    IReadOnlyList<int>? RemovedProductIds = null);

public sealed record SellerOrderDraftItemInput(int ProductId, int Quantity, decimal RequestedDiscountAmount = 0m);

public sealed record SellerOrderDraftOperationResult(
    bool Succeeded,
    SellerOrderDraftView? Draft,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record SellerOrderDraftView(
    int Id,
    SellerOrderDraftStatus Status,
    string OwnerUserId,
    int SalesRepresentativeId,
    int CustomerId,
    string? CustomerName,
    int? DeliveryAddressId,
    string? DeliveryAddressLabel,
    int? PaymentTermId,
    string? PaymentTermName,
    DateTime? RequestedDeliveryDate,
    string Notes,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? SubmittedAt,
    int? SubmittedOrderId,
    string? SubmittedOrderNumber,
    IReadOnlyList<SellerOrderDraftItemView> Items);

public sealed record SellerOrderDraftItemView(
    int ProductId,
    string Sku,
    string ProductName,
    string UnitDescription,
    int Quantity,
    decimal RequestedDiscountAmount,
    decimal ResolvedUnitPrice,
    int MinimumCases = 1,
    string? WmcCode = null);

public static class SellerOrderDraftErrorCodes
{
    public const string CustomerNotInPortfolio = "customer_not_in_portfolio";
    public const string DraftNotFound = "draft_not_found";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string AlreadySubmitted = "already_submitted";
    public const string DiscountPolicyNotConfigured = "discount_policy_not_configured";
    public const string InvalidDiscount = "invalid_discount";
    public const string InvalidInput = "invalid_input";
    public const string ProductNotSelectable = "product_not_selectable";
    public const string BelowMinimumQuantity = "below_minimum_quantity";
    public const string InvalidAddress = "invalid_address";
    public const string PaymentTermNotAllowed = "payment_term_not_allowed";
    public const string ItemNotFound = "item_not_found";
    public const string CreateRequestKeyConflict = "create_request_key_conflict";
    public const string InvalidOrder = "invalid_order";
    public const string CommercialQuoteInvalid = "commercial_quote_invalid";
    public const string CommercialQuoteChanged = "commercial_quote_changed";
    public const string SubmissionFailed = "submission_failed";
}
