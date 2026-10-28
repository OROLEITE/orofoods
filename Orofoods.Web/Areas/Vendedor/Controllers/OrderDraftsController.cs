using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Areas.Vendedor.Models;
using Orofoods.Web.Authorization;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Sellers;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class OrderDraftsController(
    SellerOrderDraftService draftService,
    SellerCatalogService catalogService,
    SellerWorkspaceService workspaceService,
    SalesRepresentativeAccessService accessService,
    IPaymentEligibilityService paymentEligibilityService,
    SellerOrderDraftSubmissionService submissionService,
    SellerOrderDraftQuoteProtector quoteProtector,
    ApplicationDbContext db) : Controller
{
    [HttpGet("/Vendedor/Rascunhos")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var drafts = await draftService.ListAsync(User, cancellationToken);
        return View(new SellerOrderDraftListViewModel(drafts));
    }

    [HttpGet("/Vendedor/Rascunhos/Novo")]
    public async Task<IActionResult> New(
        int? customerId,
        string? q,
        int page = 1,
        int? productId = null,
        CancellationToken cancellationToken = default)
    {
        if (customerId is not int selectedCustomerId)
        {
            var customers = await workspaceService.SearchCustomersForOrderDraftAsync(User, q, page, cancellationToken);
            if (customers is null) return Forbid();

            return View("Editor", new SellerOrderDraftEditorViewModel
            {
                CustomerId = 0,
                PendingProductId = productId,
                CustomerName = "",
                CustomerCode = "",
                CustomerSearch = customers.Query,
                CustomerResults = customers.Customers,
                CustomerPage = customers.Page,
                CustomerPageSize = customers.PageSize,
                CustomerTotalCount = customers.TotalCount,
                CustomerTotalPages = customers.TotalPages,
                CreateRequestKey = Guid.NewGuid()
            });
        }

        if (await accessService.GetSellerCartScopeAsync(User, selectedCustomerId, cancellationToken) is null)
        {
            return Forbid();
        }

        IReadOnlyList<SellerOrderDraftFormItem> initialItems = [];
        if (productId is int requestedProductId)
        {
            var selectableProducts = await catalogService.GetSelectableProductsByIdsAsync(
                User, selectedCustomerId, [requestedProductId], cancellationToken);
            var selectedProduct = selectableProducts?.SingleOrDefault();
            if (selectedProduct is null) return NotFound();

            initialItems = [new SellerOrderDraftFormItem
            {
                ProductId = selectedProduct.Id,
                Quantity = selectedProduct.MinimumCases
            }];
        }

        var editor = await BuildEditorAsync(
            selectedCustomerId, null, null, null, null, initialItems, Guid.NewGuid(), cancellationToken);
        return editor is null ? Forbid() : View("Editor", editor);
    }

    [HttpGet("/Vendedor/Rascunhos/Clientes")]
    public async Task<IActionResult> SearchCustomers(string? q, CancellationToken cancellationToken = default)
    {
        var suggestions = await workspaceService.SearchCustomerSuggestionsAsync(User, q, 20, cancellationToken);
        return suggestions is null ? Forbid() : Json(suggestions);
    }

    [HttpGet("/Vendedor/Rascunhos/Produtos")]
    public async Task<IActionResult> SearchProducts(
        int customerId,
        string? q,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (await accessService.GetSellerCartScopeAsync(User, customerId, cancellationToken) is null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(new { page = 1, pageSize = SellerCatalogService.MaximumPageSize, totalCount = 0, totalPages = 0, products = Array.Empty<object>() });
        }

        var catalog = await catalogService.GetCatalogAsync(
            User, customerId, q, cancellationToken, page, SellerCatalogService.MaximumPageSize);
        if (catalog is null) return Forbid();

        return Json(new
        {
            catalog.Page,
            catalog.PageSize,
            catalog.TotalCount,
            catalog.TotalPages,
            products = catalog.Products.Select(product => new
            {
                product.Id,
                product.Sku,
                product.Ean,
                product.Ean13,
                product.Dun14,
                product.Name,
                product.Brand,
                product.WmcCode,
                product.UnitDescription,
                product.CommercialUnitCode,
                product.PriceUnitLabel,
                product.AvailableQuantity,
                product.UnitPrice,
                product.MinimumCases
            })
        });
    }

    [HttpGet("/Vendedor/Rascunhos/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var draft = await draftService.GetAsync(User, id, cancellationToken);
        if (draft is null) return NotFound();
        if (draft.Status == SellerOrderDraftStatus.Submitted)
        {
            return RedirectToAction(nameof(Submitted), new { id });
        }
        var editor = await BuildEditorAsync(draft.CustomerId, draft, null, null, null, [], Guid.NewGuid(), cancellationToken);
        return editor is null ? NotFound() : View("Editor", editor);
    }

    [HttpGet("/Vendedor/Rascunhos/{id:int}/Confirmacao")]
    public async Task<IActionResult> Submitted(int id, CancellationToken cancellationToken)
    {
        var draft = await draftService.GetAsync(User, id, cancellationToken);
        if (draft is null) return NotFound();
        if (draft.Status != SellerOrderDraftStatus.Submitted || draft.SubmittedOrderId is not int orderId)
        {
            return RedirectToAction(nameof(Edit), new { id });
        }

        var order = await db.Orders.AsNoTracking()
            .Where(item => item.Id == orderId && item.CustomerId == draft.CustomerId)
            .Select(item => new { item.Id, item.Number, item.Total })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null) return NotFound();

        return View("Submitted", new SellerOrderDraftSubmittedViewModel(
            draft.CustomerId, order.Id, order.Number, order.Total));
    }

    [HttpPost("/Vendedor/Rascunhos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SellerOrderDraftCreateForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnCreateFormAsync(form, cancellationToken);
        }

        var result = await draftService.CreateAsync(User, new SellerOrderDraftInput(
            form.CustomerId,
            null,
            form.PaymentTermId,
            form.RequestedDeliveryDate,
            form.Notes,
            form.Items.Select(item => new SellerOrderDraftItemInput(item.ProductId, item.Quantity)).ToArray()),
            form.CreateRequestKey,
            cancellationToken);

        if (result.Succeeded)
        {
            return RedirectToAction(nameof(Edit), new { id = result.Draft!.Id });
        }

        if (result.ErrorCode == SellerOrderDraftErrorCodes.CustomerNotInPortfolio)
        {
            return Forbid();
        }

        if (result.ErrorCode == SellerOrderDraftErrorCodes.CreateRequestKeyConflict)
        {
            form.CreateRequestKey = Guid.NewGuid();
        }

        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Não foi possível salvar o rascunho.");
        return await ReturnCreateFormAsync(form, cancellationToken);
    }

    [HttpPost("/Vendedor/Rascunhos/{id:int}/Salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, SellerOrderDraftSaveForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReturnSaveFormAsync(id, form, cancellationToken);
        }

        var result = await draftService.SaveAsync(User, id, new SellerOrderDraftSaveInput(
            form.ExpectedVersion,
            null,
            form.PaymentTermId,
            form.RequestedDeliveryDate,
            form.Notes,
            form.Items.Select(item => new SellerOrderDraftItemInput(item.ProductId, item.Quantity)).ToArray(),
            form.RemovedProductIds), cancellationToken);

        if (result.Succeeded)
        {
            TempData["SellerDraftNotice"] = "Rascunho salvo. Preços e totais foram atualizados com o preço base vigente.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (result.ErrorCode == SellerOrderDraftErrorCodes.DraftNotFound)
        {
            return NotFound();
        }

        if (result.ErrorCode == SellerOrderDraftErrorCodes.CustomerNotInPortfolio)
        {
            return Forbid();
        }

        if (result.ErrorCode == SellerOrderDraftErrorCodes.ConcurrencyConflict)
        {
            TempData["SellerDraftError"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit), new { id });
        }

        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Não foi possível salvar o rascunho.");
        return await ReturnSaveFormAsync(id, form, cancellationToken);
    }

    [HttpPost("/Vendedor/Rascunhos/{id:int}/Enviar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, SellerOrderDraftSubmitForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["SellerDraftError"] = "Não foi possível validar a cotação do rascunho. Reabra-o para revisar os valores atuais antes de enviar.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var result = await submissionService.SubmitAsync(User, id, form.ExpectedVersion, form.QuoteToken, cancellationToken);
        if (result.Succeeded && result.OrderNumber is not null)
        {
            return RedirectToAction(nameof(Submitted), new { id });
        }

        if (result.ErrorCode == SellerOrderDraftErrorCodes.DraftNotFound)
        {
            return NotFound();
        }

        if (result.ErrorCode == SellerOrderDraftErrorCodes.CustomerNotInPortfolio)
        {
            return Forbid();
        }

        if (result.ErrorCode == SellerOrderDraftErrorCodes.CommercialQuoteChanged)
        {
            TempData["SellerDraftQuoteChanged"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["SellerDraftError"] = result.ErrorMessage ?? "Não foi possível enviar o pedido. O rascunho continua editável.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<IActionResult> ReturnCreateFormAsync(SellerOrderDraftCreateForm form, CancellationToken cancellationToken)
    {
        if (form.CustomerId <= 0 || await accessService.GetSellerCartScopeAsync(User, form.CustomerId, cancellationToken) is null)
        {
            return Forbid();
        }

        var editor = await BuildEditorAsync(
            form.CustomerId, null, form.PaymentTermId, form.RequestedDeliveryDate, form.Notes,
            form.Items, form.CreateRequestKey == Guid.Empty ? Guid.NewGuid() : form.CreateRequestKey, cancellationToken,
            usePostedInput: true);
        return editor is null ? Forbid() : View("Editor", editor);
    }

    private async Task<IActionResult> ReturnSaveFormAsync(
        int id,
        SellerOrderDraftSaveForm form,
        CancellationToken cancellationToken)
    {
        var draft = await draftService.GetAsync(User, id, cancellationToken);
        if (draft is null) return NotFound();
        var editor = await BuildEditorAsync(
            draft.CustomerId, draft, form.PaymentTermId, form.RequestedDeliveryDate, form.Notes,
            form.Items, Guid.NewGuid(), cancellationToken, form.RemovedProductIds, usePostedInput: true);
        return editor is null ? NotFound() : View("Editor", editor);
    }

    private async Task<SellerOrderDraftEditorViewModel?> BuildEditorAsync(
        int customerId,
        SellerOrderDraftView? draft,
        int? postedPaymentTermId,
        DateTime? postedDeliveryDate,
        string? postedNotes,
        IReadOnlyList<SellerOrderDraftFormItem>? postedItems,
        Guid createRequestKey,
        CancellationToken cancellationToken,
        IReadOnlyList<int>? removedProductIds = null,
        bool usePostedInput = false)
    {
        var workspace = await workspaceService.GetCustomerWorkspaceAsync(User, customerId, cancellationToken);
        if (workspace is null) return null;
        var customerMinimum = await db.Customers.AsNoTracking().Where(x => x.Id == customerId)
            .Select(x => x.MinimumOrder).SingleAsync(cancellationToken);
        var configuredMinimum = await db.OrderMinimumSettings.AsNoTracking()
            .Select(x => (decimal?)x.MinimumAmount).SingleOrDefaultAsync(cancellationToken);

        var eligibility = await paymentEligibilityService.GetAvailablePaymentOptionsAsync(customerId, cancellationToken);
        var paymentTerms = eligibility.PaymentMethods.Select(term => new SellerDraftPaymentOption(
            term.Id, term.Code, term.Name, term.DaysUntilDue)).ToArray();

        var pendingRemovals = (removedProductIds ?? []).ToHashSet();
        var sourceItems = usePostedInput
            ? (postedItems ?? []).Concat(draft?.Items
                .Where(item => pendingRemovals.Contains(item.ProductId)
                    && !(postedItems ?? []).Any(posted => posted.ProductId == item.ProductId))
                .Select(item => new SellerOrderDraftFormItem { ProductId = item.ProductId, Quantity = item.Quantity }) ?? [])
                .ToArray()
            : draft is not null
                ? draft.Items.Select(item => new SellerOrderDraftFormItem { ProductId = item.ProductId, Quantity = item.Quantity }).ToArray()
                : postedItems ?? [];
        var productRowsById = draft is not null && !usePostedInput
            ? draft.Items.ToDictionary(item => item.ProductId, item => new SellerCatalogProductRow(
                item.ProductId, item.Sku, item.ProductName, "", item.UnitDescription, item.ResolvedUnitPrice, item.MinimumCases, null, WmcCode: item.WmcCode))
            : new Dictionary<int, SellerCatalogProductRow>();
        if (draft is not null && usePostedInput)
        {
            foreach (var item in draft.Items.Where(item => pendingRemovals.Contains(item.ProductId)))
            {
                productRowsById[item.ProductId] = new SellerCatalogProductRow(
                    item.ProductId, item.Sku, item.ProductName, "", item.UnitDescription, item.ResolvedUnitPrice, item.MinimumCases, null, WmcCode: item.WmcCode);
            }
        }
        var freshRows = await catalogService.GetSelectableProductsByIdsAsync(
            User, customerId, sourceItems.Select(item => item.ProductId), cancellationToken);
        foreach (var row in freshRows ?? [])
        {
            productRowsById[row.Id] = row;
        }
        var editorItems = sourceItems.Select(item => productRowsById.TryGetValue(item.ProductId, out var product)
            ? new SellerOrderDraftEditorItem(item.ProductId, product.Sku, product.Name, product.UnitDescription,
                item.Quantity, product.MinimumCases, product.UnitPrice, pendingRemovals.Contains(item.ProductId), product.PriceUnitLabel, product.WmcCode)
            : new SellerOrderDraftEditorItem(item.ProductId, "", "Produto indisponível no catálogo atual", "", item.Quantity,
                1, 0m, pendingRemovals.Contains(item.ProductId), "caixa", null)).ToArray();

        string? quoteToken = null;
        var ownerUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!usePostedInput
            && draft is { Status: SellerOrderDraftStatus.Draft }
            && !string.IsNullOrWhiteSpace(ownerUserId)
            && editorItems.Length > 0)
        {
            var discountsByProductId = draft.Items.ToDictionary(item => item.ProductId, item => item.RequestedDiscountAmount);
            var quoteLines = editorItems.OrderBy(item => item.ProductId).Select(item => new OrderPlacementQuoteLine(
                item.ProductId,
                item.Quantity,
                item.UnitPrice,
                discountsByProductId.GetValueOrDefault(item.ProductId),
                item.Subtotal)).ToArray();
            var subtotal = quoteLines.Sum(item => item.Subtotal);
            quoteToken = quoteProtector.Protect(ownerUserId, draft.Id, draft.Version, new OrderPlacementQuote(
                customerId,
                null,
                draft.PaymentTermId,
                draft.RequestedDeliveryDate,
                quoteLines,
                subtotal,
                Freight: 0m,
                Total: subtotal));
        }

        return new SellerOrderDraftEditorViewModel
        {
            DraftId = draft?.Id,
            Status = draft?.Status,
            SubmittedOrderId = draft?.SubmittedOrderId,
            SubmittedOrderNumber = draft?.SubmittedOrderNumber,
            QuoteToken = quoteToken,
            CreateRequestKey = createRequestKey,
            ExpectedVersion = draft?.Version ?? 0,
            CustomerId = customerId,
            CustomerName = workspace.Customer.Name,
            CustomerCode = workspace.Customer.Code,
            ProductSearchUrl = Url.Action(nameof(SearchProducts), "OrderDrafts", new { area = "Vendedor", customerId }),
            PaymentTerms = paymentTerms,
            PaymentTermId = usePostedInput ? postedPaymentTermId : draft?.PaymentTermId,
            RequestedDeliveryDate = usePostedInput ? postedDeliveryDate : draft?.RequestedDeliveryDate,
            MinimumOrder = OrderMinimumPolicy.Resolve(configuredMinimum, customerMinimum),
            Notes = usePostedInput ? postedNotes ?? "" : draft?.Notes ?? "",
            Items = editorItems,
            RemovedProductIds = pendingRemovals.ToArray()
        };
    }
}
