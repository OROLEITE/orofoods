# Confirmação WMC: resumo e dados cadastrais — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corrigir a confirmação manual WMC para contar novos e existentes em categorias mutuamente exclusivas e exibir os dados cadastrais oficiais que serão persistidos.

**Architecture:** A regra de contagem ficará em uma fábrica pura de `WmcProductImportSummary`, recebendo candidatos consultados e seleção validada. A view de confirmação consumirá os campos existentes de `WmcProductImportCandidate`, `UnitProposal` e `Units`, sem alterar o serviço de importação ou criar valores derivados.

**Tech Stack:** .NET 10, ASP.NET Core MVC/Razor, C#, xUnit, EF Core SQLite de teste.

**Spec:** `docs/superpowers/specs/2026-11-25-wmc-confirmation-summary-details-design.md`

## Global Constraints

- Não modificar `WmcProductImportService`, leitor WMC, regras comerciais, preços, estoque, transação, rollback, idempotência ou bloqueio de inativos.
- Não inventar valores ausentes nem apresentar como persistido um campo que o serviço não grava.
- Preservar layout compacto, dark mode, tokens ciano e confirmação explícita de reimportação.
- Não executar importações reais, migrations, alterações de banco, sincronização automática, push, PR ou deploy.
- Preservar `WmcSync__Enabled=false`.

## Review Focus

- Produto novo com `Changes` vazio deve aparecer somente em `Novos`, nunca em `Existentes sem alterações` — teste em `WmcProductImportSummaryTests`.
- Seleção com novo, existente alterado e existente inalterado deve satisfazer a soma das categorias sem dupla contagem — teste no resumo e no controller.
- Campo WMC nulo/ausente não pode virar valor presumido na confirmação — teste de markup e propriedades de proposta.
- Unidade alternativa/master e códigos EAN/DUN devem ser exibidos a partir do snapshot oficial, sem alterar o serviço — teste de view e controller AVIAÇÃO `101900`.
- Produtos bloqueados/inativos continuam fora da seleção e são contabilizados separadamente — preservar e executar os testes existentes de seleção.

### Task 1: Resumo de confirmação com categorias mutuamente exclusivas

**Files:**
- Create: `Orofoods.Web.Tests/Models/WmcProductImportSummaryTests.cs`
- Modify: `Orofoods.Web/Models/Integrations/WmcProductPreview.cs`

**Interfaces:**
- Produces `WmcProductImportSummary.From(IEnumerable<WmcProductImportCandidate> candidates, IEnumerable<WmcProductImportCandidate> selected)`.
- Produces properties `Consulted`, `Selected`, `Blocked`, `NewProducts`, `ExistingWithChanges` e `ExistingWithoutChanges`.

- [ ] **Step 1: Write the failing tests**

  Add tests that build real `WmcProductImportCandidate` records and assert:

  - a new candidate with `ProductId == null` and no changes yields `NewProducts == 1`, `ExistingWithChanges == 0`, `ExistingWithoutChanges == 0`;
  - an existing candidate with changes and one without changes are separated;
  - `Consulted` and `Blocked` use the complete candidate collection, while `Selected` and the three selection categories use only the selected collection;
  - `NewProducts + ExistingWithChanges + ExistingWithoutChanges == Selected`.

- [ ] **Step 2: Run the focused tests and verify RED**

  Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~WmcProductImportSummaryTests"`.

  Expected: compilation/test failure because the factory and the new properties do not exist.

- [ ] **Step 3: Implement the pure summary factory**

  Update `WmcProductImportSummary` with the six explicit metrics and a static `From` method. Count `Blocked` as all candidates where `Selectable` is false; classify selected candidates by `ProductId` and `Changes.Count`, with no overlap.

- [ ] **Step 4: Run the focused tests and verify GREEN**

  Re-run the same filtered test command. Expected: all summary tests pass.

- [ ] **Step 5: Commit the task**

  Commit the model and tests with `fix(wmc): classify confirmation products exclusively`.

