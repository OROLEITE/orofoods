# ADR: Cartão na entrega com Mercado Pago Point

- **Status:** Aprovada; Fase 2 implementada localmente, ainda não integrada nem implantada.
- **Data:** 2026-09-29
- **Projeto:** Orofoods
- **Substitui:** o pagamento de cartão diretamente no checkout do site, conforme o desenho técnico anterior em `docs/superpowers/specs/2026-09-17-mercado-pago-phase-1-design.md`.
- **Análise relacionada:** [`mercado-pago-point-impact-analysis-2026-09-29.md`](../mercado-pago-point-impact-analysis-2026-09-29.md).

## Contexto

A decisão de negócio é que o cliente não pagará cartão diretamente no site durante o checkout. O cliente poderá escolher **Cartão na entrega**; o pedido será criado sem cobrança online e o pagamento ocorrerá presencialmente, na entrega, em terminal Mercado Pago Point. O Orofoods registrará a confirmação do provedor e a entrega somente será concluída após a aprovação do pagamento.

O checkout não coletará número, validade ou CVV. O Orofoods não criará uma cobrança, `Payment Preference` ou order de pagamento Point no momento de finalizar o pedido. A cobrança presencial será iniciada posteriormente pelo operador autorizado no fluxo de entrega.

## Decisão

1. Apresentar **Cartão na entrega** como uma opção de pagamento comercial própria.
2. Não reutilizar **CASH / À vista** para representar cartão presencial.
3. Criar o pedido normalmente sem chamar o gateway para cartão no checkout; manter o pagamento local em `Pending` até a etapa de cobrança.
4. Na entrega, o operador autorizado poderá solicitar uma cobrança para o terminal Point associado ao motorista/rota. Uma cobrança por tentativa deverá ser idempotente e vinculada ao pedido.
5. Atualizar o pagamento para `Approved` somente após confirmação válida do Mercado Pago. A notificação será autenticada, tratada de forma idempotente e reconciliada com o estado autoritativo do provedor.
6. Não permitir a conclusão normal de pedido com `CARD_ON_DELIVERY` enquanto o pagamento não estiver aprovado. Se uma exceção operacional vier a ser necessária, deverá ser explícita, autorizada por administrador e auditada.
7. Não efetuar estorno automaticamente ao cancelar um pedido que já tenha pagamento aprovado. Estorno será um fluxo separado, após definição comercial.
8. Não armazenar número completo de cartão, CVV ou outros dados PCI que não sejam necessários. Identificadores de order, transação, terminal, motorista, valor, status, meio retornado e timestamps poderão ser registrados para conciliação.
9. Manter separados `OrderStatus` e `PaymentStatus`. A implementação deverá usar os enums existentes após validá-los, sem introduzir duplicatas de estados.

## Escopo e limites

Esta decisão altera o desenho futuro do pagamento por cartão no site. Ela não determina a remoção de PIX, boleto ou outras condições comerciais existentes. A continuação desses meios deve ser preservada e confirmada ao planejar a alteração. O cartão online atual permanece no código enquanto não houver implementação e implantação autorizadas; este ADR, por si só, não muda o comportamento do sistema.

A semântica do WMC para pagamento presencial continua pendente: condição de pagamento exportada, momento de envio do pedido, título e conciliação contábil/financeira precisam de decisão própria.

## Consequências esperadas

- O pedido e o pagamento terão ciclos de vida independentes.
- O sistema precisará de um fluxo de operação de entrega, associação de terminal e reconciliação por webhook.
- O checkout do Cliente e o checkout assistido do Vendedor, além de endpoints e telas que atualmente cobram cartão online, precisarão ser avaliados na implementação.
- Dados de terminal e pagamento deverão permitir conciliação sem guardar dados sensíveis do cartão.
- O modelo e a persistência atuais poderão exigir migrations. Nenhuma migration foi gerada ou executada ao registrar esta decisão.

## Pré-condições para implementação

Antes de habilitar chamadas remotas, devem ser confirmados os modelos Point disponíveis para a Orofoods, a conta/aplicação Mercado Pago, a compatibilidade de integração remota, lojas, caixas (POS), IDs de dispositivos, credenciais e webhooks. Esses itens não fazem parte da Fase 2.

## Registro de implementação — Fase 2

A Fase 2 adiciona a base operacional para Cartão na Entrega e gestão de terminais, mantendo a integração Point desabilitada. As flags `Payments:CardOnDeliveryEnabled` e `Payments:MercadoPagoPointEnabled` permanecem `false`. O provedor Point registrado retorna `POINT_INTEGRATION_DISABLED` e não possui cliente HTTP nem realiza chamadas externas.

O motorista é uma entidade operacional própria, sem conta Identity ou role. `Drivers` contém `Id`, `Name`, `IsActive`, `CreatedAt` e `UpdatedAt`. `PaymentTerminals` contém `Id`, `Provider`, `DeviceId`, `StoreId`, `PosId`, `IsActive`, `CreatedAt` e `UpdatedAt`; esta implementação não guarda tokens ou credenciais. `DriverPaymentTerminalAssignments` mantém histórico por `Id`, `DriverId`, `PaymentTerminalId`, `StartedAt`, `EndedAt` e `CreatedAt`. Pagamentos podem referenciar opcionalmente a associação histórica que foi usada.

A migration PostgreSQL `AddDriverPaymentTerminalArchitecture` cria essas três tabelas e adiciona `Payments.DriverPaymentTerminalAssignmentId` nullable. As chaves estrangeiras de associação para motorista e terminal, e a referência do pagamento para associação, usam `RESTRICT`. Índices: `Drivers.Name`; índice único `(Provider, DeviceId)` em `PaymentTerminals` filtrado por `DeviceId IS NOT NULL`; índice de `DriverId` no histórico; índice único `PaymentTerminalId` no histórico filtrado por `EndedAt IS NULL`; índice de `Payments.DriverPaymentTerminalAssignmentId`. Isso permite histórico sem apagar associações encerradas, uma associação ativa por terminal e reutilização de terminais com `DeviceId` nulo. A migration foi gerada e validada como artefato; não foi aplicada a nenhum banco.

A cadeia de migrations SQLite do repositório está desatualizada e não contém a tabela `Payments`; por isso, nenhuma migration SQLite foi criada nesta etapa. Os testes SQLite do modelo usam `EnsureCreated`, enquanto a migration de produção é PostgreSQL. Antes de qualquer fluxo SQLite que dependa de migrations, a cadeia deverá ser reconciliada em escopo separado.
