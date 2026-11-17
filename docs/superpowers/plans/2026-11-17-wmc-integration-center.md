# Central de Integração WMC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transformar a tela Admin de Importação WMC em uma Central de Integração com quatro abas, indicadores reais, fluxo manual preservado e comportamento responsivo.

**Architecture:** Manter `WmcPreviewPageModel`, os endpoints existentes e o componente de diagnóstico. A apresentação será reorganizada em uma view Razor com quatro painéis; um script dedicado cuidará apenas de estado visual local, busca, contadores, abas e bloqueio de duplo envio; o CSS existente receberá regras WMC escopadas e responsivas.

**Tech Stack:** ASP.NET Core MVC/Razor, C#, HTML semântico, JavaScript vanilla, CSS existente do Admin, xUnit por contratos de view.

**Spec:** `docs/superpowers/specs/2026-11-17-wmc-integration-center-design.md`

## Global Constraints

- Não alterar regras de negócio, endpoints, integração Firebird/WMC ou banco de dados.
- Não habilitar workers ou transmissões automáticas.
- Preservar integralmente o fluxo de importação manual e as permissões existentes.
- Reutilizar componentes e estilos do projeto.
- Não criar dados de demonstração, contadores fixos ou estados simulados.
- Ações POST continuam protegidas por antiforgery e confirmação explícita.
- A Visão Geral usa `WmcSyncCoordinator.LastRun` apenas como “Última sincronização manual”; importações diretas não serão misturadas com esse ciclo.

## Review Focus

- Estado de consulta sem marcas, falha de leitura ou rota que não consultou WMC deve aparecer como disponível, indisponível ou não consultado, respectivamente; cobrir no contrato de view/controller.
- Respostas pós-POST com `Model.Step` ou prévias devem abrir a aba do workflow e não esconder erros/seleções; cobrir com atributos de estado inicial na view.
- `diagnosticOrderId` inválido, inexistente e elegível devem manter o painel técnico e o formulário de consulta; preservar nos testes existentes do componente.
- Busca de marca/produto sem resultados e seleção de itens bloqueados não pode habilitar ações indevidas; cobrir nos contratos JS/markup e manter validação servidor existente.
- Duplo clique em confirmação/prévia não pode gerar dois submits; cobrir no script com teste de contrato e inspeção de `disabled`/estado de carregamento.

---

### Task 1: Criar contratos RED da Central

**Files:**
- Create: `Orofoods.Web.Tests/Views/WmcIntegrationCenterViewTests.cs`
- Modify: `Orofoods.Web.Tests/Views/WmcActivationDiagnosticsViewTests.cs` apenas se necessário para o novo wrapper expansível.

**Interfaces:**
- Consumes: arquivos atuais `Wmc.cshtml`, `Default.cshtml`, `admin-integrations.css`, `IntegrationsController.cs`.
- Produces: contratos que fixam as quatro abas, indicadores sem dados simulados, ações existentes, estado pós-POST, tokens, script e tokens cromáticos.

- [ ] **Step 1: Write the failing tests**

  Add focused tests that read the real source files and assert:

  - `Visão Geral`, `Clientes`, `Produtos` and `Diagnóstico` exist exactly as tab labels with `role="tab"`/`role="tabpanel"`.
  - The overview contains the real-state hooks for connection, manual coordinator state, and last sync totals, but no literal demo data.
  - Existing actions `WmcCustomerPreview`, `WmcCustomerImport`, `WmcCustomerRegularizationPreview`, `WmcCustomerRegularization`, `WmcProducts`, `WmcConfirmSelection`, `WmcImportProducts` and `WmcPreview` remain referenced.
  - Existing antiforgery and `WmcActivationDiagnostics` component remain present.
  - The view references `wmc-integration-center.js` and the CSS contains `#0E7490`, `#0B5D75`, dark tokens, responsive breakpoints and duplicate-submit selectors.

