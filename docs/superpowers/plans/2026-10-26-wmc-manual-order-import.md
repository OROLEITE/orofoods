# WMC Manual Order Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar uma fila acessível a Administrador, Vendedor e Operador para importar manualmente um pedido aprovado ao WMC, com confirmação, auditoria e bloqueio idempotente.

**Architecture:** `OrderIntegrationService` reserva cada pedido aprovado antes da chamada HTTP e retorna o resultado selecionado. Uma fila compartilhada aplica acesso amplo a Administrador/Operador e o escopo de clientes do Vendedor. A importação usa `WmcJsonOrderErpIntegration`; `ManualImportEnabled` é falso por padrão e não altera o worker automático.

**Tech Stack:** ASP.NET Core MVC, EF Core, C#, xUnit, SQLite de testes.

**Spec:** `docs/superpowers/specs/2026-10-26-wmc-manual-order-import-design.md`

## Global Constraints

- Não transmitir pedidos reais, executar migrations, alterar banco/Azure ou fazer deploy.
- Não criar transmissor, fila ou regras comerciais paralelas.
- Não reenviar auditorias JSON/TXT `Started`, `Unknown`, `Succeeded`, `Generated`, `Available` ou `Imported`, nem estados WMC `Exported`/`Imported`.
- Manter Administrador, antiforgery, auditoria, `WmcTransmissionId` e reconciliação existentes.

## Review Focus

- Dois cliques simultâneos devem resultar em no máximo uma reivindicação HTTP.
- Uma resposta ambígua/erro de persistência deve bloquear nova tentativa automática.
- Uma falha determinística deve continuar visível e permitir tentativa segura.
- A confirmação deve incluir número, cliente e total, sem enviar antes do POST confirmado.
- O TXT manual e os pedidos já importados devem continuar fora da ação JSON.

### Task 1: Reivindicação e resultado do envio selecionado

**Files:**
- Modify: `Orofoods.Web/Services/Orders/OrderIntegrationService.cs`
- Test: `Orofoods.Web.Tests/Services/OrderIntegrationServiceTests.cs`

- [ ] Adicionar teste concorrente que prova uma única reivindicação e bloqueio para auditorias `Started`, `Unknown` e `Succeeded`.
- [ ] Alterar o núcleo para retornar `ErpOrderResult?`, mantendo `SendAsync` compatível e adicionando chamada selecionada para o controller.
- [ ] Executar testes focados.

### Task 2: Endpoint JSON compartilhado

**Files:**
- Create: `Orofoods.Web/Areas/Admin/Controllers/WmcOrderImportsController.cs`
- Create: `Orofoods.Web/Services/Orders/WmcOrderImportAccessService.cs`
- Test: `Orofoods.Web.Tests/Services/WmcOrderImportAccessServiceTests.cs`
- Test: `Orofoods.Web.Tests/Controllers/AdminWmcManualExportRecoveryTests.cs`

- [ ] Adicionar POST `ImportToWmc` para `Administrador,Vendedor,Operador`, com `[ValidateAntiForgeryToken]`, escopo de pedidos e mensagens claras.
- [ ] Usar o transmissor JSON concreto e não o serviço TXT; distinguir sucesso reconciliado, confirmação sem número, falha segura e resultado incerto.
- [ ] Executar testes de autorização/endpoint.

### Task 3: Elegibilidade, fila e confirmação

**Files:**
- Create: `Orofoods.Web/Areas/Admin/Views/WmcOrderImports/Index.cshtml`
- Modify: `Orofoods.Web/Views/Shared/_AdminNavigation.cshtml`
- Modify: `Orofoods.Web/Areas/Vendedor/Views/_SellerLayout.cshtml`
- Test: `Orofoods.Web.Tests/Controllers/WmcOrderImportsControllerTests.cs`
- Test: `Orofoods.Web.Tests/Views/WmcOrderImportViewTests.cs`

- [ ] Expor pedidos recebidos/aprovados; liberar ação somente para aprovado, integração pendente/falha, JSON/manual habilitados e sem estados/auditorias bloqueantes.
- [ ] Renderizar botão por linha com `confirm()` contendo número, cliente e valor; postar antiforgery ao endpoint.
- [ ] Mostrar referência WMC retornada na mensagem/monitor sem alterar o fluxo TXT.
- [ ] Executar contrato da view e testes do grid.

### Task 4: Verificação final

**Files:**
- No additional production files.

- [ ] Rodar testes focados, `dotnet build Orofoods.slnx -c Release --no-restore -v:minimal` e `git diff --check`.
- [ ] Revisar diff para confirmar ausência de migrations, configurações Azure e transmissão real.
- [ ] Commitar somente a integração JSON e os testes/documentos desta tarefa.
