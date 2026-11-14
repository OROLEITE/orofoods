# Relatório — CRM WhatsApp em tempo real

## Escopo

Implementação local no worktree `fix/whatsapp-crm-auto-refresh`, baseado em `origin/main` no commit `bc85ae2c5696b7a3b38e982a310771c5b9795df2`. Não houve publicação, acesso a Produção/Staging, alteração de banco real ou mudança de Meta/WMC.

## Diagnóstico

- O webhook já persistia mensagens com deduplicação por `ExternalMessageId`, mas descartava `contacts[].profile.name`.
- O CRM dependia de redirect HTML após envio e de uma segunda consulta de polling; uma resposta perdida podia deixar a mensagem persistida invisível.
- O polling não atualizava uma caixa sem conversa selecionada; uma conversa nova sem `<time>` também interrompia a reconciliação.
- Mudanças de status/janela recarregavam a página e perdiam rascunho e posição de leitura.
- O sino era somente server-rendered. `ReadAt` e `UserId` já forneciam persistência e isolamento por usuário.
- O som era uma preferência local; “Atendimento” era um indicador estático sem ação exclusiva.

## Correções

- `Updates` retorna conversas, histórico, status e janela de 24 horas sem cache. O cliente tenta a cada 4 segundos, aborta requisições lentas, mostra falha/reconexão, reage a `online` e conserva o último estado válido.
- Mensagens outbound aparecem de imediato; a reconciliação correlaciona `ClientRequestId`, ordena por data/ID, impede downgrade de status e preserva uma tentativa após resposta perdida. O teste cobre normalização de espaços e reenvio idempotente sem duplicar bolha.
- A conversa, o rascunho, o scroll e os controles de janela/status são atualizados in-place; não há `window.location.reload()` no fluxo de polling.
- O sino consulta snapshot JSON automaticamente, atualiza contador/lista, não marca nada ao abrir, marca somente o item clicado, suporta “Marcar todas como lidas” e navega para a conversa local correspondente. Notificações continuam separadas de `UnreadCount`/status da conversa.
- O nome de perfil é normalizado, persistido em `WhatsAppConversations.ProfileName` e exibido com telefone como fallback. `CustomerId` continua sendo somente vínculo de cadastro e não é requisito para exibir o nome.
- Somente no CRM WhatsApp, a preferência “Som ligado” foi movida para o menu do sino. O cabeçalho não exibe mais o badge “Atendimento”; “Nova conversa” permanece a ação principal ciano `#0E7490` com texto branco.

## Migration criada — não executada

`Orofoods.Web/Data/MigrationsPostgreSql/20261114120956_AddWhatsAppContactProfileName.cs` adiciona a coluna nullable `ProfileName` (`character varying(256)`) em `WhatsAppConversations`, com designer e snapshot atualizados. A migration foi apenas gerada pelo EF Core usando a factory de design-time local; não foi aplicada em Produção ou Staging.

## Arquivos alterados

- Aplicação: `WhatsAppConversation`, `WhatsAppConversationService`, `WhatsAppController`, `NotificationsController`, `UserNotificationService`, `_NotificationBell`, `_SiteHeader`, `Index.cshtml`, `whatsapp-composer.js/.css`, `notification-bell.js/.css`.
- Dados: migration PostgreSQL, designer e `PostgreSqlApplicationDbContextModelSnapshot`.
- Testes: webhook/perfil, envio/deduplicação/janela/permissões, polling, notificações backend/UI, autorização, tema e testes de navegador Playwright (`Browser/whatsapp-live.cjs` e `Browser/notification-bell.cjs`).
- Documentação: plano e este relatório.

## Verificação

- `dotnet test Orofoods.slnx -c Release --no-restore`: **1954 aprovados, 2 ignorados, 0 falhas, 1956 total**.
- `dotnet build Orofoods.slnx -c Release --no-restore`: **0 erros**. Permanece o aviso preexistente `NU1510` sobre `System.Security.Cryptography.Xml` referenciado explicitamente.
- Playwright com Edge headless e rotas sintéticas: **passou** polling sem seleção, recebimento, envio, deduplicação, rascunho, scroll, reconexão, resposta perdida/whitespace e notificações (contador, no-auto-read, leitura individual, read-all, reload e som).
- Os dois testes ignorados são integrações PostgreSQL que exigem bancos descartáveis locais: `SellerOrderDraftSubmissionPostgresIntegrationTests.Concurrent_submissions_for_one_draft_create_one_order_history_and_inventory_reservation` e `WmcManualExportPostgresIntegrationTests.Confirmation_is_atomic_idempotent_under_race_and_rolls_back_when_audit_insert_fails`. Não foram executados para respeitar a restrição de não tocar bancos reais.

## NuGet e bloqueios

O restore padrão encontrou falha de acesso ao feed externo (`NU1801`/TLS) no cache configurado. A validação foi desbloqueada sem desabilitar TLS usando somente o cache local existente:

```powershell
$env:NUGET_PACKAGES = 'C:\Users\TarcisioSassi\.nuget\packages'
dotnet restore Orofoods.slnx --source 'C:\Users\TarcisioSassi\.nuget\packages' --ignore-failed-sources
```

Depois disso, build/testes foram executados com `--no-restore`. O bloqueio de infraestrutura externa permanece documentado; não houve downgrade de segurança nem alteração persistente de configuração NuGet.

## Limites da evidência

Os testes comprovam o comportamento do código e da UI sintética local. Não comprovam que o commit esteja publicado nem que Produção tenha recebido a migration. Antes de qualquer release, aplicar a migration somente mediante autorização operacional separada e validar o ambiente alvo; não executar migrations, seed, PostgreSQL, Firebird, Meta, WMC ou Azure como parte deste trabalho.
