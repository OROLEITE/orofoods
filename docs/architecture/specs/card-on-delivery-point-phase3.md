# Cartão na entrega + Mercado Pago Point — especificação da Fase 3

- **Status:** proposta técnica para revisão; desenho aprovado, implementação ainda não iniciada.
- **Branch de trabalho:** `feature/card-on-delivery-mercado-pago-point`.
- **Escopo:** provider Point, conciliação, proteção contra duplicidade, testes fake e integração oficial somente com credenciais de teste.
- **Fora do escopo:** Staging, Production, Azure, deploy, migration aplicada, credenciais produtivas, terminal físico produtivo, WMC e dinheiro real.

## 1. Estado conhecido

A Fase 2 está implementada localmente neste worktree, sem commit ou deploy. A árvore contém alterações não commitadas da Fase 2 e sua migration PostgreSQL ainda não foi aplicada. `Payment` já é a unidade persistida de tentativa e contém identificadores externos, chave de idempotência e `DriverPaymentTerminalAssignmentId`; não existe entidade `PaymentAttempt` separada. `IPointPaymentProvider` existe e o registro atual usa `DisabledPointPaymentProvider`.

As opções Point hoje contêm somente `Enabled`. A integração online Mercado Pago já usa Orders API, `HttpClient`, autenticação Bearer, `X-Idempotency-Key`, exceções sanitizadas, mapper e reconciliação por webhook assinado. O payload online é `type=online` e não será reutilizado como payload Point.

Não foi encontrada credencial Point de teste na configuração local previamente verificada. A autorização recebida permite somente credenciais de usuário/aplicação de teste e o dispositivo virtual `SBX0000001`; não autoriza conta ou token produtivo.

## 2. Decisão de arquitetura

Implementar `MercadoPagoPointPaymentProvider` atrás de `IPointPaymentProvider`. O provider terá contratos Point próprios, mas reutilizará a infraestrutura HTTP compartilhável de Mercado Pago onde isso não misturar payloads, credenciais ou status online e presencial. O registro padrão continua desabilitado. A configuração Point terá ambiente explícito `Test`, credencial de teste, segredo de webhook de teste e timeout; valores secretos virão apenas de configuração segura não versionada. Nesta fase, a inicialização rejeitará uma configuração habilitada fora de `Test`.

O dispositivo virtual será configurável no cadastro de terminal e validado como `SBX0000001` nos testes remotos. O identificador enviado à API será composto por um `poi_type` válido no Brasil e pelo device virtual, por exemplo `NEWLAND_N950__SBX0000001`. Não haverá valor padrão que selecione terminal físico produtivo. Store e POS continuam metadados locais de configuração/validação do terminal; não serão serializados no payload de criação Point, cujo contrato documenta `config.point.terminal_id`.

## 3. Fluxo de cobrança

1. Uma ação autenticada de Admin na tela de detalhes do pedido solicita a cobrança. A ação fica indisponível por padrão e o serviço valida a flag e o ambiente `Test` no servidor.
2. O servidor carrega o pedido, o pagamento Cartão na Entrega, valor atual, motorista ativo, associação motorista-terminal ainda ativa, terminal ativo e device. Nenhum ID, valor ou status enviado pelo browser é fonte de verdade.
3. A operação serializável por pedido consulta/cria a tentativa ativa, associa o snapshot `DriverPaymentTerminalAssignmentId`, fixa valor, referência externa e chave idempotente, e persiste antes da chamada HTTP. A primeira tentativa reutiliza a linha pendente criada no checkout; uma nova tentativa somente pode ser aberta depois de um estado final que permita nova cobrança. Tentativas anteriores e suas referências externas permanecem intactas.
4. A chamada de criação usa `type=point`, `external_reference` não PII, valor monetário do pedido e `config.point.terminal_id`, com `X-Idempotency-Key`. Store/POS não são enviados como campos adicionais. A chamada externa ocorre fora da transação de banco. Em timeout ou resposta perdida, a repetição usa a mesma tentativa e chave; não cria nova tentativa automaticamente.
5. A resposta de criação persiste os IDs externos, sem marcar `Approved`. Uma resposta ambígua mantém a tentativa recuperável por chave idempotente e reconciliação.
6. A consulta de status e o webhook assinado reconciliam o estado autoritativo do provider, conferindo order ID, referência e valor. Só `processed` com detalhe de pagamento confirmado `accredited` pode aprovar. A conclusão `Delivered` continua bloqueada no backend até o status interno aprovado.

Como a Fase 2 não possui atribuição motorista-pedido, a tela de cobrança exigirá que um Admin selecione uma associação motorista-terminal ativa. O backend revalida motorista, terminal e associação no momento da persistência e guarda o ID histórico da associação no pagamento. Isso valida uma associação operacional ativa, mas não prova que esse motorista é o responsável de uma rota/pedido; criar domínio de rota/atribuição de pedido fica fora desta fase e é uma limitação operacional explícita.

