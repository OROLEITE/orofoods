# Orofoods Institutional Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the responsive Orofoods institutional site with public products, contact capture and legal pages without exposing B2B prices.

**Architecture:** `HomeController` owns anonymous routes. `ContactMessage` is saved through `ApplicationDbContext`; public product queries only use active records and never use `PriceService`. Razor views reuse the shared layout and existing visual system.

**Tech Stack:** ASP.NET Core MVC 10, Razor, EF Core 10, SQLite, xUnit, Bootstrap 5, CSS3.

**Spec:** `docs/superpowers/specs/2026-08-29-institutional-site-design.md`

## Global Constraints

- Preserve the existing gold, graphite and cream identity, typography and mobile breakpoints.
- Do not resolve or show commercial prices in anonymous pages.
- Contact posting requires anti-forgery, server validation and privacy consent.
- Migration must be additive. No email, WhatsApp, CRM or paid integration is in scope.

---

### Task 1: Contact Domain and Persistence

**Files:**
- Create: `Orofoods.Web/Models/Contact/ContactMessage.cs`
- Create: `Orofoods.Web/ViewModels/ContactViewModel.cs`
- Modify: `Orofoods.Web/GlobalUsings.cs`
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs`
- Create: `Orofoods.Web.Tests/Models/ContactViewModelTests.cs`
- Create: `Orofoods.Web.Tests/Models/ContactMessageTests.cs`
- Create: `Orofoods.Web/Data/Migrations/<timestamp>_AddContactMessages.cs`
- Modify: `Orofoods.Web/Data/Migrations/ApplicationDbContextModelSnapshot.cs`

**Interfaces:** Adds `DbSet<ContactMessage> ContactMessages`, `ContactViewModel`, and `ContactMessage`.

- [ ] **Step 1: Write failing model tests**

```csharp
[Fact]
public void Contact_view_model_requires_privacy_consent()
{
    var input = new ContactViewModel { Name = "Ana", Email = "ana@burger.com", Phone = "19", City = "Campinas", Message = "Contato", AcceptPrivacyPolicy = false };
    Assert.Contains(Validate(input), x => x.MemberNames.Contains(nameof(ContactViewModel.AcceptPrivacyPolicy)));
}

