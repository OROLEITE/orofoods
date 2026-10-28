using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class SellerOrderDraftSubmissionServiceTests
{
    [Fact]
    public async Task Administrator_selected_for_a_seller_can_submit_its_draft_and_order_records_the_administrator()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));
        fixture.Db.Users.Add(new ApplicationUser { Id = "admin-user", UserName = "admin-user", IsActive = true });
        await fixture.Db.SaveChangesAsync();
        var administrator = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, ApplicationRoles.Administrator),
            new Claim(AdminSellerContextService.SelectedRepresentativeClaimType, fixture.Representative.Id.ToString())
        ], "test"));
        var quote = await fixture.CreateQuoteTokenAsync(administrator, draft);

        var result = await fixture.Service.SubmitAsync(administrator, draft.Id, draft.Version, quote);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var order = await fixture.Db.Orders.SingleAsync(x => x.SellerOrderDraftId == draft.Id);
        Assert.Equal("admin-user", order.CreatedByUserId);
        Assert.Equal(fixture.Representative.Id, order.SalesRepresentativeId);
    }

    [Fact]
    public async Task Submit_creates_one_received_order_links_the_draft_and_reserves_every_item_at_current_prices()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var secondProduct = await fixture.AddAvailableProductAsync("SUBMIT-002", price: 23m);
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 2, 0m), (secondProduct.Id, 3, 0m));
        var expectedVersion = draft.Version;

        var result = await fixture.SubmitAsync(draft, expectedVersion: expectedVersion);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(result.WasIdempotentReplay);
        Assert.Equal("ORO-2026-000001", result.OrderNumber);
        var order = await fixture.Db.Orders.Include(x => x.Items).Include(x => x.StatusHistory)
            .SingleAsync(x => x.Id == result.OrderId);
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Equal(fixture.Seller.Id, order.CreatedByUserId);
        Assert.Equal(fixture.Representative.Id, order.SalesRepresentativeId);
        Assert.Equal(draft.Id, order.SellerOrderDraftId);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(50m, order.Items.Single(x => x.ProductId == fixture.Product.Id).UnitPrice);
        Assert.Equal(50m, order.Items.Single(x => x.ProductId == secondProduct.Id).UnitPrice);
        Assert.Equal(250m, order.Total);
        Assert.Equal(OrderStatus.Received, Assert.Single(order.StatusHistory).Status);
        Assert.Equal(2, await fixture.Db.InventoryReservations.CountAsync(x => x.OrderId == order.Id && x.Status == InventoryReservationStatus.Active));

        var submittedDraft = await fixture.Db.SellerOrderDrafts.AsNoTracking().Include(x => x.SubmittedOrder)
            .SingleAsync(x => x.Id == draft.Id);
        Assert.Equal(SellerOrderDraftStatus.Submitted, submittedDraft.Status);
        Assert.Equal(order.Id, submittedDraft.SubmittedOrder!.Id);
        Assert.NotNull(submittedDraft.SubmittedAt);
        Assert.Equal(expectedVersion + 1, submittedDraft.Version);
        Assert.Equal(2, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id)
            .Select(x => x.QuantityReserved).SingleAsync());
        Assert.Empty(await fixture.Db.WmcExportAudits.ToListAsync());
        Assert.Null(await fixture.Db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == order.Id));
        Assert.Empty(await fixture.Db.Payments.Where(x => x.OrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task Submit_accepts_draft_without_delivery_address_and_still_reserves_stock()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 2, 0m));
        draft.DeliveryAddressId = null;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.SubmitAsync(draft);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var order = await fixture.Db.Orders.SingleAsync(x => x.Id == result.OrderId);
        Assert.Null(order.DeliveryAddressId);
        Assert.Single(await fixture.Db.InventoryReservations.Where(x => x.OrderId == order.Id && x.Status == InventoryReservationStatus.Active).ToListAsync());
        Assert.Equal(2, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id)
            .Select(x => x.QuantityReserved).SingleAsync());
        Assert.Empty(await fixture.Db.WmcExportAudits.ToListAsync());
        Assert.Null(await fixture.Db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == order.Id));
    }

    [Fact]
    public async Task Submit_rejects_price_changes_since_the_last_server_quote_without_side_effects()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 2, 0m));
        var previousQuote = await fixture.CreateQuoteTokenAsync(fixture.SellerPrincipal, draft);
        fixture.Product.BasePrice = 31m;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SubmitAsync(fixture.SellerPrincipal, draft.Id, draft.Version, previousQuote);

        Assert.False(result.Succeeded);
        Assert.Equal("commercial_quote_changed", result.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.ToListAsync());
        Assert.Equal(0, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id)
            .Select(x => x.QuantityReserved).SingleAsync());
        var unchangedDraft = await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == draft.Id);
        Assert.Equal(SellerOrderDraftStatus.Draft, unchangedDraft.Status);
        Assert.Equal(draft.Version, unchangedDraft.Version);
    }

    [Fact]
    public async Task Submit_requires_explicit_confirmation_of_the_refreshed_price_then_uses_that_exact_quote()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 2, 0m));
        var displayedQuote = await fixture.CreateQuoteTokenAsync(fixture.SellerPrincipal, draft);
        fixture.Product.BasePrice = 31m;
        await fixture.Db.SaveChangesAsync();

        var firstAttempt = await fixture.Service.SubmitAsync(fixture.SellerPrincipal, draft.Id, draft.Version, displayedQuote);

        Assert.False(firstAttempt.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.CommercialQuoteChanged, firstAttempt.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.AsNoTracking().ToListAsync());
        Assert.Equal(SellerOrderDraftStatus.Draft, (await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).Status);

        var refreshedQuote = await fixture.CreateQuoteTokenAsync(fixture.SellerPrincipal, draft);
        var confirmedAttempt = await fixture.Service.SubmitAsync(fixture.SellerPrincipal, draft.Id, draft.Version, refreshedQuote);

        Assert.True(confirmedAttempt.Succeeded, confirmedAttempt.ErrorMessage);
        var order = await fixture.Db.Orders.Include(x => x.Items).SingleAsync(x => x.Id == confirmedAttempt.OrderId);
        Assert.Equal(31m, order.Items.Single().UnitPrice);
        Assert.Equal(62m, order.Total);
        Assert.Single(await fixture.Db.Orders.Where(x => x.SellerOrderDraftId == draft.Id).ToListAsync());
        Assert.Single(await fixture.Db.InventoryReservations.Where(x => x.OrderId == order.Id && x.Status == InventoryReservationStatus.Active).ToListAsync());
    }

    [Fact]
    public async Task Submit_rejects_a_quote_bound_to_another_seller_or_draft()
    {
        await using var fixture = await SubmissionFixture.CreateAsync(addSecondSeller: true);
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));
        var otherSellerQuote = await fixture.CreateQuoteTokenAsync(
            SubmissionFixture.CreateSellerPrincipal(fixture.OtherSeller!.Id), draft);
        var sellerMismatch = await fixture.Service.SubmitAsync(
            fixture.SellerPrincipal, draft.Id, draft.Version, otherSellerQuote);

        Assert.False(sellerMismatch.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.CommercialQuoteInvalid, sellerMismatch.ErrorCode);

        var anotherDraft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));
        var wrongDraftQuote = await fixture.CreateQuoteTokenAsync(fixture.SellerPrincipal, draft);
        var draftMismatch = await fixture.Service.SubmitAsync(
            fixture.SellerPrincipal, anotherDraft.Id, anotherDraft.Version, wrongDraftQuote);

        Assert.False(draftMismatch.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.CommercialQuoteInvalid, draftMismatch.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());

        var correctDraftQuote = await fixture.CreateQuoteTokenAsync(fixture.SellerPrincipal, draft);
        var submitted = await fixture.Service.SubmitAsync(fixture.SellerPrincipal, draft.Id, draft.Version, correctDraftQuote);
        Assert.True(submitted.Succeeded, submitted.ErrorMessage);

        var anotherDraftQuote = await fixture.CreateQuoteTokenAsync(fixture.SellerPrincipal, anotherDraft);
        var wrongQuoteAfterSubmission = await fixture.Service.SubmitAsync(
            fixture.SellerPrincipal, draft.Id, draft.Version, anotherDraftQuote);
        Assert.False(wrongQuoteAfterSubmission.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.CommercialQuoteInvalid, wrongQuoteAfterSubmission.ErrorCode);
    }

    [Fact]
    public async Task Submit_rejects_a_quote_after_quantity_changes_without_a_version_increment()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));
        var oldQuote = await fixture.CreateQuoteTokenAsync(fixture.SellerPrincipal, draft);
        draft.Items.Single().Quantity = 3;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SubmitAsync(fixture.SellerPrincipal, draft.Id, draft.Version, oldQuote);

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.CommercialQuoteInvalid, result.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
        Assert.Equal(SellerOrderDraftStatus.Draft,
            (await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).Status);
    }

    [Fact]
    public async Task Submit_rejects_a_draft_owned_by_another_user_on_the_same_representative()
    {
        await using var fixture = await SubmissionFixture.CreateAsync(addSecondSeller: true);
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));
        var otherSeller = SubmissionFixture.CreateSellerPrincipal(fixture.OtherSeller!.Id);

        var result = await fixture.SubmitAsync(draft, otherSeller);

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.DraftNotFound, result.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Equal(SellerOrderDraftStatus.Draft, (await fixture.Db.SellerOrderDrafts.SingleAsync(x => x.Id == draft.Id)).Status);
    }

    [Fact]
    public async Task Submit_rejects_a_customer_that_left_the_sellers_portfolio()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));
        var newRepresentative = new SalesRepresentative { Name = "Novo vendedor", IsActive = true };
        fixture.Db.SalesRepresentatives.Add(newRepresentative);
        await fixture.Db.SaveChangesAsync();
        fixture.Customer.SalesRepresentativeId = newRepresentative.Id;
        fixture.Customer.SalesRepresentative = newRepresentative;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.SubmitAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.CustomerNotInPortfolio, result.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task Submit_ignores_an_inactive_legacy_delivery_address_and_creates_an_addressless_order()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));
        fixture.Address.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.SubmitAsync(draft);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var order = await fixture.Db.Orders.SingleAsync();
        Assert.Null(order.DeliveryAddressId);
        var persistedDraft = await fixture.Db.SellerOrderDrafts.SingleAsync(x => x.Id == draft.Id);
        Assert.Equal(SellerOrderDraftStatus.Submitted, persistedDraft.Status);
        Assert.Equal(fixture.Address.Id, persistedDraft.DeliveryAddressId);
    }

    [Fact]
    public async Task Submit_rejects_a_payment_term_that_is_no_longer_authorized()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));
        var term = new PaymentTerm { Code = "BOLETO14", Name = "Boleto 14 dias", DaysUntilDue = 14, IsActive = true };
        fixture.Db.PaymentTerms.Add(term);
        await fixture.Db.SaveChangesAsync();
        draft.PaymentTermId = term.Id;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.SubmitAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.InvalidOrder, result.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task Submit_rejects_a_stale_version_without_creating_an_order()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));

        var result = await fixture.SubmitAsync(draft, expectedVersion: draft.Version + 1);

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Equal(SellerOrderDraftStatus.Draft, (await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).Status);
    }

    [Fact]
    public async Task Submit_rejects_insufficient_credit_without_reserving_stock_or_changing_the_draft()
    {
        var paymentConditionReader = new FixedWmcPaymentConditionReader(new Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition(
            "5264", "3", "3", "14 DIAS", 1, 14, 0, "A", "WMC Firebird mirror", DateTimeOffset.UtcNow));
        await using var fixture = await SubmissionFixture.CreateAsync(paymentConditionReader: paymentConditionReader);
        fixture.Customer.WmcCode = "5264";
        fixture.PaymentTerm.Code = "BOLETO_14D";
        fixture.PaymentTerm.Name = "Boleto 14 dias";
        fixture.PaymentTerm.DaysUntilDue = 14;
        fixture.Customer.CreditLimit = 10m;
        fixture.Customer.CreditOverrideEnabled = true;
        fixture.Customer.MaximumPaymentTermDays = 14;
        await fixture.Db.SaveChangesAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));

        var result = await fixture.SubmitAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Contains("cr\u00e9dito dispon\u00edvel", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.ToListAsync());
        Assert.Equal(0, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id)
            .Select(x => x.QuantityReserved).SingleAsync());
        Assert.Equal(SellerOrderDraftStatus.Draft, (await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).Status);
    }

    [Fact]
    public async Task Submit_rejects_a_product_that_became_unavailable_without_submitting_any_item()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var secondProduct = await fixture.AddAvailableProductAsync("SUBMIT-002", price: 23m);
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m), (secondProduct.Id, 1, 0m));
        secondProduct.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.SubmitAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.ToListAsync());
        Assert.Equal(SellerOrderDraftStatus.Draft, (await fixture.Db.SellerOrderDrafts.SingleAsync(x => x.Id == draft.Id)).Status);
    }

    [Fact]
    public async Task Submit_accepts_items_with_zero_stock_and_creates_the_order()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var secondProduct = await fixture.AddAvailableProductAsync("SUBMIT-002", price: 23m, stock: 0);
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m), (secondProduct.Id, 1, 0m));

        var result = await fixture.SubmitAsync(draft);

        Assert.True(result.Succeeded);
        Assert.Single(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task Submit_rejects_nonzero_discount_and_keeps_the_draft_editable()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 2, 1m));

        var result = await fixture.SubmitAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.DiscountPolicyNotConfigured, result.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Equal(SellerOrderDraftStatus.Draft, (await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).Status);
    }

    [Fact]
    public async Task Submit_returns_the_same_order_on_a_sequential_retry_without_duplicate_reservations()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 2, 0m));
        var originalQuote = await fixture.CreateQuoteTokenAsync(fixture.SellerPrincipal, draft);
        var first = await fixture.Service.SubmitAsync(fixture.SellerPrincipal, draft.Id, draft.Version, originalQuote);
        var retry = await fixture.Service.SubmitAsync(fixture.SellerPrincipal, draft.Id, draft.Version, originalQuote);

        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.True(retry.Succeeded, retry.ErrorMessage);
        Assert.True(retry.WasIdempotentReplay);
        Assert.Equal(first.OrderId, retry.OrderId);
        Assert.Equal(first.OrderNumber, retry.OrderNumber);
        Assert.Single(await fixture.Db.Orders.Where(x => x.SellerOrderDraftId == draft.Id).ToListAsync());
        Assert.Single(await fixture.Db.InventoryReservations.Where(x => x.OrderId == first.OrderId && x.Status == InventoryReservationStatus.Active).ToListAsync());
        Assert.Single(await fixture.Db.OrderStatusHistories.Where(x => x.OrderId == first.OrderId).ToListAsync());
        Assert.Equal(2, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id)
            .Select(x => x.QuantityReserved).SingleAsync());
    }

    [Fact]
    public async Task Submit_rolls_back_every_write_and_keeps_the_draft_editable_if_submitted_status_cannot_be_saved()
    {
        await using var fixture = await SubmissionFixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 2, 0m));
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER "RejectSellerDraftSubmissionForTest"
            BEFORE UPDATE OF "Status" ON "SellerOrderDrafts"
            WHEN NEW."Status" = 'Submitted'
            BEGIN
                SELECT RAISE(ABORT, 'intentional seller draft submission failure');
            END;
            """);

        var result = await fixture.SubmitAsync(draft);

        Assert.False(result.Succeeded);
        Assert.Equal(SellerOrderDraftErrorCodes.SubmissionFailed, result.ErrorCode);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.OrderStatusHistories.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.AsNoTracking().ToListAsync());
        Assert.Equal(0, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id)
            .Select(x => x.QuantityReserved).SingleAsync());

        await fixture.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"RejectSellerDraftSubmissionForTest\"");
        var persistedDraft = await fixture.Db.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == draft.Id);
        var saved = await fixture.DraftService.SaveAsync(fixture.SellerPrincipal, draft.Id, new SellerOrderDraftSaveInput(
            persistedDraft.Version, fixture.Address.Id, fixture.PaymentTerm.Id, null, "Continua editável",
            [new SellerOrderDraftItemInput(fixture.Product.Id, 2)]));
        Assert.True(saved.Succeeded, saved.ErrorMessage);
    }

    [Fact]
    public async Task Submit_keeps_card_on_delivery_payment_local_pending_and_never_calls_payment_or_wmc_integrations()
    {
        await using var fixture = await SubmissionFixture.CreateAsync(cardOnDeliveryEnabled: true);
        fixture.PaymentTerm.Code = "CARD_ON_DELIVERY";
        fixture.PaymentTerm.Name = "Cartão na entrega";
        await fixture.Db.SaveChangesAsync();
        var draft = await fixture.CreateDraftAsync((fixture.Product.Id, 1, 0m));

        var result = await fixture.SubmitAsync(draft);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var payment = await fixture.Db.Payments.SingleAsync(x => x.OrderId == result.OrderId);
        Assert.Equal(PaymentMethodType.CardOnDelivery, payment.Method);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.Gateway);
        Assert.Null(payment.GatewayOrderId);
        Assert.Null(payment.GatewayPaymentId);
        Assert.Null(payment.ExternalPaymentId);
        Assert.Null(payment.IdempotencyKey);
        Assert.Empty(await fixture.Db.WmcExportAudits.ToListAsync());
        Assert.Null(await fixture.Db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == result.OrderId));
    }

    private sealed class SubmissionFixture : IAsyncDisposable
    {
        private SubmissionFixture(ApplicationDbContext db, SalesRepresentative representative, ApplicationUser seller,
            ApplicationUser? otherSeller, Customer customer, CustomerAddress address, PaymentTerm paymentTerm,
            Product product, PriceTableItem priceTableItem, bool cardOnDeliveryEnabled,
            Orofoods.Web.Integrations.Erp.Wmc.IWmcCustomerPaymentConditionReader? paymentConditionReader)
        {
            Db = db;
            Representative = representative;
            Seller = seller;
            OtherSeller = otherSeller;
            Customer = customer;
            Address = address;
            PaymentTerm = paymentTerm;
            Product = product;
            PriceTableItem = priceTableItem;
            SellerPrincipal = CreateSellerPrincipal(seller.Id);

            var access = new SalesRepresentativeAccessService(db);
            var priceService = new PriceService(db);
            var eligibility = new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions
            {
                CardOnDeliveryEnabled = cardOnDeliveryEnabled
            }), paymentConditionReader);
            QuoteProtector = new SellerOrderDraftQuoteProtector(new EphemeralDataProtectionProvider());
            DraftService = new SellerOrderDraftService(db, access,
                new SellerCatalogService(db, access, priceService), priceService, eligibility);
            Service = new SellerOrderDraftSubmissionService(db, access,
                new OrderPlacementService(db, new CartService(db, priceService), priceService, eligibility, new OrderReservationService(db)),
                QuoteProtector,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SellerOrderDraftSubmissionService>.Instance);
        }

        public ApplicationDbContext Db { get; }
        public SalesRepresentative Representative { get; }
        public ApplicationUser Seller { get; }
        public ApplicationUser? OtherSeller { get; }
        public Customer Customer { get; }
        public CustomerAddress Address { get; }
        public PaymentTerm PaymentTerm { get; }
        public Product Product { get; }
        public PriceTableItem PriceTableItem { get; }
        public ClaimsPrincipal SellerPrincipal { get; }
        public SellerOrderDraftService DraftService { get; }
        public SellerOrderDraftSubmissionService Service { get; }
        public SellerOrderDraftQuoteProtector QuoteProtector { get; }

        public static ClaimsPrincipal CreateSellerPrincipal(string userId) => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")], "test"));

        public async Task<SellerOrderDraft> CreateDraftAsync(params (int ProductId, int Quantity, decimal Discount)[] lines)
        {
            var draft = new SellerOrderDraft
            {
                OwnerUserId = Seller.Id,
                SalesRepresentativeId = Representative.Id,
                CustomerId = Customer.Id,
                DeliveryAddressId = Address.Id,
                PaymentTermId = PaymentTerm.Id,
                RequestedDeliveryDate = DateTime.UtcNow.Date.AddDays(1),
                Notes = "Pedido do vendedor",
                Status = SellerOrderDraftStatus.Draft,
                Version = 1,
                Items = lines.Select(line => new SellerOrderDraftItem
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    RequestedDiscountAmount = line.Discount
                }).ToList()
            };
            Db.SellerOrderDrafts.Add(draft);
            await Db.SaveChangesAsync();
            return draft;
        }

        public async Task<string> CreateQuoteTokenAsync(ClaimsPrincipal user, SellerOrderDraft draft)
        {
            var ownerUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var prices = await new PriceService(Db).GetPricesAsync(
                draft.CustomerId, draft.Items.Select(item => item.ProductId).ToArray());
            var quoteLines = draft.Items.OrderBy(item => item.ProductId).Select(item =>
            {
                var unitPrice = prices.GetValueOrDefault(item.ProductId, item.Product?.PromotionalPrice ?? item.Product?.BasePrice ?? 0m);
                return new OrderPlacementQuoteLine(
                    item.ProductId,
                    item.Quantity,
                    unitPrice,
                    item.RequestedDiscountAmount,
                    unitPrice * item.Quantity);
            }).ToArray();
            var subtotal = quoteLines.Sum(item => item.Subtotal);
            var quote = new OrderPlacementQuote(
                draft.CustomerId,
                draft.DeliveryAddressId,
                draft.PaymentTermId,
                draft.RequestedDeliveryDate,
                quoteLines,
                subtotal,
                0m,
                subtotal);
            return QuoteProtector.Protect(ownerUserId, draft.Id, draft.Version, quote);
        }

        public async Task<SellerOrderDraftSubmissionResult> SubmitAsync(
            SellerOrderDraft draft,
            ClaimsPrincipal? user = null,
            int? expectedVersion = null,
            string? quoteToken = null)
        {
            user ??= SellerPrincipal;
            quoteToken ??= await CreateQuoteTokenAsync(user, draft);
            return await Service.SubmitAsync(user, draft.Id, expectedVersion ?? draft.Version, quoteToken);
        }

        public async Task<Product> AddAvailableProductAsync(string sku, decimal price, int stock = 10)
        {
            var product = new Product
            {
                Sku = sku,
                Name = $"Produto {sku}",
                Brand = "Orofoods",
                Unit = "caixa",
                BasePrice = 50m,
                MinimumCases = 1,
                IsActive = true,
                IsAvailable = true,
                ProductCategory = new ProductCategory { Name = $"Categoria {sku}", Slug = sku.ToLowerInvariant() }
            };
            var priceRow = new PriceTableItem { PriceTableId = Customer.PriceTableId!.Value, Product = product, Price = price };
            Db.AddRange(product, priceRow, new ProductInventory { Product = product, QuantityOnHand = stock });
            await Db.SaveChangesAsync();
            return product;
        }

        public static async Task<SubmissionFixture> CreateAsync(bool addSecondSeller = false, bool cardOnDeliveryEnabled = false,
            Orofoods.Web.Integrations.Erp.Wmc.IWmcCustomerPaymentConditionReader? paymentConditionReader = null)
        {
            var db = await TestDbContextFactory.CreateAsync();
            var representative = new SalesRepresentative { Name = "Vendedor A", IsActive = true };
            var seller = new ApplicationUser { Id = "seller-submit", UserName = "seller-submit", IsActive = true, SalesRepresentative = representative };
            ApplicationUser? otherSeller = addSecondSeller
                ? new ApplicationUser { Id = "seller-submit-other", UserName = "seller-submit-other", IsActive = true, SalesRepresentative = representative }
                : null;
            var table = new PriceTable { Name = "Tabela vigente", IsActive = true };
            var customer = new Customer
            {
                LegalName = "Cliente autorizado",
                TradeName = "Cliente autorizado",
                Cnpj = "11.111.111/0001-11",
                Status = CustomerStatus.Approved,
                IsActive = true,
                MinimumOrder = 1m,
                CreditLimit = 1000m,
                SalesRepresentative = representative,
                PriceTable = table
            };
            var address = new CustomerAddress
            {
                Customer = customer,
                Label = "Principal",
                Street = "Rua A",
                Number = "1",
                City = "Campinas",
                State = "SP",
                IsActive = true
            };
            var paymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, IsActive = true };
            var product = new Product
            {
                Sku = "SUBMIT-001",
                Name = "Produto disponível",
                Brand = "Orofoods",
                Unit = "caixa",
                BasePrice = 50m,
                MinimumCases = 1,
                IsActive = true,
                IsAvailable = true,
                ProductCategory = new ProductCategory { Name = "Categoria", Slug = "submission-category" }
            };
            var priceTableItem = new PriceTableItem { PriceTable = table, Product = product, Price = 40m, PromotionalPrice = 37m };
            db.AddRange(representative, seller, table, customer, address, paymentTerm, product, priceTableItem,
                new Orofoods.Web.Models.Commercial.OrderMinimumSettings { MinimumAmount = 1m });
            if (otherSeller is not null) db.Users.Add(otherSeller);
            await db.SaveChangesAsync();
            db.ProductInventories.Add(new ProductInventory { ProductId = product.Id, QuantityOnHand = 10 });
            await db.SaveChangesAsync();

            return new SubmissionFixture(db, representative, seller, otherSeller, customer, address, paymentTerm,
                product, priceTableItem, cardOnDeliveryEnabled, paymentConditionReader);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FixedWmcPaymentConditionReader(Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition condition)
        : Orofoods.Web.Integrations.Erp.Wmc.IWmcCustomerPaymentConditionReader
    {
        public Task<Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition?> GetForCustomerAsync(string customerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Orofoods.Web.Integrations.Erp.Wmc.WmcCustomerPaymentCondition?>(condition);
    }
}