## 4. Tentativas, idempotência e concorrência

`Payment` continua sendo a tentativa. Uma chave idempotente gerada pelo servidor é gravada antes da rede e única no banco. Uma tentativa ativa por pedido é definida pelos estados `Pending`, `Processing` e `ActionRequired`. Repetições do mesmo comando retornam/reconciliam essa tentativa. Não se inicia uma nova cobrança enquanto houver tentativa ativa ou resultado ambíguo.

Criação e validação da tentativa usam transação serializável no agregado do pedido e tratam conflito previsível como resposta controlada, sem HTTP 500. Será avaliada uma constraint/índice parcial único por pedido para tentativas ativas. Só será gerada migration PostgreSQL adicional se a garantia não puder ser comprovada com os índices e transações existentes e os testes de concorrência; qualquer migration gerada ficará sem aplicação. A migration PostgreSQL da Fase 2 também permanece sem aplicação.

Antes da chamada remota, a transação verifica novamente que a associação escolhida está ativa e que motorista e terminal estão ativos. A associação histórica fica referenciada no pagamento mesmo que seja encerrada depois. Se a atribuição mudar durante a chamada, a tentativa continua vinculada ao snapshot persistido; a resposta do provider não troca motorista/terminal retroativamente.

## 5. Estados Point

| Status Mercado Pago Point | Estado interno | Regra |
|---|---|---|
| `created` | `Pending` | Order aceita/criada; não é aprovação. |
| `at_terminal` | `Processing` | Aguardando interação/processamento no terminal. |
| `action_required` | `ActionRequired` | Ação no terminal ainda necessária; não é aprovação nem terminal para nova cobrança. |
| `processed` + pagamento `accredited` | `Approved` | Confirmação autoritativa que pode disparar o handler existente uma única vez. |
| `failed` | `Rejected` | Resultado final recusado. |
| `canceled` | `Cancelled` | Resultado final cancelado. |
| `expired` | `Expired` | Resultado final expirado. |
| `refunded` | `Refunded` | Só permitido como avanço após aprovação/processamento. Sem estorno automático. |
| desconhecido/inconsistente | sem alteração | Registrar tipo/código sanitizado e exigir reconciliação; nunca aprovar nem inventar equivalência. |

Transições são monotônicas. Atualizações `Pending` não podem regredir `Processing` ou `ActionRequired`; estados finais não podem regredir; a única mudança permitida após `Approved` é `Refunded`. Webhook não usa status recebido para atualizar diretamente: valida assinatura e reconsulta a order. Consultas concorrentes e webhooks duplicados usam a mesma transação/regras de transição; o efeito de aprovação ocorre uma vez.

`PaymentStatus.ActionRequired` será acrescentado sem renumerar membros existentes. Como o enum é persistido numericamente, seu novo valor será anexado ao final. Não há mudança de schema apenas por esse novo membro.

## 6. Webhook

Reutilizar `POST /api/v1/webhooks/mercadopago`; não criar endpoint paralelo. O validador aceitará as chaves de assinatura configuradas para os fluxos online e Point, validando assinatura HMAC, request ID, data ID e tolerância existente. A configuração Point usa segredo da aplicação de teste, sem alterar secrets de Staging/Azure. Após autenticação, a reconciliação localiza o `Payment` pelo Gateway/order ID e escolhe o cliente/provider correto. Dados do corpo servem somente para identificar a notificação, nunca para aprovar, definir valor ou atualizar pagamento.

IDs desconhecidos e indisponibilidade transitória mantêm as respostas/retries controlados existentes. Logs registram correlation IDs, tipo e resultado sanitizado; nunca Authorization, segredo, token, payload sensível ou resposta bruta.

## 7. Operação e autorização

- A cobrança é ação de Admin autorizada server-side; uma chamada direta sem role é negada.
- `MercadoPagoPointEnabled=false` por padrão em todo ambiente versionado.
- Ativar exige configuração explícita local de ambiente `Test`; qualquer ambiente Staging/Production rejeita o uso, mesmo que a flag seja ligada acidentalmente.
- UI exibe estados “criando cobrança”, “aguardando terminal”, “ação necessária”, “aprovado”, “recusado/erro”, sem habilitar fluxo operacional quando desabilitado.
- Somente pagamento confirmado como `Approved` libera a transição normal para `Delivered`.
- CASH / À vista e demais métodos permanecem inalterados. WMC permanece fora do escopo.

## 8. Testes

### Unitários com HTTP fake