- [ ] **Step 2: Run tests to verify RED**

  Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WmcIntegrationCenterViewTests`

  Expected: FAIL because the four-tab contract and dedicated integration-center script/hooks do not yet exist.

- [ ] **Step 3: Commit the RED contracts**

  Run: `git add Orofoods.Web.Tests/Views/WmcIntegrationCenterViewTests.cs Orofoods.Web.Tests/Views/WmcActivationDiagnosticsViewTests.cs; git commit -m "test: define WMC integration center contracts"`

### Task 2: Add server-derived overview state and tab-safe Razor structure

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/IntegrationsController.cs` in existing WMC actions only.
- Modify: `Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml`.
- Test: `Orofoods.Web.Tests/Views/WmcIntegrationCenterViewTests.cs`.

**Interfaces:**
- Consumes: existing brand reads, `WmcPreviewPageModel`, `ViewBag.IsRunning`, `ViewBag.LastRun`, customer previews and existing actions.
- Produces: `ViewBag.WmcBrandQueryState` with `available`, `unavailable` or `not-queried`; `data-wmc-initial-tab`/server-state hooks; four tab panels containing unchanged form action names and model fields.

- [ ] **Step 1: Implement the minimal state transport**

  Set `ViewBag.WmcBrandQueryState` only from existing `wmcBrandReader.GetAllAsync` outcomes: `available` after success, `unavailable` in the existing catches, and `not-queried` for actions that render without a brand query. Do not add a new reader call, endpoint or persistence.

- [ ] **Step 2: Restructure `Wmc.cshtml`**

  Add the compact header and four accessible tabs. Put the current customer forms in Clientes, the current three-step product flow plus the existing multi-brand `WmcPreview` form in Produtos, and the existing diagnostic component in a collapsible Diagnóstico panel. Add a Visão Geral with cards derived only from `WmcBrandQueryState`, `IsRunning`, `LastRun` and model counts; show explicit “Não consultado”/“Nenhuma sincronização” states rather than invented values.

  Preserve every existing input name, `asp-action`, antiforgery token, validation summary, confirmation field, `simulationValidated`, `reimportExisting`, `selectedProductCodes`, role-protected controller action and result details.

- [ ] **Step 3: Run focused tests to verify GREEN for server/view contracts**

  Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~WmcIntegrationCenterViewTests|FullyQualifiedName~WmcProductPreviewViewTests|FullyQualifiedName~WmcCustomerImportViewTests|FullyQualifiedName~WmcActivationDiagnosticsViewTests"`

  Expected: PASS with existing WMC flow contracts preserved.

- [ ] **Step 4: Commit the server/view structure**

  Run: `git add Orofoods.Web/Areas/Admin/Controllers/IntegrationsController.cs Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml Orofoods.Web.Tests/Views; git commit -m "feat: organize WMC admin center into tabs"`

### Task 3: Implement client-side behavior without changing business flow

**Files:**
- Create: `Orofoods.Web/wwwroot/js/wmc-integration-center.js`.
- Modify: `Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml` only for script inclusion/data hooks.
- Test: `Orofoods.Web.Tests/Views/WmcIntegrationCenterViewTests.cs`.

**Interfaces:**
- Consumes: data attributes emitted by the Razor view and existing product/customer rows.
- Produces: tab selection, keyboard navigation, instant text filters, visible/selected counters, pagination/filter updates already present in the page, loading labels and idempotent submit handling.

- [ ] **Step 1: Add failing script contracts**

  Extend the focused view test to require `data-wmc-tabs`, `aria-selected`, `data-wmc-loading`, `data-wmc-submit-once`, search/filter hooks, and the external script path.

- [ ] **Step 2: Run RED**

  Run the same filtered WMC view test command; expected FAIL for absent script/data hooks.

- [ ] **Step 3: Implement the script**

  Use one page-scoped initializer. Server workflow state wins over hash; otherwise hash selects a tab, then Visão Geral is the default. Support arrow/Home/End tab keyboard navigation and focus the active panel. Filter brand/product/customer rows instantly and update `aria-live` counters. On submit, disable only the submitting form’s submit controls, add a loading label/class, and ignore subsequent submits while preserving native validation. Do not issue fetch/XHR or alter form targets.

- [ ] **Step 4: Run focused tests and inspect script syntax**

  Run the focused view tests and `node --check Orofoods.Web/wwwroot/js/wmc-integration-center.js`.