[Fact]
public void Contact_message_sets_creation_time()
{
    Assert.InRange(new ContactMessage().CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
}
```

- [ ] **Step 2: Verify RED**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~Contact"`

Expected: FAIL because the types do not exist.

- [ ] **Step 3: Implement models, context and migration**

```csharp
public class ContactViewModel
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [StringLength(120)] public string Company { get; set; } = "";
    [Required, StringLength(30)] public string Phone { get; set; } = "";
    [StringLength(30)] public string WhatsApp { get; set; } = "";
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = "";
    [Required, StringLength(80)] public string City { get; set; } = "";
    [Required, StringLength(1500)] public string Message { get; set; } = "";
    [Range(typeof(bool), "true", "true")] public bool AcceptPrivacyPolicy { get; set; }
}
```

`ContactMessage` has `Id`, the corresponding string fields with identical max lengths, `PrivacyConsentAt`, and `CreatedAt = DateTimeOffset.UtcNow`. Add its DbSet and mapping. Generate migration with `dotnet tool run dotnet-ef migrations add AddContactMessages --project .\Orofoods.Web --startup-project .\Orofoods.Web`; inspect `Up` to confirm it only creates `ContactMessages`.

- [ ] **Step 4: Verify GREEN and commit**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~Contact"`

Expected: PASS. Commit the domain, context, migration and tests as `feat: persist institutional contact requests`.

### Task 2: Public Product Routes

**Files:**
- Modify: `Orofoods.Web/Controllers/HomeController.cs`
- Create: `Orofoods.Web/ViewModels/PublicProductCatalogViewModel.cs`
- Create: `Orofoods.Web.Tests/Controllers/HomeControllerProductTests.cs`

**Interfaces:** Adds `Products(string? search, string? category)` and `Product(int id)`. The catalog model exposes `Products`, `Categories`, `Search`, and `Category`.

- [ ] **Step 1: Write failing route tests**

```csharp
[Fact]
public async Task Products_returns_only_active_products_matching_search()
{
    await using var db = await TestDbContextFactory.CreateAsync();
    db.Products.AddRange(new Product { Sku = "BRI-01", Name = "Pão Brioche", IsActive = true }, new Product { Sku = "OLD-01", Name = "Produto Antigo", IsActive = false });
    await db.SaveChangesAsync();
    var result = await new HomeController(db).Products("brioche", null);
    Assert.Single(Assert.IsType<PublicProductCatalogViewModel>(Assert.IsType<ViewResult>(result).Model).Products);
}

[Fact]
public async Task Product_returns_not_found_when_inactive()
{
    await using var db = await TestDbContextFactory.CreateAsync();
    db.Products.Add(new Product { Sku = "OLD-01", Name = "Produto Antigo", IsActive = false });
    await db.SaveChangesAsync();
    Assert.IsType<NotFoundResult>(await new HomeController(db).Product(1));
}
```

- [ ] **Step 2: Verify RED**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~HomeControllerProductTests"`

Expected: FAIL because actions and catalog model do not exist.

- [ ] **Step 3: Implement active-only catalog**

```csharp
var query = db.Products.AsNoTracking().Where(x => x.IsActive)
    .Include(x => x.ProductCategory).Include(x => x.Images);
if (!string.IsNullOrWhiteSpace(search))
    query = query.Where(x => x.Name.Contains(search) || x.Sku.Contains(search) || x.Brand.Contains(search));
if (!string.IsNullOrWhiteSpace(category))
    query = query.Where(x => x.ProductCategory!.Slug == category);
```

`Products` returns sorted products and active categories. `Product` uses the predicate `x => x.IsActive && x.Id == id` and returns `NotFound()` otherwise. Never use price fields or `PriceService`.

- [ ] **Step 4: Verify GREEN and commit**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~HomeControllerProductTests"`

Expected: PASS. Commit as `feat: add public product showcase routes`.

### Task 3: Contact Posting and Static Institutional Actions

**Files:**
- Modify: `Orofoods.Web/Controllers/HomeController.cs`
- Create: `Orofoods.Web.Tests/Controllers/HomeControllerContactTests.cs`

**Interfaces:** Adds GET/POST `Contact`, GET `About`, `HowItWorks`, `Terms`, and preserves public `Privacy`.

- [ ] **Step 1: Write failing contact behavior tests**

```csharp
[Fact]
public async Task Contact_post_returns_view_for_invalid_model()
{
    await using var db = await TestDbContextFactory.CreateAsync();
    var controller = CreateController(db);
    controller.ModelState.AddModelError(nameof(ContactViewModel.AcceptPrivacyPolicy), "required");
    Assert.IsType<ViewResult>(await controller.Contact(new ContactViewModel()));
    Assert.Empty(await db.ContactMessages.ToListAsync());
}

[Fact]
public async Task Contact_post_persists_valid_message()
{
    await using var db = await TestDbContextFactory.CreateAsync();
    var input = new ContactViewModel { Name = "Ana", Email = "ana@burger.com", Phone = "19", City = "Campinas", Message = "Contato", AcceptPrivacyPolicy = true };
    Assert.IsType<RedirectToActionResult>(await CreateController(db).Contact(input));
    Assert.NotEqual(default, (await db.ContactMessages.SingleAsync()).PrivacyConsentAt);
}
```

- [ ] **Step 2: Verify RED**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~HomeControllerContactTests"`

Expected: FAIL because `Contact` does not exist.

- [ ] **Step 3: Implement actions**

```csharp
[HttpGet("/contato")]
public IActionResult Contact() => View(new ContactViewModel());

[HttpPost("/contato")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Contact(ContactViewModel input)
{
    if (!ModelState.IsValid) return View(input);
    db.ContactMessages.Add(new ContactMessage { Name = input.Name, Company = input.Company, Phone = input.Phone, WhatsApp = input.WhatsApp, Email = input.Email, City = input.City, Message = input.Message, PrivacyConsentAt = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync();
    TempData["ContactSuccess"] = "Recebemos sua mensagem. Nossa equipe retornará em breve.";
    return RedirectToAction(nameof(Contact));
}
```

- [ ] **Step 4: Verify GREEN and commit**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~HomeControllerContactTests"`

Expected: PASS. Commit as `feat: capture institutional contact messages`.

### Task 4: Public Views, Navigation and Responsive Styling

**Files:**
- Create: `Orofoods.Web/Views/Home/Products.cshtml`
- Create: `Orofoods.Web/Views/Home/Product.cshtml`
- Create: `Orofoods.Web/Views/Home/About.cshtml`
- Create: `Orofoods.Web/Views/Home/HowItWorks.cshtml`
- Create: `Orofoods.Web/Views/Home/Contact.cshtml`
- Create: `Orofoods.Web/Views/Home/Terms.cshtml`
- Modify: `Orofoods.Web/Views/Home/Index.cshtml`
- Modify: `Orofoods.Web/Views/Home/Privacy.cshtml`
- Modify: `Orofoods.Web/Views/Shared/_Layout.cshtml`
- Modify: `Orofoods.Web/wwwroot/css/site.css`
- Create: `Orofoods.Web.Tests/Views/InstitutionalViewTests.cs`

**Interfaces:** Views consume Task 1/2 models. `TempData["ContactSuccess"]` displays the successful contact state.

- [ ] **Step 1: Write failing view contract test**

```csharp
[Fact]
public void Contact_view_contains_antiforgery_form_and_privacy_link()
{
    var markup = File.ReadAllText(Path.Combine(ProjectRoot, "Orofoods.Web", "Views", "Home", "Contact.cshtml"));
    Assert.Contains("asp-antiforgery=\"true\"", markup);
    Assert.Contains("asp-action=\"Privacy\"", markup);
}
```

- [ ] **Step 2: Verify RED**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~InstitutionalViewTests"`

Expected: FAIL because `Contact.cshtml` does not exist.

- [ ] **Step 3: Implement view contracts**

`Products` provides search, category filter, cards and empty state. `Product` shows product identity, packaging, availability and optional storage/ingredients but no price. `Contact` has anti-forgery, validation messages, privacy checkbox/link and success alert. `About`, `HowItWorks`, `Privacy`, and `Terms` contain initial Orofoods-specific copy. Update home CTAs, header and footer routes; add mobile rules at `900px` and `600px` for filters, detail, forms and legal copy.

- [ ] **Step 4: Verify GREEN, smoke test and commit**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~InstitutionalViewTests"`

Expected: PASS.

Run: `dotnet run --project .\Orofoods.Web\Orofoods.Web.csproj`

Check `/`, `/produtos`, `/produtos/1`, `/contato`, `/sobre`, `/como-funciona`, `/privacidade`, `/termos` at `375px` and `1440px`: collapsed navigation, stacked filters, usable form fields, readable cards, no commercial prices. Commit as `feat: complete Orofoods institutional site`.

### Task 5: Full Verification and Migration Safety

- [ ] **Step 1: Run all tests**

Run: `dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --no-restore`

Expected: PASS.

- [ ] **Step 2: Build**

Run: `dotnet build Orofoods.slnx`

Expected: PASS with zero errors.

- [ ] **Step 3: Validate migration against a clean SQLite database**

```powershell
$phase2TempDb = Join-Path (Get-Location) 'phase2-temp.db'
$env:ConnectionStrings__DefaultConnection = "Data Source=$phase2TempDb"
dotnet tool run dotnet-ef database update --project .\Orofoods.Web\Orofoods.Web.csproj --startup-project .\Orofoods.Web\Orofoods.Web.csproj --context ApplicationDbContext
Remove-Item -LiteralPath $phase2TempDb -Force
Remove-Item Env:ConnectionStrings__DefaultConnection
```

Expected: migrations apply, then temporary database is removed.

- [ ] **Step 4: Final inspection**

Run: `git diff --check` and `git status --short`. Commit only any verification fix as `fix: verify institutional site`.
