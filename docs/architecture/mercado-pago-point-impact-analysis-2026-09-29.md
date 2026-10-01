# Análise de impacto: Mercado Pago Point e Cartão na entrega

**Data:** 2026-09-29

**Decisão de negócio:** aprovada; ver [ADR Cartão na entrega](decisions/ADR-2026-09-29-card-on-delivery-mercado-pago-point.md).

**Estado desta etapa:** levantamento e documentação somente. Nenhum código, banco, migration, configuração, Staging ou Production foi alterado.

## 1. Arquitetura atual do Mercado Pago

A implementação atual é Checkout Transparente online via Orders API. O browser usa o SDK JavaScript Mercado Pago para tokenizar o cartão; o servidor recebe token e identificador do meio e cria order online `type=online` com `processing_mode=automatic`. PIX e cartão são orquestrados pelo mesmo serviço. A integração não cria atualmente uma `Payment Preference`; as cobranças online usam `POST /v1/orders`.

O retorno é salvo como uma tentativa `Payment`, com chave de idempotência e referências do gateway. O webhook público valida assinatura e consulta a order no Mercado Pago antes de reconciliar o status. Boleto/recebíveis têm serviço separado.

## 2. Arquivos envolvidos e classificação

| Classificação | Arquivos/componentes | Relação com a decisão |
|---|---|---|
| **REUSE** | `Orofoods.Web/Services/Payments/IBoletoProvider.cs`, `PendingBoletoProvider.cs`, `PaymentService.cs` | Recebíveis e boleto a prazo permanecem separados do novo fluxo Point. |
| **REUSE / ADAPT** | `Orofoods.Web/Services/Payments/IPaymentGateway.cs`, `PaymentOrchestrationService.cs` | Fronteira gateway, persistência e idempotência são reutilizáveis; orquestração precisa iniciar cobrança Point somente na entrega. |
| **ADAPT** | `MercadoPagoPaymentGateway.cs`, `MercadoPagoApiModels.cs`, `MercadoPagoStatusMapper.cs` | Hoje representam orders online. Precisarão suportar payload, destino/terminal e estados Point sem misturar os contratos online e presencial. |
| **REUSE / ADAPT** | `MercadoPagoWebhookSignatureValidator.cs`, `IMercadoPagoWebhookSignatureValidator.cs`, `MercadoPagoWebhooksController.cs` | Validação de origem e endpoint são reaproveitáveis; ação/tipos/estados Point e conciliação precisam ser confirmados. |
| **ADAPT** | `Orofoods.Web/Models/Payments/Payment.cs`, `PaymentStatus.cs`, `PaymentMethodType.cs`, `Orofoods.Web/Models/Orders/Order.cs` | Já há pagamento por tentativa e enums distintos de pedido e pagamento; falta representar claramente cartão presencial e terminal/motorista. |
| **ADAPT / REMOVE_LATER** | `Controllers/PortalController.cs`, `Views/Portal/Checkout.cshtml`, `wwwroot/js/checkout.js`, `ViewModels/CheckoutViewModel.cs` | O fluxo atual coleta dados no browser, cria token e inicia cartão online. Esse caminho deverá deixar de ser usado para cartão após a migração controlada. PIX não é abrangido por esta decisão. |
| **ADAPT / REMOVE_LATER** | `Areas/Vendedor/Controllers/CheckoutController.cs`, `Areas/Vendedor/Views/Checkout/Index.cshtml`, `ViewModels/SellerCheckoutViewModels.cs` | O checkout assistido também aceita token de cartão e chama o gateway; precisa de definição de escopo e adaptação para não cobrar online indevidamente. |
| **ADAPT** | `Controllers/Api/V1/PaymentsController.cs`, `ViewModels/PaymentViewModels.cs` | `POST /api/v1/payments/card` inicia atualmente tentativa online de cartão. No futuro, deve ser substituído ou delimitado por uma ação de cobrança autorizada na entrega. O endpoint PIX pode continuar separado. |
| **ADAPT** | `Services/Customers/PaymentEligibilityService.cs`, `Services/Orders/OrderPlacementService.cs`, `Services/Orders/AssistedOrderService.cs` | A seleção de condições hoje combina meios e prazos. Será necessário distinguir Cartão na entrega de CASH e preservar a validação comercial de cliente/endereço/pedido. |
| **ADAPT** | `Services/Orders/AdminOrderService.cs`, `Areas/Admin/Controllers/OrdersController.cs`, views Admin de pedidos | A atualização de `OrderStatus` não verifica o status de pagamento; a regra de bloqueio da conclusão e eventual exceção auditada ainda não existem. |
| **REUSE / ADAPT** | `Services/Sellers/SellerOrderHistoryService.cs`, views Portal/Admin/Vendedor de pedido | Já há apresentação de alguns estados de pagamento no histórico do Vendedor; contexto e telas precisam cobrir pendência Point e operação de entrega. |
| **UNKNOWN** | `WmcOrderFileGenerator.cs`, `OrderIntegrationService.cs` | O gerador de arquivo WMC não serializa hoje uma condição de pagamento explícita. A forma de representar cartão presencial continua pendente de contrato comercial com WMC. |
| **UNKNOWN** | Subsistema Driver/Delivery/Route | Não foram encontrados modelos, área, controllers ou serviços de motorista/rota no projeto. A tela e a autorização do operador dependerão de desenhar esse subsistema ou identificar integração externa. |
| **ADAPT** | `MercadoPagoOptions.cs`, registro em `Program.cs`, opções `MercadoPago` | A configuração atual prevê Access Token, Public Key e Webhook Secret; Point exigirá credencial compatível com a conta e configuração segura. Não foi inspecionado nem alterado valor de secret. |
| **REUSE / ADAPT** | `Data/MigrationsPostgreSql/20260917192904_AddMercadoPagoPaymentInfrastructure.cs` e snapshot PostgreSQL | A migration já adicionou campos de gateway, ordem/pagamento externo, referência, idempotência, metadados não sensíveis e suporte a múltiplas tentativas. Novas relações Point podem exigir migration adicional. |
| **ADAPT** | `docs/superpowers/specs/2026-09-17-mercado-pago-phase-1-design.md`, `docs/superpowers/plans/2026-09-17-mercado-pago-phase-1.md` | São o desenho/plano técnico da integração atual online. O ADR novo supersede a parte de cartão online no checkout; o suporte a PIX/recebíveis e demais decisões permanece sujeito ao texto existente. |

