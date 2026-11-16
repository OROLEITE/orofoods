# Correção do updateConversation do CRM WhatsApp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Corrigir a incompatibilidade entre cards server-side e dinâmicos no polling do CRM WhatsApp.

**Architecture:** A view Razor rendera uma estrutura estável; o reconciliador JS trata nós opcionais com guards e encapsula cada conversa em um limite de erro técnico para continuar o lote. A suíte existente usa Jint para executar as funções extraídas do script com um DOM mínimo.

**Tech Stack:** ASP.NET Core Razor, JavaScript browser, C# xUnit/Jint.

**Spec:** `docs/superpowers/specs/2026-11-16-whatsapp-composer-update-conversation-design.md`

## Global Constraints

- Worktree isolada; preservar o checkout sujo atual.
- Sem PostgreSQL, banco, migrations, Azure, deploy, Staging ou Produção.
- Não alterar o contrato do endpoint `Updates`.
- Erros isolados devem ser registrados sem expor texto de mensagens, telefone ou dados do cliente.

## Task 1: Regressões de estrutura e continuidade

**Files:**
- Modify: `Orofoods.Web.Tests/Services/WhatsAppPollingUiTests.cs`

- [x] Adicionar cenários RED para card existente com horário, card existente sem horário/badge, card novo, atualização de preview/mensagem e uma conversa malformada seguida de outra válida.
- [x] Executar `dotnet test ... --filter FullyQualifiedName~WhatsAppPollingUiTests` e observar a falha causada por `time` nulo/loop interrompido.

## Task 2: Correção Razor/JavaScript

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Views/WhatsApp/Index.cshtml`
- Modify: `Orofoods.Web/wwwroot/js/whatsapp-composer.js`

- [x] Renderizar `<time>` sempre no card server-side.
- [x] Guardar elementos opcionais em `updateConversation`, mantendo atualização dos elementos presentes.
- [x] Isolar cada conversa em `updateConversations`, registrar falhas técnicas e continuar o loop.
- [x] Rodar os testes focados e confirmar GREEN.

## Task 3: Verificação final

- [x] Executar a suíte focada novamente, `git diff --check` e build/teste proporcional.
- [x] Confirmar que somente a view, o JavaScript e os testes/registros de plano foram alterados.
- [x] Reportar arquivos, diagnóstico e resultados; não publicar nem integrar a branch.

## Revisão solicitada: recuperação, tempo e meta visual

### Task 4: Testes RED de recuperação e contrato DOM

**Files:**
- Modify: `Orofoods.Web.Tests/Services/WhatsAppPollingUiTests.cs`

- [x] Testar substituição de card inválido sem duplicata, ordem preservada e atualização das conversas seguintes.
- [x] Testar que card cuja reconstrução falha é removido e pode ser tentado novamente no próximo ciclo.
- [x] Testar `time.textContent`/`dateTime` com e sem mensagem anterior.
- [x] Testar o HTML dinâmico e o conteúdo do meta, incluindo ícone e span de texto.
- [x] Executar a suíte focada e observar RED.

### Task 5: Recuperação e contrato visual

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/WhatsAppController.cs`
- Modify: `Orofoods.Web/Areas/Admin/Views/WhatsApp/Index.cshtml`
- Modify: `Orofoods.Web/wwwroot/js/whatsapp-composer.js`

- [x] Expor `lastInboundAt` no payload de polling.
- [x] Padronizar o meta com o mesmo ícone e `.whatsapp-conversation-meta-text`.
- [x] Recriar cards inválidos com `replaceWith`, removendo o original quando a recuperação falhar.
- [x] Adicionar IDs a `visibleIds` somente após sucesso.
- [x] Executar a suíte focada e confirmar GREEN.

### Task 6: Verificação solicitada

- [x] Executar testes focados, suíte completa, build Release, `node --check` e `git diff --check`.
- [x] Confirmar ausência de banco, Staging, Produção, migrations, deploy, push e merge.
