# Orofoods Foundation and Authentication Design

## Objective

Establish the first production-oriented foundation for the Orofoods B2B portal while evolving the existing ASP.NET Core MVC application rather than rewriting it. This phase covers architecture, core commercial data model, Identity integration, B2B company approval, global roles, and the boundaries needed by later catalog, ordering, administration, and mobile work.

## Product context

Orofoods is a B2B ordering portal for food-service customers. The primary experience must optimize recurring purchasing: Login -> Buy again / quick order -> Review -> Checkout. Public institutional pages and commercial administration live in the same web application during the first phase.

## Approved technical decisions

- Keep SQLite for the first development phase.
- Keep a single `Orofoods.Web` project and evolve it as a modular monolith.
- Continue using ASP.NET Core MVC, Razor Views, Entity Framework Core, ASP.NET Core Identity and Bootstrap.
- Separate public MVC, `Areas/Customer`, and `Areas/Admin` concerns.
- Put business rules in focused services rather than controllers/views.
- A Customer represents a company, not a login.
- A company may have multiple user accounts.
- All users belonging to the same customer company have the same customer permissions in v1.
- Global roles remain Administrator, SalesRepresentative and Customer.
- New B2B registrations create a Pending customer.
- Pending customers may authenticate but may not access commercial prices or place orders.
- Approved customers receive catalog pricing and ordering access.
- Blocked or inactive customers cannot use commercial ordering features.

## Application structure

```text
Orofoods.Web
├── Areas
│   ├── Admin
│   │   ├── Controllers
│   │   ├── ViewModels
│   │   └── Views
│   └── Customer
│       ├── Controllers
│       ├── ViewModels
│       └── Views
├── Controllers
├── Data
│   ├── ApplicationDbContext.cs
│   ├── Migrations
│   └── SeedData.cs
├── Models
│   ├── Identity
│   ├── Customers
│   ├── Catalog
│   ├── Pricing
│   └── Orders
├── Services
│   ├── Customers
│   ├── Catalog
│   ├── Pricing
│   ├── Orders
│   └── Identity
├── ViewModels
├── Views
└── wwwroot
```

This is intentionally a modular monolith. Domain/Application/Infrastructure projects may be extracted later when API/mobile requirements justify the additional boundaries.

## Identity model

Introduce `ApplicationUser : IdentityUser`.

Core fields:

- `CustomerId?`: identifies the B2B company for users in the Customer role.
- `SalesRepresentativeId?`: optional association for seller accounts when needed.
- `IsActive`: permits account-level deactivation independently of the company.

A Customer user is authorized commercially only when both the account is active and its Customer is Approved. Customer-level commercial information (price table, credit, addresses, history) belongs to Customer, not ApplicationUser.

## Core entities

### Customer

Fields include legal/trade name, CNPJ, state registration, responsible contact, phone, WhatsApp, status, minimum order, credit limit/usage, price table, sales representative, creation/approval timestamps, and active state.

Statuses: Pending, Approved, Blocked, Inactive.

### CustomerAddress

Belongs to Customer and stores label, postal code, street, number, complement, district, city, state, primary flag, and active flag.

### ProductCategory

Stores name, slug, active flag, and sort order.

### Product

Stores SKU, name, category, brand, description, weight, unit, packaging/case quantities, base/promotional price, availability/featured/promotional flags, storage information, temperature, shelf life, ingredients, additional information, optional substitute product, and active state.

### ProductImage

Belongs to Product and stores URL, alt text, sort order, and primary flag.

### PriceTable / PriceTableItem

PriceTable groups commercial pricing. PriceTableItem associates a Product with its regular and optional promotional price. Customer references a PriceTable. This becomes the primary v1 pricing mechanism instead of per-customer prices.

### PaymentTerm / CustomerPaymentTerm

PaymentTerm represents PIX, boleto, transfer, and configured credit terms. CustomerPaymentTerm controls which terms are available to each company.

### Order / OrderItem

Order stores number, customer, creator, delivery address, requested delivery date, payment term, status, subtotal, freight, total, notes, creation and confirmation timestamps.

OrderItem stores product reference plus immutable commercial snapshots (`ProductNameSnapshot`, `SkuSnapshot`, `UnitPrice`) and quantity/subtotal. Historical orders must not change when the product catalog changes later.