### Branches examinadas

- Branch atual: `feature/orofoods-ui-customer-portal-release`, commit `7a8af95`.
- A branch local `feature/mercado-pago-phase-1` aponta para `e751535`, é ancestral da branch atual e está 13 commits à frente de `origin/feature/mercado-pago-phase-1`.
- A árvore da feature contém os componentes Mercado Pago listados acima. O checkout assistido do Vendedor e a proteção de idempotência do checkout foram acrescentados depois da base original da feature e também estão na árvore atual.
- A árvore de trabalho já tinha alterações locais em arquivos de login e `.vscode/`; foram preservadas.

### Configuração e feature flags

`MercadoPagoOptions` contém `AccessToken`, `PublicKey`, `WebhookSecret`, `BaseAddress` e expiração de PIX. `Program.cs` faz binding da seção `MercadoPago` e registra o gateway e serviços. Os valores sensíveis não foram lidos nem transcritos. Não foi encontrada feature flag específica para habilitar/desabilitar cartão online; a opção é selecionada pelas condições disponíveis e pelos fluxos de checkout.

## 3. Modelo atual de Payment

`Payment` representa uma tentativa de pagamento ligada a `Order` e `Customer`. Tem método e valor, status, IDs externos, `Gateway`, `GatewayOrderId`, `GatewayPaymentId`, `ExternalReference`, `IdempotencyKey`, timestamps e alguns metadados não sensíveis. A migration existente removeu a unicidade de `OrderId` e tornou a chave de idempotência única, permitindo várias tentativas por pedido.

`PaymentStatus` contém estados legados (`Pending`, `Issued`, `Paid`, `Overdue`, `Cancelled`, `Failed`) e estados de gateway (`Processing`, `Approved`, `Rejected`, `Refunded`, `Expired`). `OrderStatus` é separado e já inclui `OutForDelivery` e `Delivered`. O status de pagamento está na entidade `Payment`; não há propriedade persistida `PaymentStatus` no `Order`.

