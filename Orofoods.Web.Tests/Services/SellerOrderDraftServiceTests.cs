using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class SellerOrderDraftServiceTests
{
    [Fact]
    public async Task Administrator_selected_for_a_seller_can_list_and_reopen_that_sellers_drafts()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());
        Assert.True(created.Succeeded, created.ErrorMessage);
        var administrator = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, ApplicationRoles.Administrator),
            new Claim(AdminSellerContextService.SelectedRepresentativeClaimType, fixture.Representative.Id.ToString())
        ], "test"));

        var drafts = await fixture.Service.ListAsync(administrator);
        var reopened = await fixture.Service.GetAsync(administrator, created.Draft!.Id);

        Assert.Equal(created.Draft.Id, Assert.Single(drafts).Id);
        Assert.NotNull(reopened);
        Assert.Equal(fixture.Seller.Id, reopened.OwnerUserId);
        Assert.Equal(fixture.Representative.Id, reopened.SalesRepresentativeId);
    }

    [Fact]
    public async Task Create_and_reopen_persists_a_draft_with_current_customer_price_without_creating_an_order_or_reservation()
    {
        await using var fixture = await DraftFixture.CreateAsync();

        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id,
            fixture.Address.Id,
            fixture.PaymentTerm.Id,
            DateTime.UtcNow.Date.AddDays(2),
            "Entregar pela manhã",
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2)]));

        Assert.True(created.Succeeded, created.ErrorMessage);
        Assert.NotNull(created.Draft);
        Assert.Equal(SellerOrderDraftStatus.Draft, created.Draft.Status);
        Assert.Equal(fixture.Seller.Id, created.Draft.OwnerUserId);
        Assert.Equal(fixture.Representative.Id, created.Draft.SalesRepresentativeId);

        var reopened = await fixture.Service.GetAsync(fixture.SellerPrincipal, created.Draft.Id);

        Assert.NotNull(reopened);
        Assert.Equal(fixture.Customer.Id, reopened.CustomerId);
        Assert.Equal(fixture.Address.Id, reopened.DeliveryAddressId);
        Assert.Equal(fixture.PaymentTerm.Id, reopened.PaymentTermId);
        Assert.Equal("Entregar pela manhã", reopened.Notes);
        var item = Assert.Single(reopened.Items);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(50m, item.ResolvedUnitPrice);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.ToListAsync());
        Assert.Equal(0, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id).Select(x => x.QuantityReserved).SingleAsync());
    }

    [Fact]
    public async Task Create_and_save_accept_a_draft_without_delivery_address()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var input = new SellerOrderDraftInput(
            fixture.Customer.Id,
            DeliveryAddressId: null,
            fixture.PaymentTerm.Id,
            DateTime.UtcNow.Date.AddDays(1),
            "Pedido sem selecao manual de endereco",
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2)]);

        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, input);

        Assert.True(created.Succeeded, created.ErrorMessage);
        Assert.Null(created.Draft!.DeliveryAddressId);

        var saved = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version,
            DeliveryAddressId: null,
            fixture.PaymentTerm.Id,
            DateTime.UtcNow.Date.AddDays(2),
            "Rascunho sem endereco",
            [new SellerOrderDraftItemInput(fixture.Product.Id, 3)]));

        Assert.True(saved.Succeeded, saved.ErrorMessage);
        Assert.Null(saved.Draft!.DeliveryAddressId);
        Assert.Equal(3, Assert.Single(saved.Draft.Items).Quantity);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task Save_and_reopen_preserves_all_draft_items_and_updated_quantities()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var secondProduct = await fixture.AddAvailableProductAsync("SECOND");
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id,
            fixture.Address.Id,
            fixture.PaymentTerm.Id,
            null,
            null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2), new SellerOrderDraftItemInput(secondProduct.Id, 3)]));

        Assert.True(created.Succeeded, created.ErrorMessage);
        var saved = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version,
            fixture.Address.Id,
            fixture.PaymentTerm.Id,
            null,
            "Pedido atualizado",
            [new SellerOrderDraftItemInput(fixture.Product.Id, 4), new SellerOrderDraftItemInput(secondProduct.Id, 5)]));

        Assert.True(saved.Succeeded, saved.ErrorMessage);
        var reopened = await fixture.Service.GetAsync(fixture.SellerPrincipal, created.Draft.Id);

        Assert.NotNull(reopened);
        var itemsByProductId = reopened.Items.ToDictionary(item => item.ProductId, item => item.Quantity);
        Assert.Equal(2, itemsByProductId.Count);
        Assert.Equal(4, itemsByProductId[fixture.Product.Id]);
        Assert.Equal(5, itemsByProductId[secondProduct.Id]);
    }

    [Fact]
    public async Task Saving_without_the_legacy_delivery_date_keeps_an_existing_date_unchanged()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var existingDate = DateTime.UtcNow.Date.AddDays(5);
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, fixture.Address.Id, fixture.PaymentTerm.Id, existingDate, null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2)]));
        Assert.True(created.Succeeded, created.ErrorMessage);

        var saved = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, "Atualizado",
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2)]));

        Assert.True(saved.Succeeded, saved.ErrorMessage);
        Assert.Equal(existingDate, saved.Draft!.RequestedDeliveryDate);
    }

    [Fact]
    public async Task Repeated_create_request_key_returns_the_existing_draft_without_duplicating_it()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var requestKey = Guid.NewGuid();
        var input = fixture.ValidInput();

        var first = await fixture.Service.CreateAsync(fixture.SellerPrincipal, input, requestKey);
        var retry = await fixture.Service.CreateAsync(fixture.SellerPrincipal, input, requestKey);

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.True(retry.Succeeded, retry.ErrorMessage);
        Assert.Equal(first.Draft!.Id, retry.Draft!.Id);
        Assert.Single(await fixture.Db.SellerOrderDrafts.ToListAsync());
        Assert.Single(await fixture.Db.SellerOrderDraftItems.ToListAsync());
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_create_posts_with_the_same_key_persist_only_one_draft()
    {
        await using var fixture = await DraftFixture.CreateAsync(sharedDatabase: true);
        var second = await fixture.OpenSecondContextAsync();
        await using var secondDb = second.Db;
        await using var secondConnection = second.Connection;
        var secondAccess = new SalesRepresentativeAccessService(secondDb);
        var secondPrice = new PriceService(secondDb);
        var secondService = new SellerOrderDraftService(secondDb, secondAccess,
            new SellerCatalogService(secondDb, secondAccess, secondPrice), secondPrice,
            new PaymentEligibilityService(secondDb, Microsoft.Extensions.Options.Options.Create(new PaymentEligibilityOptions())));
        var key = Guid.NewGuid();
        var input = fixture.ValidInput();

        var results = await Task.WhenAll(
            fixture.Service.CreateAsync(fixture.SellerPrincipal, input, key),
            secondService.CreateAsync(fixture.SellerPrincipal, input, key));

        Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorMessage));
        Assert.Single(results.Select(result => result.Draft!.Id).Distinct());
        Assert.Single(await fixture.Db.SellerOrderDrafts.ToListAsync());
        Assert.Single(await fixture.Db.SellerOrderDraftItems.ToListAsync());
    }

    [Fact]
    public async Task Save_removes_only_products_explicitly_selected_by_the_seller()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var otherProduct = await fixture.AddAvailableProductAsync("SKU-OTHER");
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, fixture.Address.Id, fixture.PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2), new SellerOrderDraftItemInput(otherProduct.Id, 1)]));
        Assert.True(created.Succeeded, created.ErrorMessage);

        var saved = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(otherProduct.Id, 3)], [fixture.Product.Id]));

        Assert.True(saved.Succeeded, saved.ErrorMessage);
        var item = Assert.Single(saved.Draft!.Items);
        Assert.Equal(otherProduct.Id, item.ProductId);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public async Task Save_rejects_a_product_marked_for_both_update_and_removal()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());
        Assert.True(created.Succeeded, created.ErrorMessage);

        var result = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2)], [fixture.Product.Id]));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.InvalidInput, result.ErrorCode);
        Assert.Single(await fixture.Db.SellerOrderDraftItems.ToListAsync());
    }

    [Fact]
    public async Task Draft_schema_has_status_and_quantity_constraints_and_owner_lookup_indexes()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var schema = fixture.Db.Database.GenerateCreateScript();

        Assert.Contains("CK_SellerOrderDrafts_Status", schema);
        Assert.Contains("CK_SellerOrderDrafts_Version", schema);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_SellerOrderDraftItems_SellerOrderDraftId_ProductId\"", schema);
        Assert.Contains("IX_SellerOrderDrafts_OwnerUserId_Status_UpdatedAt", schema);
        Assert.Contains("IX_SellerOrderDrafts_OwnerUserId_CreateRequestKey", schema);
        Assert.Contains("CK_SellerOrderDraftItems_Quantity", schema);
        Assert.Contains("CK_SellerOrderDraftItems_RequestedDiscountAmount", schema);
    }

    [Fact]
    public async Task Create_rejects_a_customer_outside_the_sellers_portfolio()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var otherRepresentative = new SalesRepresentative { Name = "Outro representante", IsActive = true };
        var otherCustomer = new Customer
        {
            LegalName = "Cliente externo",
            TradeName = "Cliente externo",
            Cnpj = "22.222.222/0001-22",
            Status = CustomerStatus.Approved,
            IsActive = true,
            SalesRepresentative = otherRepresentative
        };
        fixture.Db.AddRange(otherRepresentative, otherCustomer);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            otherCustomer.Id, null, null, null, null, []));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, result.ErrorCode);
        Assert.Empty(await fixture.Db.SellerOrderDrafts.ToListAsync());
    }

    [Fact]
    public async Task Get_and_list_do_not_expose_another_users_drafts_even_inside_the_same_representative()
    {
        await using var fixture = await DraftFixture.CreateAsync(addSecondUserForRepresentative: true);
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());
        Assert.True(created.Succeeded, created.ErrorMessage);

        Assert.Single(await fixture.Service.ListAsync(fixture.SellerPrincipal));
        var otherUser = DraftFixture.CreateSellerPrincipal(fixture.OtherSeller!.Id);
        var reopened = await fixture.Service.GetAsync(otherUser, created.Draft!.Id);
        var listed = await fixture.Service.ListAsync(otherUser);

        Assert.Null(reopened);
        Assert.Empty(listed);
    }

    [Fact]
    public async Task Save_uses_optimistic_concurrency_and_does_not_overwrite_a_newer_version()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());
        Assert.True(created.Succeeded, created.ErrorMessage);

        var first = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, "Primeira gravação", []));
        var stale = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, "Gravação obsoleta", []));

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal(created.Draft.Version + 1, first.Draft!.Version);
        Assert.False(stale.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.ConcurrencyConflict, stale.ErrorCode);
        Assert.Equal("Primeira gravação", (await fixture.Service.GetAsync(fixture.SellerPrincipal, created.Draft.Id))!.Notes);
    }

    [Fact]
    public async Task Two_concurrent_saves_from_separate_contexts_allow_only_one_write()
    {
        await using var fixture = await DraftFixture.CreateAsync(sharedDatabase: true);
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());
        Assert.True(created.Succeeded, created.ErrorMessage);

        var second = await fixture.OpenSecondContextAsync();
        await using var secondDb = second.Db;
        await using var secondConnection = second.Connection;
        var secondAccess = new SalesRepresentativeAccessService(secondDb);
        var secondPrice = new PriceService(secondDb);
        var secondService = new SellerOrderDraftService(secondDb, secondAccess,
            new SellerCatalogService(secondDb, secondAccess, secondPrice), secondPrice,
            new PaymentEligibilityService(secondDb, Microsoft.Extensions.Options.Options.Create(new PaymentEligibilityOptions())));

        var firstSave = fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id,
            new SellerOrderDraftSaveInput(created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, "Sessao A",
                [new SellerOrderDraftItemInput(fixture.Product.Id, 3)]));
        var secondSave = secondService.SaveAsync(fixture.SellerPrincipal, created.Draft.Id,
            new SellerOrderDraftSaveInput(created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, "Sessao B",
                [new SellerOrderDraftItemInput(fixture.Product.Id, 4)]));
        var results = await Task.WhenAll(firstSave, secondSave);

        var winner = Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => !result.Succeeded && result.ErrorCode == SellerOrderDraftErrorCodes.ConcurrencyConflict);
        var stored = await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == created.Draft.Id);
        Assert.Equal(created.Draft.Version + 1, stored.Version);
        Assert.Equal(winner.Draft!.Notes, stored.Notes);
        Assert.Equal(winner.Draft.Items.Single().Quantity,
            await fixture.Db.SellerOrderDraftItems.AsNoTracking().Where(x => x.SellerOrderDraftId == created.Draft.Id)
                .Select(x => x.Quantity).SingleAsync());
    }

    [Fact]
    public async Task Save_does_not_silently_remove_items_omitted_from_an_update()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());

        var saved = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, "Ajustei as observações", []));

        Assert.True(saved.Succeeded, saved.ErrorMessage);
        Assert.Single(saved.Draft!.Items);
        var removed = await fixture.Service.RemoveItemAsync(fixture.SellerPrincipal, created.Draft.Id, fixture.Product.Id, saved.Draft.Version);
        Assert.True(removed.Succeeded, removed.ErrorMessage);
        Assert.Empty(removed.Draft!.Items);
    }

    [Fact]
    public async Task Save_cannot_edit_a_submitted_draft()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());
        var draft = await fixture.Db.SellerOrderDrafts.SingleAsync(x => x.Id == created.Draft!.Id);
        draft.Status = SellerOrderDraftStatus.Submitted;
        draft.SubmittedAt = DateTime.UtcNow;
        draft.SubmittedOrder = new Order
        {
            Number = "ORO-2026-000001",
            CustomerId = fixture.Customer.Id,
            CreatedByUserId = fixture.Seller.Id,
            Status = OrderStatus.Received,
            SellerOrderDraft = draft
        };
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SaveAsync(fixture.SellerPrincipal, draft.Id, new SellerOrderDraftSaveInput(
            draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, "Não pode alterar", []));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.AlreadySubmitted, result.ErrorCode);
        Assert.NotEqual("Não pode alterar", (await fixture.Db.SellerOrderDrafts.SingleAsync(x => x.Id == draft.Id)).Notes);
    }

    [Fact]
    public async Task Create_rejects_requested_discount_when_no_authorization_policy_exists()
    {
        await using var fixture = await DraftFixture.CreateAsync();

        var result = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, fixture.Address.Id, fixture.PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2, 0.01m)]));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.DiscountPolicyNotConfigured, result.ErrorCode);
        Assert.Empty(await fixture.Db.SellerOrderDrafts.ToListAsync());
        Assert.Empty(await fixture.Db.SellerOrderDraftItems.ToListAsync());
    }

    [Fact]
    public async Task Save_rejects_requested_discount_when_no_authorization_policy_exists()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());
        Assert.True(created.Succeeded, created.ErrorMessage);

        var result = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version,
            fixture.Address.Id,
            fixture.PaymentTerm.Id,
            null,
            "Tentativa de desconto",
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2, 0.01m)]));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.DiscountPolicyNotConfigured, result.ErrorCode);

        var persisted = await fixture.Service.GetAsync(fixture.SellerPrincipal, created.Draft.Id);
        Assert.NotNull(persisted);
        Assert.Equal(created.Draft.Version, persisted.Version);
        Assert.Equal(0m, Assert.Single(persisted.Items).RequestedDiscountAmount);
        Assert.Equal("", persisted.Notes);
    }

    [Fact]
    public async Task Create_rejects_a_product_id_that_is_not_in_the_current_seller_catalog()
    {
        await using var fixture = await DraftFixture.CreateAsync();

        var result = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, null, null, null, null,
            [new SellerOrderDraftItemInput(fixture.UnavailableProduct.Id, 1)]));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.ProductNotSelectable, result.ErrorCode);
        Assert.Empty(await fixture.Db.SellerOrderDrafts.ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_a_product_without_a_positive_base_price()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        fixture.Product.BasePrice = 0m;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.ProductNotSelectable, result.ErrorCode);
        Assert.Empty(await fixture.Db.SellerOrderDrafts.ToListAsync());
    }

    [Fact]
    public async Task Save_rejects_an_item_when_its_base_price_becomes_non_positive()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(fixture.SellerPrincipal, fixture.ValidInput());
        Assert.True(created.Succeeded, created.ErrorMessage);
        fixture.Product.BasePrice = -1m;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SaveAsync(fixture.SellerPrincipal, created.Draft!.Id, new SellerOrderDraftSaveInput(
            created.Draft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2)]));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.ProductNotSelectable, result.ErrorCode);
        Assert.Equal(created.Draft.Version, (await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync()).Version);
    }

    [Fact]
    public async Task Create_rejects_quantities_below_the_product_minimum_without_adjusting_them_silently()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        fixture.Product.MinimumCases = 3;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, fixture.Address.Id, fixture.PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2)]));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.BelowMinimumQuantity, result.ErrorCode);
        Assert.Empty(await fixture.Db.SellerOrderDrafts.ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_two_different_products_with_the_same_wmc_code()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        fixture.Product.WmcCode = "504342";
        fixture.Product.IsWmcActive = true;
        fixture.Product.WmcStockAvailable = true;
        fixture.Product.WmcInitialLoadReady = true;
        var duplicate = await fixture.AddAvailableProductAsync("DUP-WMC");
        duplicate.WmcCode = "504342";
        duplicate.IsWmcActive = true;
        duplicate.WmcStockAvailable = true;
        duplicate.WmcInitialLoadReady = true;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, fixture.Address.Id, fixture.PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 1), new SellerOrderDraftItemInput(duplicate.Id, 1)]));

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.InvalidInput, result.ErrorCode);
        Assert.Equal("Este item já existe no pedido!", result.ErrorMessage);
        Assert.Empty(await fixture.Db.SellerOrderDrafts.ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_the_same_product_twice_by_its_wmc_code()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        fixture.Product.WmcCode = "504342";
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, fixture.Address.Id, fixture.PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(fixture.Product.Id, 1), new SellerOrderDraftItemInput(fixture.Product.Id, 2)]));

        Assert.False(result.Succeeded);
        Assert.Equal("Este item já existe no pedido!", result.ErrorMessage);
        Assert.Empty(await fixture.Db.SellerOrderDrafts.ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_an_address_from_another_customer_and_a_disallowed_payment_term()
    {
        await using var fixture = await DraftFixture.CreateAsync();
        var otherCustomer = new Customer
        {
            LegalName = "Outro cliente",
            TradeName = "Outro cliente",
            Cnpj = "33.333.333/0001-33",
            Status = CustomerStatus.Approved,
            IsActive = true,
            SalesRepresentativeId = fixture.Representative.Id
        };
        var otherAddress = new CustomerAddress { Customer = otherCustomer, Label = "Outro", Street = "Rua B", Number = "2", City = "Campinas", State = "SP", IsActive = true };
        var creditTerm = new PaymentTerm { Code = "BOLETO14", Name = "Boleto 14 dias", DaysUntilDue = 14, IsActive = true };
        fixture.Db.AddRange(otherCustomer, otherAddress, creditTerm);
        await fixture.Db.SaveChangesAsync();

        var invalidAddress = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, otherAddress.Id, fixture.PaymentTerm.Id, null, null, []));
        var invalidPayment = await fixture.Service.CreateAsync(fixture.SellerPrincipal, new SellerOrderDraftInput(
            fixture.Customer.Id, fixture.Address.Id, creditTerm.Id, null, null, []));

        Assert.Equal(SellerOrderDraftErrorCodes.InvalidAddress, invalidAddress.ErrorCode);
        Assert.Equal(SellerOrderDraftErrorCodes.PaymentTermNotAllowed, invalidPayment.ErrorCode);
        Assert.Empty(await fixture.Db.SellerOrderDrafts.ToListAsync());
    }

    private sealed class DraftFixture : IAsyncDisposable
    {
        private DraftFixture(ApplicationDbContext db, SalesRepresentative representative, ApplicationUser seller, ApplicationUser? otherSeller,
            Customer customer, CustomerAddress address, PaymentTerm paymentTerm, Product product, Product unavailableProduct,
            string? sharedConnectionString = null, SqliteConnection? keepAliveConnection = null)
        {
            Db = db;
            Representative = representative;
            Seller = seller;
            OtherSeller = otherSeller;
            Customer = customer;
            Address = address;
            PaymentTerm = paymentTerm;
            Product = product;
            UnavailableProduct = unavailableProduct;
            SharedConnectionString = sharedConnectionString;
            KeepAliveConnection = keepAliveConnection;
            SellerPrincipal = CreateSellerPrincipal(seller.Id);
            var access = new SalesRepresentativeAccessService(db);
            var priceService = new PriceService(db);
            Service = new SellerOrderDraftService(db, access, new SellerCatalogService(db, access, priceService), priceService,
                new PaymentEligibilityService(db, Microsoft.Extensions.Options.Options.Create(new PaymentEligibilityOptions())));
        }

        public ApplicationDbContext Db { get; }
        public SalesRepresentative Representative { get; }
        public ApplicationUser Seller { get; }
        public ApplicationUser? OtherSeller { get; }
        public Customer Customer { get; }
        public CustomerAddress Address { get; }
        public PaymentTerm PaymentTerm { get; }
        public Product Product { get; }
        public Product UnavailableProduct { get; }
        public ClaimsPrincipal SellerPrincipal { get; }
        public SellerOrderDraftService Service { get; }
        private string? SharedConnectionString { get; }
        private SqliteConnection? KeepAliveConnection { get; }

        public SellerOrderDraftInput ValidInput() => new(Customer.Id, Address.Id, PaymentTerm.Id, null, null,
            [new SellerOrderDraftItemInput(Product.Id, 2)]);

        public async Task<Product> AddAvailableProductAsync(string sku)
        {
            var product = ProductFactory(sku, sku, active: true);
            Db.Products.Add(product);
            await Db.SaveChangesAsync();
            Db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 10 });
            await Db.SaveChangesAsync();
            return product;
        }

        public static ClaimsPrincipal CreateSellerPrincipal(string userId) => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")], "test"));

        public async Task<(ApplicationDbContext Db, SqliteConnection Connection)> OpenSecondContextAsync()
        {
            if (SharedConnectionString is null)
            {
                throw new InvalidOperationException("This fixture does not use a shared SQLite database.");
            }

            var connection = new SqliteConnection(SharedConnectionString);
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            return (new ApplicationDbContext(options), connection);
        }

        public static async Task<DraftFixture> CreateAsync(bool addSecondUserForRepresentative = false, bool sharedDatabase = false)
        {
            SqliteConnection? keepAlive = null;
            string? sharedConnectionString = null;
            ApplicationDbContext db;
            if (sharedDatabase)
            {
                sharedConnectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = $"seller-draft-{Guid.NewGuid():N}",
                    Mode = SqliteOpenMode.Memory,
                    Cache = SqliteCacheMode.Shared,
                    ForeignKeys = true
                }.ToString();
                keepAlive = new SqliteConnection(sharedConnectionString);
                await keepAlive.OpenAsync();
                db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(keepAlive).Options);
                await db.Database.EnsureCreatedAsync();
            }
            else
            {
                db = await TestDbContextFactory.CreateAsync();
            }
            var representative = new SalesRepresentative { Name = "Vendedor A", IsActive = true };
            var seller = new ApplicationUser { Id = "seller-a", UserName = "seller-a", IsActive = true, SalesRepresentative = representative };
            ApplicationUser? otherSeller = addSecondUserForRepresentative
                ? new ApplicationUser { Id = "seller-a-2", UserName = "seller-a-2", IsActive = true, SalesRepresentative = representative }
                : null;
            var table = new PriceTable { Name = "Tabela vigente", IsActive = true };
            var customer = new Customer
            {
                LegalName = "Cliente da carteira",
                TradeName = "Cliente da carteira",
                Cnpj = "11.111.111/0001-11",
                Status = CustomerStatus.Approved,
                IsActive = true,
                CreditLimit = 10000m,
                SalesRepresentative = representative,
                PriceTable = table
            };
            var address = new CustomerAddress { Customer = customer, Label = "Principal", Street = "Rua A", Number = "1", City = "Campinas", State = "SP", IsActive = true };
            var paymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, IsActive = true };
            var product = ProductFactory("DRAFT-001", "Produto disponível", active: true);
            var unavailable = ProductFactory("DRAFT-002", "Produto fora do catálogo", active: false);
            var priceItem = new PriceTableItem { PriceTable = table, Product = product, Price = 40m, PromotionalPrice = 37m };
            db.AddRange(representative, seller, table, customer, address, paymentTerm, product, unavailable, priceItem);
            if (otherSeller is not null) db.Users.Add(otherSeller);
            await db.SaveChangesAsync();
            db.ProductInventories.AddRange(
                new ProductInventory { ProductId = product.Id, QuantityOnHand = 10 },
                new ProductInventory { ProductId = unavailable.Id, QuantityOnHand = 10 });
            await db.SaveChangesAsync();
            return new DraftFixture(db, representative, seller, otherSeller, customer, address, paymentTerm, product, unavailable,
                sharedConnectionString, keepAlive);
        }

        private static Product ProductFactory(string sku, string name, bool active) => new()
        {
            Sku = sku,
            Name = name,
            Brand = "Orofoods",
            BasePrice = 50m,
            MinimumCases = 1,
            IsActive = active,
            IsAvailable = true,
            ProductCategory = new ProductCategory { Name = "Categoria", Slug = sku.ToLowerInvariant() }
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (KeepAliveConnection is not null)
            {
                await KeepAliveConnection.DisposeAsync();
            }
        }
    }
}