- [ ] **Step 5: Commit the client behavior**

  Run: `git add Orofoods.Web/wwwroot/js/wmc-integration-center.js Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml Orofoods.Web.Tests/Views/WmcIntegrationCenterViewTests.cs; git commit -m "feat: add WMC center interaction states"`

### Task 4: Apply compact dark responsive styling and expandable diagnostics

**Files:**
- Modify: `Orofoods.Web/wwwroot/css/admin-integrations.css`.
- Modify: `Orofoods.Web/Areas/Admin/Views/Shared/Components/WmcActivationDiagnostics/Default.cshtml`.
- Test: `Orofoods.Web.Tests/Views/WmcIntegrationCenterViewTests.cs`, `WmcActivationDiagnosticsViewTests.cs`.

**Interfaces:**
- Consumes: existing Admin dark variables, WMC classes, component model fields and diagnostics values.
- Produces: visual contracts for dark surfaces, cyan buttons, compact cards, responsive tabs/grids/tables, focus states and a collapsible technical panel.

- [ ] **Step 1: Add failing style/component contracts**

  Assert exact cyan values `#0E7490` and `#0B5D75`, existing dark tokens, card/border/radius rules, mobile/tablet media queries, visible focus styles, and `details`/`summary` wrapping the diagnostic technical content without removing `diagnosticOrderId`, eligibility fields, or exclusion reasons.

- [ ] **Step 2: Run RED**

  Run the focused view test command; expected FAIL for new selectors and expandable wrapper.

- [ ] **Step 3: Implement CSS and diagnostic markup**

  Scope all new rules under `.admin-wmc-page`. Reuse `--admin-dark-*` tokens, make primary buttons white on `#0E7490` with `#0B5D75` hover, keep tables scrollable at narrow widths, collapse grids at tablet/mobile breakpoints, and preserve readable contrast/focus. Wrap the technical diagnostic body in an accessible expandable panel while retaining all existing server-rendered values and the GET form.

- [ ] **Step 4: Run focused tests and diff check**

  Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~WmcIntegrationCenterViewTests|FullyQualifiedName~WmcActivationDiagnosticsViewTests|FullyQualifiedName~WmcProductPreviewViewTests|FullyQualifiedName~WmcCustomerImportViewTests"`; then `git diff --check`.

- [ ] **Step 5: Commit styling and diagnostics**

  Run: `git add Orofoods.Web/wwwroot/css/admin-integrations.css Orofoods.Web/Areas/Admin/Views/Shared/Components/WmcActivationDiagnostics/Default.cshtml Orofoods.Web.Tests/Views; git commit -m "style: polish WMC integration center"`

### Task 5: Full verification and visual smoke check

**Files:**
- Modify: none unless verification exposes a scoped defect.

- [ ] **Step 1: Review the complete diff**

  Run: `git diff origin/main...HEAD --stat` and `git diff origin/main...HEAD -- Orofoods.Web/Areas/Admin/Controllers/IntegrationsController.cs Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml Orofoods.Web/Areas/Admin/Views/Shared/Components/WmcActivationDiagnostics/Default.cshtml Orofoods.Web/wwwroot/css/admin-integrations.css Orofoods.Web/wwwroot/js/wmc-integration-center.js`.

- [ ] **Step 2: Build Release**

  Run: `dotnet build Orofoods.Web/Orofoods.Web.csproj -c Release --no-restore`.

  Expected: exit code 0 with zero errors.

- [ ] **Step 3: Run the full relevant test project**

  Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore`.

  Expected: exit code 0 and zero failed tests; report any pre-existing warnings separately.

- [ ] **Step 4: Validate local browser presentation if available**

  Start only the local app using the project’s existing safe local configuration, verify the Admin page at desktop/tablet/mobile widths, switch all four tabs, exercise search/filter/loading states and inspect the diagnostic expansion. Do not use Staging, Production, real WMC transmissions or remote credentials.

- [ ] **Step 5: Final diff check**

  Run: `git diff --check` and `git status --short --branch`. Report the isolated worktree path, exact verification results, and any browser limitation without claiming unobserved visual correctness.