`PaymentMethodType` atual contém `Legacy`, `Pix`, `CreditCard`, `Cash` e `Boleto`; ainda não contém um tipo Point/Cartão na entrega. `Order.PaymentMethod` é texto associado ao nome da condição escolhida. A condição `CREDIT_CARD` é usada hoje pelo checkout online. O pedido é inicialmente criado como `Received`; PIX/cartão geram tentativa após a criação do pedido. Não há criação de tentativa pendente Point no pedido normal.

## 4. Fluxo atual de checkout

No Portal do Cliente, o POST valida carrinho, endereço, condição, elegibilidade e preço, chama `AssistedOrderService`/`OrderPlacementService` para criar o pedido e reserva o estoque. Para PIX ou `CREDIT_CARD`, em seguida chama `PaymentOrchestrationService`. No cartão, o browser obtém token, método e parcelas com o SDK Mercado Pago; nenhum número/CVV é enviado ao Orofoods. Se o gateway recusa/falha em condições tratadas como definitivas, o pedido é cancelado e a reserva liberada.

O checkout do Vendedor também cria o pedido e inicia PIX/cartão online pelo mesmo orquestrador. O checkout assistido de Admin usa a criação comercial existente e precisa ser mantido em escopo ao definir quais usuários poderão selecionar Cartão na entrega.

## 5. O que pode ser reaproveitado

- Entidade `Payment`, múltiplas tentativas, chave de idempotência e referências externas como base de conciliação.
- Separação entre `OrderStatus` e `PaymentStatus`; estados existentes incluem `Pending` e `Approved`.
- Serviço de gateway via `HttpClient`, DTOs, isolamento do contrato externo e mapeador de status, adaptados a Point.
- Assinatura do webhook, comparação constante, consulta posterior ao provedor e proteção contra notificações duplicadas/regressivas.
- `OrderStatusHistory`, trilha de pagamentos e componentes de status do pedido.
- Regras comerciais de cliente aprovado, endereço ativo, preço, estoque e condições elegíveis.
- PIX e boleto devem permanecer isolados e só mudar se outra decisão solicitar.

## 6. O que precisará mudar

- Introduzir um conceito distinto `CARD_ON_DELIVERY`; não codificá-lo como `CASH` nem reinterpretar silenciosamente o `CREDIT_CARD` existente.
- Criar pedido sem chamada de cobrança de cartão e manter pagamento pendente. A equipe deverá definir se a tentativa `Payment` nasce junto do pedido ou se o status pendente será derivado de outro registro.
- Trocar a coleta/tokenização de cartão por texto informativo no checkout. Remover os campos/SDK apenas depois que todos os fluxos de cartão online forem desativados de forma controlada.
- Criar ação explícita “Cobrar na maquininha” autorizada para motorista/operador ligado ao pedido e terminal. Validar servidor-side pedido, valor, atribuição, estado e idempotência; tratar timeout como resultado ambíguo e bloquear segunda cobrança automática.
- Adicionar Store/POS/device/terminal e vínculo operacional. Uma tentativa não deverá poder ser enviada a um terminal arbitrário fornecido pelo browser.
- Adaptar mapper, persistência, webhook e reconciliação para estados Point. A confirmação deve validar assinatura, order/transação externa, referência e valor antes de atualizar o pagamento para `Approved`.
- Bloquear `Delivered` para cartão pendente/recusado/expirado. Se uma exceção administrativa for aprovada no futuro, exigir motivo, identidade do administrador e histórico/auditoria específica.
- Definir cancelamento antes da cobrança sem criar order Point e manter refund como fluxo separado, sem estorno automático.

## 7. Impacto no banco

Existe uma tabela `Payments` com campos de gateway e uma migration PostgreSQL de infraestrutura Mercado Pago já registrada. Ela é parcialmente reutilizável: não cobre a associação de motorista/rota a terminal, nem todas as referências operacionais necessárias para Point. `Orders` tem `PaymentMethod` textual e condição de pagamento; atualmente não guarda motorista nem terminal.

Não foi acessado o banco nesta análise. Não há confirmação de quais migrations estão implantadas em cada ambiente. Nenhuma linha, schema ou migration foi alterada.

## 8. Possíveis migrations futuras

Dependendo da decisão de modelagem, avaliar:

