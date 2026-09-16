# Seller Area Design

## Goal

Add a dedicated seller area where an active sales representative can work only with customers assigned to them, inspect commercial history, build customer-scoped carts, repeat previous orders, and confirm orders directly as `Received`.

## Scope

The seller area includes customer search, customer commercial summary, recent orders, frequent products, a customer-scoped catalog and cart, checkout, direct order confirmation, and repeat-order loading. It does not grant user administration, security administration, customer approval, price-table editing, or access to customers assigned to another seller.

The existing customer portal remains unchanged: customers continue to build and confirm their own carts. Seller-created orders use the same pricing, availability, minimum-order, credit, snapshot, numbering, and status-history rules as customer-created orders.

## Architecture

Create a separate MVC area named `Vendedor`. Its controllers and views remain independent from both the customer portal and the administrative area. This avoids conditional role logic in customer controllers and prevents seller navigation from exposing administrative functions.

Access is enforced by a `LinkedSalesRepresentative` authorization policy. The policy requires an authenticated, active `ApplicationUser` in the `Vendedor` role with an active `SalesRepresentative` link. A seller-scope service resolves the current representative and applies the representative ID, active customer status, and approved customer status to every customer query.

Business operations use focused services rather than trusting route or form IDs. Controllers receive customer and order IDs, but services confirm that each resource belongs to the current seller before reading or changing it.

## Components

### Authorization

- `LinkedSalesRepresentativeRequirement` and handler validate role, active user, linked representative, and active representative.
- `SalesRepresentativeAccessService` resolves the current representative and exposes seller-scoped customer and order queries.
- Administrators do not implicitly receive seller access. Administrative order management remains in the existing Admin area.

### Seller Dashboard

The dashboard lists only active, approved customers whose `SalesRepresentativeId` matches the current representative. Search covers trade name, legal name, CNPJ, responsible person, email, and phone. Each row shows company, city when available, contact, last order, and a link to the customer workspace.

The customer workspace shows commercial limits, price table, approved payment terms, active delivery addresses, recent orders, and frequent products. Empty states are shown when no orders or frequent products exist.

### Customer-Scoped Cart

Seller carts are isolated by seller and customer. Session keys include both IDs so switching customers cannot mix items. Cart operations reuse current product availability, `PriceService`, product minimum quantities, and customer minimum-order rules.

Loading a previous order into the cart recovers product IDs and quantities only. Current product records and prices are always queried again. Unavailable products are excluded and substitutes are presented when configured.

### Checkout And Order Creation

Checkout accepts only active delivery addresses and active payment terms assigned to the selected customer. Before persistence, the server recalculates all prices and subtotals, enforces product minimum quantities, customer minimum order, available credit, and at least one available item.

On success, the order:

- uses the selected customer;
- stores the authenticated seller user in `CreatedByUserId`;
- starts as `OrderStatus.Received`;
- stores product name, SKU, quantity, unit-price, and subtotal snapshots;
- receives the standard `ORO-{year}-{id}` number;
- creates the initial `OrderStatusHistory` event with the seller as author;
- clears only that seller/customer cart.

Persistence is atomic. A database transaction first obtains the generated order ID, then stores the final number before commit. A validation or persistence failure creates neither an order nor partial order items or history.

## Data Flow

1. The authenticated seller opens the seller dashboard.
2. The access service resolves the linked active representative.
3. The seller selects one of the scoped active, approved customers.
4. Catalog and history queries include both customer ownership and seller ownership constraints.
5. Cart commands write to a session bucket keyed by seller and customer.
6. Checkout rebuilds the cart from current database data and validates commercial rules.
7. A database transaction persists the order, snapshots, and initial status history, obtains the generated ID, stores the final number, and then commits.
8. The confirmation page links to the seller's customer workspace and order details.

## Error Handling

- Missing seller link, inactive user, inactive representative, or wrong role returns access denied.
- A customer not assigned to the current seller returns not found, avoiding disclosure that the customer exists.
- Pending, blocked, inactive, or reassigned customers become inaccessible immediately.
- Invalid addresses or payment terms produce validation errors and no persistence.
- Missing, inactive, or unavailable products are removed during revalidation; configured substitutes are shown.
- Minimum-order and credit failures return clear commercial messages while preserving the scoped cart.
- Repeating a missing or unauthorized order returns not found.

## User Interface

The seller area uses the established Orofoods visual language and shared responsive layout, with a dedicated navigation label and no links to user or security administration. Desktop tables become stacked cards on small screens. Forms include empty, validation, and unavailable-product states.

## Testing

Automated tests use SQLite in memory where practical and cover:

- active linked seller authorization;
- inactive user and inactive representative denial;
- customer isolation between two sellers;
- customer search constrained to the current representative;
- empty customer history;
- cart isolation by seller and customer;
- current-price and availability revalidation;
- repeat-order quantities without historical prices;
- allowed address and payment-term enforcement;
- product and customer minimum rules;
- credit-limit rejection;
- order snapshots and seller creator identity;
- initial `Received` status history;
- no partial persistence on validation failure.

The full existing test suite must continue to pass. A Release build and responsive browser smoke test complete verification.

## Migration And Compatibility

No new database entity is required for seller ownership because `ApplicationUser.SalesRepresentativeId`, `Customer.SalesRepresentativeId`, and `Order.CreatedByUserId` already represent the required relationships. The feature must not delete or rewrite existing orders, customers, users, carts, or status history.