Order statuses: Draft, Received, UnderReview, Approved, Picking, Invoiced, OutForDelivery, Delivered, Cancelled.

### OrderStatusHistory

Stores every order status transition with timestamp, optional acting user, and notes.

### FavoriteProduct

Customer/Product association for recurring purchasing.

### SavedOrder / SavedOrderItem

Named reusable order templates owned by Customer. SavedOrder records creator and creation time; items store product and quantity.

### SalesRepresentative

Stores optional Identity user association, name, phone, WhatsApp, email, and active state.

## Pricing rules

1. Anonymous/public users do not receive customer commercial pricing.
2. Pending/Blocked/Inactive customers do not receive ordering access.
3. Approved customers resolve product prices through their assigned PriceTable.
4. Product base price may exist for administration/demo purposes but customer-facing commercial price resolution goes through the assigned table.
5. Per-customer price exceptions are deferred until there is a demonstrated business requirement.

## Registration and approval flow

1. Visitor submits B2B company registration and first user credentials.
2. System creates Customer with Pending status.
3. System creates ApplicationUser linked to that Customer and assigns Customer role.
4. User can authenticate.
5. Commercial area checks Customer status.
6. Pending customer sees a registration-under-review screen.
7. Administrator reviews the company and can approve, block, or inactivate it.
8. On approval the administrator can assign price table, payment terms, credit limit, and sales representative.
9. Approved users gain catalog price and ordering access.

Additional users for the same company can be supported without changing Customer ownership of commercial data.

## User flows

### Customer

Public site -> Login -> company status gate -> Customer Dashboard -> Catalog / Quick Order / Buy Again -> Cart -> Checkout -> Confirmation -> Order tracking/history.

The dashboard prioritizes New Order, last order, open orders, monthly purchasing, and recently purchased products.

### Sales representative

Login -> customer search -> customer commercial profile/history/frequent products -> create order on behalf of customer -> cart -> checkout -> order created.

Seller access does not imply general administration privileges.

### Administrator

Admin Dashboard -> Orders / Products / Categories / Customers / Users / Sales Representatives / Price Tables / Payment Terms / Reports.

The customer approval workflow is a first-class administrative function.

## UI boundaries

### Public

Home, Products, Product Detail, About, Contact, Login, B2B Registration, Password Recovery, Privacy, Terms.

### Customer portal

Dashboard, Catalog, Quick Order, Cart, Checkout, My Orders, Order Detail, Buy Again, Favorites, Saved Orders, My Company, Addresses.

### Administration

Dashboard, Orders, Order Detail, Products, Categories, Customers, Users, Sales Representatives, Price Tables, Payment Terms, Reports.

Mobile web remains a priority: large quantity controls, fast search, persistent cart access, responsive navigation, and simplified checkout.

## Security and authorization

- ASP.NET Core Identity remains responsible for password hashing and account authentication.
- Use role authorization for Administrator, SalesRepresentative, and Customer.
- Add customer-status authorization/service checks for commercial operations.
- Preserve login lockout and server-side validation.
- Use anti-forgery protection on state-changing MVC forms.
- Do not trust posted CustomerId, price, totals, or authorization-sensitive values; resolve them server-side from the authenticated principal and database.
- Administrative changes should be designed to support audit logging.

## Migration strategy

The existing database already has early Customer/Product/Order entities. Replace/evolve these incrementally through EF Core migrations rather than deleting the application and starting again. During development, SQLite remains the provider. Entity mappings should avoid SQLite-specific domain assumptions so SQL Server migration remains straightforward later.

## Phase boundaries

This foundation phase does not implement fiscal issuance, ERP integration, advanced inventory, carrier integration, route optimization, WhatsApp API automation, or the native mobile application. It establishes the domain and authentication boundaries those features will consume later.

## Acceptance criteria

- Existing web project remains the application host.
- Application uses SQLite successfully after migrations.
- Identity uses ApplicationUser.
- Customer companies can own multiple users.
- Customer role users share company-level commercial permissions.
- B2B registration creates a Pending customer and linked user.
- Pending/Blocked/Inactive companies cannot access customer commercial ordering functions.
- Approved companies can enter the customer commercial area.
- Administrator, SalesRepresentative, and Customer roles are seeded/configured.
- Data model supports categories, product images, price tables, payment terms, order status history, favorites, saved orders, and sales representatives.
- Existing catalog/order functionality is migrated without silently changing historical order pricing.