1. Código comercial distinto `CARD_ON_DELIVERY` ou separação explícita entre método de pagamento e condição/prazo; manter `CASH` e dados históricos inalterados.
2. Persistência de vínculo Driver–Point terminal, incluindo identificadores do Mercado Pago (Store, POS, device/terminal) e estado ativo. Como não existe entidade Driver no projeto, a chave estrangeira e o limite do agregado ainda não podem ser fechados.
3. Campos ou tabela de conciliação para resultado Point, referência da transação, método retornado, terminal, motorista, horários de solicitação/confirmação e status detalhado, reutilizando os IDs já existentes quando suficiente.
4. Auditoria para exceções administrativas de entrega sem pagamento, caso essa exceção seja posteriormente aprovada.
5. Índices únicos/consultas para idempotência, IDs externos e webhooks duplicados, evitando unicidade que impeça tentativas legítimas após falha.

Antes de gerar qualquer migration futura, apresentar tipos, nulabilidade, defaults, índices, FKs, estratégia de backfill e SQL PostgreSQL. Não gerar ou executar agora.

## 9. Impacto no Portal Cliente

O checkout deve exibir “Cartão na entrega” e a instrução “Pague no momento da entrega diretamente na maquininha”. Não mostrar campos de cartão nem tokenizar. O resumo/sucesso e histórico devem deixar claro que o pedido foi criado e o pagamento ainda está pendente, e manter PIX e condições já existentes quando aplicáveis. A idempotência de pedido existente deve permanecer compatível com repetição/refresh sem criar cobrança antecipada.

## 10. Impacto no Admin

Admin precisa ver método comercial, status de pagamento, IDs e histórico de conciliação sem dados sensíveis de cartão. A tela de detalhes e a ação atual de atualização de status precisam respeitar a regra de entrega. Administração de motoristas e terminais não existe hoje; qualquer tela de vínculo exige permissão, validação e auditoria. Regras de override continuam pendentes, não presumidas.

## 11. Impacto no sistema de entregas/motorista

Há estados `OutForDelivery` e `Delivered`, mas não foram encontrados modelos, área, controllers ou serviços de Driver, Route ou terminal atribuído. Também não existe ação do motorista para cobrança. Portanto, a tela de entrega proposta não é uma extensão pequena de uma tela existente: depende de identificar um sistema externo ou desenhar a capacidade de motorista, atribuição de pedido/rota, autenticação e autorização.

## 12. Impacto no WMC

O gerador atual de arquivo WMC constrói cabeçalho, cliente, itens e trailer, sem serializar a condição de pagamento. O código atual não define se cartão presencial será enviado como cartão de crédito, outra condição ou não será exportado até a confirmação. O WMC também precisa esclarecer criação/importação do pedido, título e conciliação financeira. Essa é uma decisão pendente; os mapeamentos citados no desenho anterior são documentação e não foram encontrados como implementação neste gerador.

## 13. Impacto em webhooks

O controller atual aceita notificações de order, valida `x-signature`/`x-request-id`/`data.id`, consulta `GET /v1/orders/{id}`, valida valor e referência externa e aplica transição idempotente. O fluxo conceitual é adequado para Point. Será necessário tratar o formato `type=point`, eventos e estados atuais Point (`order.processed`, `order.canceled`, `order.refunded`, `order.action_required`, `order.failed`, `order.expired`) e confirmar quais estados de order e transação mapeiam para `PaymentStatus`.

