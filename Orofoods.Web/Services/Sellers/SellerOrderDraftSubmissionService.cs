using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Services.Sellers;

public sealed class SellerOrderDraftSubmissionService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    OrderPlacementService orderPlacementService,
    SellerOrderDraftQuoteProtector quoteProtector,
    ILogger<SellerOrderDraftSubmissionService> logger)
{
    private const string SellerDraftOrderUniqueIndex = "IX_Orders_SellerOrderDraftId";

    public async Task<SellerOrderDraftSubmissionResult> SubmitAsync(
        ClaimsPrincipal user,
        int draftId,
        int expectedVersion,
        string? quoteToken,
        CancellationToken cancellationToken = default)
    {
        var ownerUserId = user.IsInRole(ApplicationRoles.Seller) || user.IsInRole(ApplicationRoles.Administrator)
            ? user.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        var isAdministrator = user.IsInRole(ApplicationRoles.Administrator);
        if (string.IsNullOrWhiteSpace(ownerUserId) || draftId <= 0)
        {
            return Failure(SellerOrderDraftErrorCodes.DraftNotFound, "Rascunho não encontrado.");
        }

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var draft = await db.SellerOrderDrafts
                .Include(x => x.Items)
                .Include(x => x.SubmittedOrder)
                .SingleOrDefaultAsync(x => x.Id == draftId
                    && (isAdministrator || x.OwnerUserId == ownerUserId), cancellationToken);
            if (draft is null)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.DraftNotFound, "Rascunho não encontrado.");
            }

            var scope = await accessService.GetSellerCartScopeAsync(user, draft.CustomerId, cancellationToken);
            if (scope?.SalesRepresentativeId != draft.SalesRepresentativeId)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.CustomerNotInPortfolio,
                    "O cliente não pertence mais à carteira ativa deste vendedor.");
            }

            if (draft.Status == SellerOrderDraftStatus.Submitted)
            {
                if (!IsQuoteBoundToDraft(quoteToken, ownerUserId, draft, draft.Version - 1, out _))
                {
                    return await AbortAsync(SellerOrderDraftErrorCodes.CommercialQuoteInvalid,
                        "A cotação não corresponde mais a este rascunho. Reabra-o para consultar o pedido registrado.");
                }

                if (draft.SubmittedOrder is null)
                {
                    return await AbortAsync(SellerOrderDraftErrorCodes.SubmissionFailed,
                        "O rascunho consta como enviado, mas não possui pedido vinculado. Solicite suporte antes de tentar novamente.");
                }

                await transaction.CommitAsync(cancellationToken);
                return Success(draft, draft.SubmittedOrder, wasIdempotentReplay: true);
            }

            if (draft.Status != SellerOrderDraftStatus.Draft)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.InvalidOrder, "O estado do rascunho não permite envio.");
            }

            if (expectedVersion <= 0 || draft.Version != expectedVersion)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.ConcurrencyConflict,
                    "O rascunho foi alterado em outra sessão. Reabra-o para revisar os dados antes do envio.");
            }

            if (draft.DeliveryAddressId is null)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.InvalidOrder, "Selecione um endereço de entrega cadastrado.");
            }

            if (draft.PaymentTermId is null)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.InvalidOrder, "Selecione uma condição de pagamento autorizada.");
            }

            if (draft.Items.Count == 0)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.InvalidOrder, "Adicione ao menos um produto antes de enviar o pedido.");
            }

            if (draft.Items.Any(x => x.RequestedDiscountAmount < 0m))
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.InvalidDiscount, "O desconto solicitado não pode ser negativo.");
            }

            if (draft.Items.Any(x => x.RequestedDiscountAmount != 0m))
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.DiscountPolicyNotConfigured,
                    "Não há política de desconto autorizada para vendedores; nenhum desconto foi aplicado.");
            }

            if (draft.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.InvalidOrder,
                    "O rascunho contém produtos ou quantidades inválidas. Corrija-o antes do envio.");
            }

            var wmcCodesByProductId = await db.Products.AsNoTracking()
                .Where(product => draft.Items.Select(item => item.ProductId).Contains(product.Id))
                .Select(product => new { product.Id, product.WmcCode })
                .ToDictionaryAsync(product => product.Id, product => product.WmcCode, cancellationToken);
            var duplicateWmcCode = draft.Items
                .Select(item => wmcCodesByProductId.GetValueOrDefault(item.ProductId)?.Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicateWmcCode is not null)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.InvalidOrder,
                    "Este item já existe no pedido!");
            }

            if (!IsQuoteBoundToDraft(quoteToken, ownerUserId, draft, expectedVersion, out var protectedQuote)
                || protectedQuote is null)
            {
                return await AbortAsync(SellerOrderDraftErrorCodes.CommercialQuoteInvalid,
                    "A cotação não corresponde mais a este rascunho. Reabra-o, revise os valores atuais e tente novamente.");
            }

            var placement = await orderPlacementService.PlaceDraftLinesWithinTransactionAsync(
                draft.CustomerId,
                ownerUserId,
                new OrderPlacementCommand(
                    draft.DeliveryAddressId.Value,
                    draft.PaymentTermId.Value,
                    draft.RequestedDeliveryDate,
                    draft.Notes),
                draft.Id,
                draft.Items.Select(x => (x.ProductId, x.Quantity)).ToArray(),
                protectedQuote.Quote,
                cancellationToken);

            if (!placement.Succeeded || placement.Order is null)
            {
                if (placement.ErrorCode == OrderPlacementErrorCodes.CommercialQuoteChanged)
                {
                    return await AbortAsync(SellerOrderDraftErrorCodes.CommercialQuoteChanged,
                        placement.Errors.FirstOrDefault()
                            ?? "Os valores comerciais mudaram. Nenhum pedido foi enviado e o rascunho não foi alterado.");
                }

                return await AbortAsync(SellerOrderDraftErrorCodes.InvalidOrder,
                    placement.Errors.Count == 0
                        ? "Não foi possível validar o pedido. O rascunho continua editável."
                        : string.Join(" ", placement.Errors));
            }

            var now = DateTime.UtcNow;
            draft.SubmittedOrder = placement.Order;
            draft.Status = SellerOrderDraftStatus.Submitted;
            draft.SubmittedAt = now;
            draft.UpdatedAt = now;
            draft.Version++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Success(draft, placement.Order, wasIdempotentReplay: false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return await ResolveConflictAsync(exception);
        }
        catch (Exception exception) when (IsSubmissionUniquenessOrSerializationConflict(exception))
        {
            return await ResolveConflictAsync(exception);
        }
        catch (Exception exception)
        {
            await RollbackAndDisposeAsync(transaction, draftId);
            transaction = null;
            db.ChangeTracker.Clear();
            logger.LogError(exception, "Seller draft submission failed for draft {DraftId}.", draftId);
            return Failure(SellerOrderDraftErrorCodes.SubmissionFailed,
                "Não foi possível registrar o pedido. Nenhuma alteração foi confirmada; o rascunho continua editável.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        async Task<SellerOrderDraftSubmissionResult> AbortAsync(string errorCode, string message)
        {
            await RollbackAndDisposeAsync(transaction, draftId);
            transaction = null;
            db.ChangeTracker.Clear();
            return Failure(errorCode, message);
        }

        async Task<SellerOrderDraftSubmissionResult> ResolveConflictAsync(Exception exception)
        {
            var rolledBack = await RollbackAndDisposeAsync(transaction, draftId);
            transaction = null;
            db.ChangeTracker.Clear();
            if (!rolledBack)
            {
                logger.LogError(exception, "Could not roll back conflicting seller draft submission {DraftId}.", draftId);
                return Failure(SellerOrderDraftErrorCodes.SubmissionFailed,
                    "Não foi possível confirmar o resultado do envio. Reabra o rascunho antes de tentar novamente.");
            }

            var committed = await FindCommittedSubmissionAsync(user, ownerUserId, draftId, cancellationToken);
            return committed is not null
                ? Success(committed.Value.Draft, committed.Value.Order, wasIdempotentReplay: true)
                : Failure(SellerOrderDraftErrorCodes.ConcurrencyConflict,
                    "O rascunho foi enviado em outra sessão ou sofreu um conflito. Reabra-o para confirmar o estado.");
        }
    }

    private static bool QuoteMatchesDraft(OrderPlacementQuote quote, SellerOrderDraft draft)
    {
        if (quote.CustomerId != draft.CustomerId
            || quote.DeliveryAddressId != draft.DeliveryAddressId
            || quote.PaymentTermId != draft.PaymentTermId
            || quote.RequestedDeliveryDate != draft.RequestedDeliveryDate
            || quote.Lines.Count != draft.Items.Count)
        {
            return false;
        }

        var quoteLines = quote.Lines.OrderBy(line => line.ProductId).ToArray();
        var draftLines = draft.Items.OrderBy(item => item.ProductId).ToArray();
        for (var index = 0; index < quoteLines.Length; index++)
        {
            var quoted = quoteLines[index];
            var persisted = draftLines[index];
            if (quoted.ProductId != persisted.ProductId
                || quoted.Quantity != persisted.Quantity
                || quoted.RequestedDiscountAmount != persisted.RequestedDiscountAmount)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsQuoteBoundToDraft(
        string? quoteToken,
        string ownerUserId,
        SellerOrderDraft draft,
        int expectedQuoteVersion,
        out ProtectedSellerDraftQuote? protectedQuote)
    {
        return quoteProtector.TryUnprotect(quoteToken, out protectedQuote)
            && protectedQuote is not null
            && protectedQuote.OwnerUserId == ownerUserId
            && protectedQuote.DraftId == draft.Id
            && protectedQuote.Version == expectedQuoteVersion
            && QuoteMatchesDraft(protectedQuote.Quote, draft);
    }

    private async Task<(SellerOrderDraft Draft, Order Order)?> FindCommittedSubmissionAsync(
        ClaimsPrincipal user,
        string ownerUserId,
        int draftId,
        CancellationToken cancellationToken)
    {
        var draft = await db.SellerOrderDrafts.AsNoTracking()
            .Include(x => x.SubmittedOrder)
            .SingleOrDefaultAsync(x => x.Id == draftId
                && (user.IsInRole(ApplicationRoles.Administrator) || x.OwnerUserId == ownerUserId)
                && x.Status == SellerOrderDraftStatus.Submitted, cancellationToken);
        if (draft?.SubmittedOrder is null)
        {
            return null;
        }

        var scope = await accessService.GetSellerCartScopeAsync(user, draft.CustomerId, cancellationToken);
        return scope?.SalesRepresentativeId == draft.SalesRepresentativeId
            ? (draft, draft.SubmittedOrder)
            : null;
    }

    private async Task<bool> RollbackAndDisposeAsync(IDbContextTransaction? transaction, int draftId)
    {
        if (transaction is null)
        {
            return true;
        }

        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not roll back seller draft submission {DraftId}.", draftId);
            return false;
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private static bool IsSubmissionUniquenessOrSerializationConflict(Exception exception)
    {
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (cause.GetType().FullName == "Npgsql.PostgresException")
            {
                var sqlState = cause.GetType().GetProperty("SqlState")?.GetValue(cause) as string;
                var constraintName = cause.GetType().GetProperty("ConstraintName")?.GetValue(cause) as string;
                if (sqlState == "40001" || (sqlState == "23505" && constraintName == SellerDraftOrderUniqueIndex))
                {
                    return true;
                }
            }

            if (cause.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
                && cause.GetType().GetProperty("SqliteErrorCode")?.GetValue(cause) is int errorCode
                && errorCode == 19
                && (cause.Message.Contains(SellerDraftOrderUniqueIndex, StringComparison.Ordinal)
                    || cause.Message.Contains("Orders.SellerOrderDraftId", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static SellerOrderDraftSubmissionResult Success(
        SellerOrderDraft draft,
        Order order,
        bool wasIdempotentReplay) => new(
            true, draft.Id, draft.CustomerId, order.Id, order.Number, null, null, wasIdempotentReplay);

    private static SellerOrderDraftSubmissionResult Failure(string errorCode, string message) =>
        new(false, null, null, null, null, errorCode, message, false);
}

public sealed record SellerOrderDraftSubmissionResult(
    bool Succeeded,
    int? DraftId,
    int? CustomerId,
    int? OrderId,
    string? OrderNumber,
    string? ErrorCode,
    string? ErrorMessage,
    bool WasIdempotentReplay);