### Task 2: Controller integration and AVIAÇÃO confirmation data

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/IntegrationsController.cs`
- Modify: `Orofoods.Web.Tests/Controllers/WmcProductSelectionControllerTests.cs`

**Interfaces:**
- Consumes `WmcProductImportSummary.From` from Task 1.
- Produces confirmation models whose `Confirmation` metrics are calculated from all candidates and the validated selection.

- [ ] **Step 1: Write the failing controller tests**

  Extend the test reader with an active AVIAÇÃO row `101900` containing primary `UN`, master `CX`, official factors, EAN, DUN, `QTDE_EMB` and `QTDE_CONV`. Add tests that call `WmcConfirmSelection` and assert:

  - a selected new `101900` has `NewProducts == 1`, both existing categories equal zero, and `Consulted`/`Selected`/`Blocked` are distinct;
  - an existing candidate with changes and an existing candidate without changes populate the two existing categories without counting either as new;
  - the candidate retains the official `UnitProposal` and `Units` data used by confirmation.

- [ ] **Step 2: Run the controller tests and verify RED**

  Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~WmcProductSelectionControllerTests"`.

  Expected: failure because the controller still constructs the old summary directly and the new fixture assertions are unmet.

- [ ] **Step 3: Use the summary factory in `WmcConfirmSelection`**

  Replace the inline `new WmcProductImportSummary(...)` with `WmcProductImportSummary.From(candidates, selected)`. Do not change selection validation, reimport authorization, service calls or import behavior.

- [ ] **Step 4: Run the controller tests and verify GREEN**

  Re-run the focused controller command. Expected: all controller tests pass, including existing inactive/blocked/reimport protections.

- [ ] **Step 5: Commit the task**

  Commit with `fix(wmc): use exclusive confirmation summary`.

### Task 3: Compact confirmation presentation and contract coverage

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml`
- Modify: `Orofoods.Web/wwwroot/css/wmc-integration-center.css`
- Modify: `Orofoods.Web.Tests/Views/WmcProductPreviewViewTests.cs`

**Interfaces:**
- Consumes the six summary properties from Task 1 through `Model.Confirmation`.
- Consumes `WmcProductImportCandidate.UnitProposal`, `Units`, `PriceProposal`, `Changes` and existing product fields without adding new persistence fields.

- [ ] **Step 1: Write the failing view contract tests**

  Assert that confirmation markup contains `Consulted`, `Selected`, `Blocked`, `ExistingWithChanges` and `ExistingWithoutChanges`, labels the categories as `Existentes com alterações` and `Existentes sem alterações`, and renders the WMC fields `Unidade principal`, `Unidade master`, `Fator oficial`, `EAN`, `DUN`, `QTDE_EMB`, `QTDE_CONV`, `Unidades alternativas` and the actual `Units`/`UnitProposal` expressions. Assert that current-to-WMC differences remain present and that existing reimport authorization, dark tokens and ciano confirmation actions remain.

- [ ] **Step 2: Run the view tests and verify RED**

  Run `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~WmcProductPreviewViewTests"`.

  Expected: failure because the confirmation currently shows only price/source unit and generic changed/unchanged labels.

- [ ] **Step 3: Update the compact confirmation markup and styles**

  Replace the overlapping summary labels with the mutually exclusive categories and add consulted/selected/blocked counts. For every selected item, render current and proposed values only from the existing proposal objects, the complete official unit list, price proposal and service-generated changes. Use existing compact grid/list classes or narrowly scoped `wmc-confirmation-*` styles with dark variables and ciano accents.

- [ ] **Step 4: Run focused view tests and verify GREEN**

  Re-run the focused view command. Expected: all view contract tests pass.

- [ ] **Step 5: Run the complete verification**

  Run `git diff --check`, `dotnet build Orofoods.slnx --no-restore --configuration Release`, focused WMC tests, and `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore --configuration Release --logger "console;verbosity=minimal"`.

  Expected: no whitespace errors, build with 0 errors, focused tests pass, and the complete suite has 0 failures. No import, migration, database, push, PR or deploy command is executed.

- [ ] **Step 6: Commit the task**

  Commit with `feat(wmc): show official data in import confirmation`.
