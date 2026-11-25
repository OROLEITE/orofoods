# Confirmação explícita de reimportação WMC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan.

## Goal

Corrigir a autorização visual e o transporte da decisão de reimportação de produtos existentes na Central WMC, mantendo intacto o fluxo unificado e suas regras comerciais.

## Global constraints

- Worktree: `C:\Dev\Orofoods\.worktrees\wmc-reimport-confirmation`.
- Base: `origin/main` atual, commit `4ed38b6` no início do trabalho.
- Sem importações reais, migrations, alterações de bancos ou deploy.
- Não alterar regras de preços, unidades, estoque ou aplicação do serviço.

## Files and responsibilities

- `Orofoods.Web/Areas/Admin/Controllers/IntegrationsController.cs`: normalizar a autorização opcional do POST e encaminhar apenas um bool seguro.
- `Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml`: checkbox desmarcado, aviso de estado, resumo de alterações e confirmação visual antes do POST.
- `Orofoods.Web.Tests/Controllers/WmcProductSelectionControllerTests.cs`: autorização, ausência segura, seleção consistente e proteção de existentes.
- `Orofoods.Web.Tests/Views/WmcProductPreviewViewTests.cs`: contrato textual/estrutural da confirmação.
- `Orofoods.Web.Tests/Services/WmcProductImportServiceTests.cs`: somente se necessário para cobrir a diferença existente/inalterada sem mudar o serviço.

## Tasks

### 1. RED — testes da confirmação

Adicionar testes que falhem para:

1. Checkbox com texto exato, sem `checked`, aviso de autorização desligada e indicação visual do estado.
2. Resumo com “Com alterações” e “Sem alterações”, detalhes abertos e campos cadastrais/preços.
3. POST sem `reimportExisting` tratar a decisão como `false` e manter produto existente sem alteração.
4. POST com `true` seguir o fluxo autorizado, preservando `selectedProductKeys`.
5. Cenário com preço WMC igual ao preço local permanecer sem alteração.

Run focused tests and verify they fail for the missing contract, not due to setup.

### 2. GREEN — implementação mínima

Alterar somente controller/view para:

- aceitar valor ausente como recusa (`false`);
- rejeitar valores não booleanos/cancelamento legado conforme o contrato atual;
- renderizar checkbox sem seleção inicial;
- mostrar aviso quando desativado e confirmação textual quando ativado;
- exibir contadores e detalhes de alterações/inalterados;
- manter antiforgery, seleção oculta e revalidação backend.

Run focused tests and confirm all pass.

### 3. Verificação final

- `dotnet build Orofoods.slnx -c Release`
- testes focados com `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --filter ...`
- suíte completa com `dotnet test Orofoods.slnx`
- `git diff --check` e revisão do diff/status.

Commitar apenas a implementação, testes e estes documentos de especificação/plano. Não publicar branch nem abrir PR nesta etapa.

## Review focus

- Checkbox ausente nunca pode autorizar reimportação.
- `true` precisa alcançar o serviço sem ser convertido em `false` por model binding.
- A seleção não pode ser perdida entre confirmação e importação.
- Nenhuma regra comercial ou de preço deve ser duplicada na view/controller.