Não confiar no payload para aprovar pagamento; não atualizar pagamento/order fora da transação correspondente; manter idempotência e proteção de replays. A documentação atual do Mercado Pago recomenda Orders notifications e assinatura secreta por aplicação. [Documentação de notificações Point](https://www.mercadopago.com.br/developers/pt/docs/mp-point/notifications)

## 14. Impacto nos testes

Já existem testes de configuração, gateway, status mapper, assinatura/webhook, orquestração, controllers, checkout Cliente/Vendedor, eligibility e harness HTTP. A cobertura atual valida pagamento **online**, não o workflow de cobrança Point.

Futura cobertura deverá incluir: pedido criado sem chamada Point; CASH distinto; tentativa criada apenas por operador autorizado; terminal associado correto; idempotência e retry ambíguo sem duplicar cobrança; estados `action_required`, processed/approved, recusado, cancelado e expirado; webhook válido/inválido/replay; valor/referência divergentes; impedir entrega antes da aprovação; override auditado somente se aprovado; refund não automático; preservação de PIX/boleto. Não foram executados testes nesta etapa.

## 15. Riscos

- Cobrança duplicada após timeout ou clique repetido.
- Pedido entregue sem confirmação autoritativa ou, inversamente, entrega bloqueada por webhook atrasado.
- Associação de um pedido ao terminal/Store/POS incorreto, em especial com vários motoristas.
- Operador ou usuário do Portal ganhar autorização indevida para cobrar.
- Divergência entre status da order Point e status da transação; o domínio não deve colapsar os dois sem regra.
- Cancelamento/refund e pagamentos presenciais parcialmente processados.
- Checkout assistido continuar cobrando cartão online depois de o fluxo do Portal ter mudado.
- Incompatibilidade do WMC ou de relatório financeiro com uma nova condição.
- Dados históricos `CREDIT_CARD` não devem ser reinterpretados como `CARD_ON_DELIVERY`.

## 16. Dependências externas e validação Point

A documentação atual do Mercado Pago descreve integração Point pela Orders API: criar order `type=point`, atribuí-la a um terminal no modo integrado/PDV, associando-o a Store e POS; a API exige `X-Idempotency-Key`. A lista de terminais é consultável pela API. A visão geral lista Point Smart 1, Point Smart 2, Point Pro 2 e Point Pro 3, mas a compatibilidade da conta e dos dispositivos efetivamente usados pela Orofoods não foi verificada. [Fluxo Point](https://www.mercadopago.com.br/developers/pt/docs/mp-point/payment-processing), [configuração de terminal](https://www.mercadopago.com.br/developers/pt/docs/mp-point/configure-terminal), [referência de criação de order](https://www.mercadopago.com.br/developers/es/reference/in-person-payments/point/orders/create-order/post), [visão geral e dispositivos](https://www.mercadopago.com.br/developers/es/docs/mp-point/overview)

Para teste, o Mercado Pago documenta credenciais de teste e simulação de estados por `POST /v1/orders/{order_id}/events`; há device virtual de sandbox. A documentação informa que credenciais de teste não processam pagamento real em terminal físico. Precisam ser confirmados no ambiente da empresa: aplicação e conta, credenciais de teste, hardware e seriais, possibilidade de modo PDV, Store/POS, device IDs, meio aceito, webhooks e modelo de credenciais próprio versus OAuth. [Testes de integração Point](https://www.mercadopago.com.br/developers/pt/docs/mp-point/integration-test), [credenciais Point](https://www.mercadopago.com.br/developers/pt/docs/mp-point/create-application)

Nenhuma conta Mercado Pago, dispositivo, secret ou configuração foi consultada nesta análise.

## 17. Fases futuras propostas

1. **Pré-requisitos e modelagem:** fechar escopo dos checkouts, método versus condição comercial, Driver/Route, WMC, hardware, conta, Stores/POS, permissões e proposta de schema/feature flag.
2. **Conceito e flag:** introduzir Cartão na entrega em Staging com persistência e leitura controladas; preservar CASH e PIX.
3. **Checkout:** criar pedido sem cobrança online e exibir estado pendente; retirar o cartão online somente após validar todos os fluxos afetados.
4. **Entrega e terminais:** estabelecer atribuição motorista–terminal e UX autenticada para iniciar/repetir/consultar cobrança.
5. **Orders API Point:** implementar criação, consulta e cancelamento idempotentes; tratar timeout/estados ambíguos. Refund permanece separado.
6. **Webhook e conciliação:** autenticar eventos, buscar estado autoritativo, reconciliar idempotentemente e bloquear conclusão da entrega sem `Approved`.
7. **Validação de Staging:** simular sucesso, recusa, cancelamento, expiração, ação requerida, duplicidade e indisponibilidade; testar sem cobrança real quando sandbox permitir.
8. **WMC e operação financeira:** só após contrato de negócio, definir condição exportada, momento de envio/importação, título, conciliação e relatório.

Estas fases são proposta de análise, não autorização para abrir branch, implementar, alterar banco, configurar Mercado Pago, fazer deploy ou atuar em Production. A execução requer o encerramento das validações atuais de Staging e autorização separada.
