# WhatsApp Template Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Endurecer e validar o fluxo de templates oficiais existente no CRM WhatsApp com a menor alteração possível.

**Architecture:** Reutilizar `MetaWhatsAppBusinessGateway`, `WhatsAppController`, `WhatsAppConversationService` e o polling já existentes. A implementação será limitada à cópia do botão e a testes de contrato/integração local que fixam o comportamento autorizado, janela de atendimento e erros.

**Tech Stack:** ASP.NET Core MVC, EF Core com SQLite de teste, xUnit, `HttpMessageHandler` local, Jint, Razor, JavaScript e CSS existentes.

**Spec:** `docs/superpowers/specs/2026-11-16-whatsapp-template-hardening-design.md`

## Global Constraints

- Reutilizar integralmente a integração Meta existente.
- O texto visível da ação deve ser exatamente `Enviar mensagem modelo`.
- Não executar migrations, acessar/modificar PostgreSQL real, enviar mensagens reais, publicar ou fazer deploy.
- Não alterar Staging/Produção, não adicionar migrations e não refatorar fora do CRM WhatsApp.
- Preservar dark mode, botões azul-ciano, antiforgery, autorização, idempotência e histórico.

## Review Focus

- Novo número sem `LastInboundAt`: deve iniciar o fluxo de template e persistir o histórico — coberto na Task 1 por `New_number_can_send_an_approved_template_without_an_inbound_message`.
- Janela expirada: mensagem livre não pode chegar ao gateway e o template aprovado deve continuar disponível — coberto na Task 1 por `Expired_window_blocks_free_text_but_allows_an_approved_template`.
- Resposta inbound recente: deve atualizar a janela e liberar mensagem livre — coberto na Task 1 por `Inbound_reply_reopens_the_service_window_for_free_text`.
- Parâmetros obrigatórios e autorização: devem falhar sem chamada de envio — coberto na Task 1 por testes de parâmetros e vendedor fora do escopo.
- Falha HTTP da Meta: deve produzir resultado seguro sem expor corpo do erro — coberto na Task 1 por `Meta_template_send_maps_http_failure_without_exposing_response_body`.

### Task 1: Regression contracts for template hardening

**Files:**
- Modify: `Orofoods.Web.Tests/Services/WhatsAppCrmOperationsTests.cs`
- Modify: `Orofoods.Web.Tests/Services/WhatsAppThemeSurfaceTests.cs`

**Interfaces:**
- Consumes: `WhatsAppController.StartConversation`, `Send`, `SendTemplate`, `Templates`, `Updates`, `WhatsAppConversationService.ProcessAsync` and `MetaWhatsAppBusinessGateway`.
- Produces: executable regression tests proving the existing interfaces satisfy the authorized behavior.

- [ ] **Step 1: Write the failing tests**

  Add focused tests for new-number template sending, expired-window free-text blocking plus approved-template sending, inbound-window reopening, required header/body parameters, seller authorization, Meta HTTP failure mapping, and the exact `Enviar mensagem modelo` view label while retaining `whatsapp-primary-action`.

- [ ] **Step 2: Run the focused tests to verify the new contracts fail for the missing label and expose any real behavioral gap**

  Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~WhatsAppCrmOperationsTests|FullyQualifiedName~WhatsAppThemeSurfaceTests" -c Release -p:NuGetAudit=false -p:IsTestProject=true`

  Expected: the new label contract fails before implementation; existing behavior tests should pass unless a genuine hardening gap is found.

### Task 2: Minimal UI hardening

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Views/WhatsApp/Index.cshtml`

**Interfaces:**
- Consumes: the existing template catalog form and `.whatsapp-primary-action` theme contract.
- Produces: the exact visible action text `Enviar mensagem modelo`, without changing endpoint, form fields, CSS, JavaScript, or the send/persistence path.

- [ ] **Step 1: Change only the template-load action label**

  Replace `Consultar modelos aprovados` with `Enviar mensagem modelo`; preserve the form action, IDs, classes, note, parameter controls and the separate submit button.

- [ ] **Step 2: Run the focused tests and verify the contract is green**

  Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter "FullyQualifiedName~WhatsAppCrmOperationsTests|FullyQualifiedName~WhatsAppThemeSurfaceTests" -c Release -p:NuGetAudit=false -p:IsTestProject=true`

- [ ] **Step 3: Commit the scoped implementation**

  Run: `git add docs/superpowers/specs/2026-11-16-whatsapp-template-hardening-design.md docs/superpowers/plans/2026-11-16-whatsapp-template-hardening.md Orofoods.Web.Tests/Services/WhatsAppCrmOperationsTests.cs Orofoods.Web.Tests/Services/WhatsAppThemeSurfaceTests.cs Orofoods.Web/Areas/Admin/Views/WhatsApp/Index.cshtml; git commit -m "test: harden WhatsApp template flow"`

### Task 3: Full validation and scope review

**Files:**
- Verify: all files in the branch diff against `origin/main`.

**Interfaces:**
- Consumes: completed Tasks 1–2.
- Produces: local evidence suitable for review; no external side effects.

- [ ] **Step 1: Run the focused WhatsApp tests**

  Run the Task 2 command, including `-p:IsTestProject=true` so the .NET 10 SDK does not silently skip this project, and record the result.

- [ ] **Step 2: Run the full test suite and Release build**

  Run: `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release -p:NuGetAudit=false -p:IsTestProject=true`

  Run: `dotnet build Orofoods.Web/Orofoods.Web.csproj -c Release --no-restore -p:NuGetAudit=false`

- [ ] **Step 3: Validate JavaScript syntax and whitespace**

  Run: `node --check Orofoods.Web/wwwroot/js/whatsapp-composer.js` and `git diff --check`.

- [ ] **Step 4: Review scope and runtime safety**

  Confirm the diff contains no migration, model, seed, database, network, deployment, Staging or Production changes; confirm test HTTP calls use local handlers and no credential/string is printed.