- payload `type=point`, terminal ID virtual e valor formatado; Store/POS permanecem fora do JSON enviado;
- Bearer presente sem validar/imprimir o valor do token;
- `X-Idempotency-Key` persistente por tentativa;
- criação, consulta, cancelamento, parse de order/payment IDs e status;
- respostas malformadas, 4xx, 5xx, rate limit, timeout, cancelamento e erro de rede sem vazar corpo/credenciais;
- configuração bloqueia execução fora de `Test` e flags desligadas não chamam HTTP.

### Serviço, controller, webhook e concorrência

- validação de pedido, método, status, valor, motorista, associação atual, terminal e device;
- primeira chamada e retries após timeout/resposta perdida reaproveitam a mesma tentativa/chave;
- double click e duas requisições simultâneas não geram duas orders;
- troca/encerramento da associação concorrente conserva snapshot ou aborta antes da chamada;
- evento duplicado não duplica aprovação/efeitos;
- webhook e polling em corrida não produzem regressão;
- evento antigo depois de estado final não altera o resultado;
- estados de sucesso, falha, cancelamento, expiração, `action_required` e refund;
- autorização Admin e entrega bloqueada até `Approved`;
- testes garantem zero chamadas externas quando fake é usado.

### Integração oficial, somente se houver credenciais de teste

Usar exclusivamente credenciais Test do usuário/aplicação autorizado, `SBX0000001`, Store/POS compatíveis e nenhuma conta/token produtivo. Criar uma order teste e usar o endpoint oficial de simulação de status, com polling de intervalo curto e timeout total limitado (até 60s para `action_required`), sem sleeps fixos longos. Validar `created`, `at_terminal`, `processed`, `failed`, `canceled`, `expired`, `action_required` e `refunded` quando permitido pelo estado anterior. Confirmar recebimento de webhook apenas se a URL de callback de teste já estiver disponível e autorizada; não criar túnel nem alterar Azure. Credenciais ausentes resultam em `BLOCKED: MERCADO PAGO TEST CREDENTIALS REQUIRED` para esta etapa remota, mantendo os testes locais.

## 9. Migrações e dados

Reutilizar os campos atuais de `Payment` e a associação criada na Fase 2. Não aplicar migrations. Acrescentar apenas o valor final `ActionRequired` ao enum, sem renumerar status. Nenhuma migration Fase 3 é planejada inicialmente; se a revisão de concorrência provar necessidade de índice/coluna, apresentar a justificativa e a migration PostgreSQL separadamente, sem executar.

As credenciais de teste não serão gravadas em Git, appsettings versionado, arquivos temporários ou logs. Não ler ou modificar banco remoto, Key Vault, App Settings ou Azure nesta fase.

## 10. Entregáveis e critérios de conclusão

- Provider Point real implementado atrás da abstração existente, com registro padrão disabled.
- Reuso da Orders API e do webhook assinado sem duplicar endpoint.
- Mapeamento de status documentado e testado; só confirmação `processed/accredited` aprova.
- Proteção contra repetição, concorrência, timeout e eventos duplicados/fora de ordem.
- Referência motorista/terminal/associação histórica preservada no Payment.
- Guarda de entrega continua efetiva.
- Restore, build Release, testes Point/Pagamento/webhook/concorrência, suíte completa e `git diff --check` aprovados.
- Feature flags continuam `false`.
- Sem commit, push, deploy, alteração Azure, execução de migration ou ação Production.
- Chamadas externas reais de teste: zero enquanto credenciais seguras não estiverem disponíveis; se disponíveis, número e cenários registrados sem IDs/segredos sensíveis.

Ao final da Fase 3, reportar provider, infraestrutura reutilizada, disponibilidade de credenciais, chamadas externas, cobertura dos status, concorrência/idempotência, vínculo histórico, guarda Delivered, migrations e testes, conforme o relatório pedido na especificação original.

## 11. Revisão de consistência

- A criação da order não aprova o pagamento; `Delivered` continua bloqueado pelo estado interno.
- Fase 2 e migrations existentes são preservadas; não há mudança remota.
- O ponto de configuração distingue flag funcional e ambiente, evitando chamada em Staging/Production.
- Timeout não abre nova cobrança; a tentativa e chave já persistidas permitem recuperar a mesma operação.
- A lacuna de atribuição pedido-motorista está declarada: nesta fase um Admin seleciona uma associação ativa e o pagamento guarda o snapshot; relação formal de rota/pedido não é inferida.
- Existe uma restrição de contrato a validar durante implementação: a resposta `processed` deve trazer pagamento confirmado e detalhe `accredited`; qualquer formato diferente falha fechado e não aprova.
- Integração de callback externo permanece dependente de URL já disponível; nenhum túnel ou mudança Azure está autorizado.
