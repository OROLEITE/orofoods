# WMC Admin HTTP Audit Display Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Exibir no histórico WMC do Admin os dados HTTP e de reconciliação já gravados, sanitizados e com horários explícitos em UTC e Brasília.

**Architecture:** Manter a página de detalhes do pedido e sua autorização existente. Criar um formatador puro de auditoria para mascarar dados e converter `ExportedAt` de UTC para `America/Sao_Paulo`; a view renderiza os dados existentes com codificação Razor. Não adicionar chamadas externas, gravações, endpoints ou schema.

**Tech Stack:** ASP.NET Core MVC, Razor, C#, xUnit, CSS existente do Admin.

**Spec:** `docs/superpowers/specs/2026-10-29-wmc-admin-http-audit-display-design.md`

## Global Constraints

- Ler somente `WmcExportAudit` já carregado; não transmitir nem consultar o WMC.
- Não alterar banco, migrations, Azure, pedidos, marco 16, configurações do worker ou proteções contra duplicidade.
- Restringir a saída à área Admin protegida por `Administrador`.
- Não renderizar destino WMC, credenciais ou resposta sem sanitização; não usar `Html.Raw`.
- Manter `ExportedAt` imutável e identificá-lo como horário do registro da auditoria.
- Abrir PR para revisão; não mesclar.

## Review Focus

- Segredos em JSON, texto livre, cabeçalhos e parâmetros de URL devem ser mascarados — testar senha, serial, token e Authorization.
- Dados pessoais reconhecíveis em campos ou texto livre devem ser mascarados — testar e-mail, CPF/CNPJ e telefone.
- Status, corpos e erros ausentes devem renderizar um placeholder sem exceções — testar entrada nula/vazia.
- `DateTime` persistido como UTC deve converter para Brasília explicitamente — testar `2026-10-29 15:15 UTC` como `12:15 -03:00`.
- Resposta WMC com HTML deve permanecer codificada pelo Razor — testar que a view não usa `Html.Raw`.

---

### Task 1: Formatador de auditoria WMC

**Files:**
- Create: `Orofoods.Web/Services/Integrations/WmcAuditDisplayFormatter.cs`
- Test: `Orofoods.Web.Tests/Services/WmcAuditDisplayFormatterTests.cs`

**Interfaces:**
- Produces `WmcAuditDisplayFormatter.Sanitize(string?) -> string?` e `WmcAuditDisplayFormatter.FormatTimestamp(DateTime) -> WmcAuditTimestampDisplay`.
- `WmcAuditTimestampDisplay` contém textos UTC e Brasília com offset explícito.

- [x] Escrever testes para mascarar segredos/dados pessoais em JSON e texto livre, preservar texto de diagnóstico não sensível e formatar o instante UTC em Brasília.
- [x] Executar os testes focados e confirmar falha pelas APIs ausentes.
- [x] Implementar o formatador puro com redaction pattern-based para chaves sensíveis, Authorization/Bearer, e-mail, documentos fiscais e telefone; usar `America/Sao_Paulo`.
- [x] Executar novamente e confirmar aprovação.
- [x] Commitar o formatador e os testes focados.

```bash
git add Orofoods.Web/Services/Integrations/WmcAuditDisplayFormatter.cs Orofoods.Web.Tests/Services/WmcAuditDisplayFormatterTests.cs
git commit -m "feat(wmc): add safe audit display formatter"
```

### Task 2: Renderização no histórico protegido do pedido

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Views/Orders/Details.cshtml`
- Modify: `Orofoods.Web/wwwroot/css/admin-order-details.css`
- Test: `Orofoods.Web.Tests/Views/OrderWmcAuditViewTests.cs`

**Interfaces:**
- A view chama o formatador da Task 1 para as duas respostas, erros e `ExportedAt`.
- A autorização existente em `OrdersController` permanece sem alteração.

- [x] Escrever teste de contrato da view para status/resposta de exportação e reconciliação, ID/código do vendedor, timestamp duplo, sanitização e autorização Admin.
- [x] Executar o teste focado e confirmar falha pelos campos de saída ausentes.
- [x] Mostrar detalhes técnicos em seção expansível por auditoria, indicar ausências com placeholder e explicar que o timestamp é o do registro da auditoria.
- [x] Adicionar estilo responsivo e compatível com os temas atuais para respostas longas e texto monoespaçado.
- [x] Executar os testes focados e confirmar aprovação.
- [x] Commitar a renderização, estilos e teste de contrato.

```bash
git add Orofoods.Web/Areas/Admin/Views/Orders/Details.cshtml Orofoods.Web/wwwroot/css/admin-order-details.css Orofoods.Web.Tests/Views/OrderWmcAuditViewTests.cs
git commit -m "feat(admin): show WMC HTTP audit details"
```

### Task 3: Verificação e publicação

**Files:**
- Verify: diff completo, `Orofoods.Web.Tests` e build Release.

- [x] Executar todos os testes do projeto (1.779 aprovados, 2 ignorados por exigirem PostgreSQL) e o build Release (0 avisos, 0 erros), com pacotes do cache local.
- [x] Rever o diff e confirmar que não há alterações de integração, banco, Azure, worker ou retransmissão.
- [ ] Commitar, enviar a branch e abrir PR contra `main`; não mesclar.
