# Controles Comerciais de Estoque e Credito

## Objetivo

Impedir que pedidos confirmados ultrapassem o estoque disponivel ou o limite de credito do cliente, registrando reservas auditaveis por item sem alterar o contrato ainda pendente com o ERP WMC.

## Escopo

- Estoque sera controlado por produto em unidades de caixa, que ja e a unidade usada em `OrderItem.Quantity`.
- A confirmacao do checkout validara pedido minimo existente, disponibilidade, estoque e credito disponivel antes de gravar o pedido.
- Um pedido confirmado reservara o saldo de cada produto e aumentara `Customer.CreditUsed` pelo total a prazo.
- Cancelamentos liberarao a reserva e o credito uma unica vez. PIX nao consome limite de credito.
- O administrador podera consultar e ajustar estoque por produto e visualizar o saldo reservado.

## Fora de Escopo

- Integracao real com WMC, importacao/exportacao `.txt`, estoque por lote, fiscal, faturamento e conciliacao financeira.
- Sincronizacao de estoque externa. O ERP continuara desabilitado ate a definicao do layout e contrato.

## Arquitetura

`ProductInventory` sera a fonte de saldo fisico e reservado de cada produto. `InventoryReservation` mantera o vinculo de cada reserva com um pedido, impedindo liberacao duplicada e preservando auditoria.

`CommercialValidationService` concentrara as regras de disponibilidade e credito. `OrderReservationService` executara, na mesma transacao, a criacao das reservas e a atualizacao do credito. O `PortalController` chamara esses servicos ao confirmar e o fluxo administrativo de cancelamento os chamara para desfazer as reservas.

## Regras de Negocio

- Saldo disponivel = `QuantityOnHand - QuantityReserved`; pedidos nao podem gerar saldo negativo.
- Todo produto ativo recebe um registro de estoque com saldo inicial zero; o administrador deve informar o saldo antes de liberar venda.
- Produtos sem estoque sao indisponiveis no carrinho e o checkout informa o item impeditivo em portugues.
- Credito disponivel = `CreditLimit - CreditUsed`. Pagamentos PIX sao liberados sem reserva de credito; outros prazos validos consomem credito.
- Uma reserva esta vinculada a um unico pedido e pode estar `Active` ou `Released`.
- Somente a transicao para `Cancelled` libera reservas e credito. Demais alteracoes de status preservam a reserva.

## Persistencia e Concorrencia

As alteracoes de saldo e credito ocorrerao em transacao serializavel no PostgreSQL. Cada reserva e unica por `OrderId` e `ProductId`; a chave unica impede duplicidade em repeticoes de requisicao.

Uma migration PostgreSQL adicionara tabelas de inventario e reservas, indices de consulta e precisao monetaria. A migration SQLite de testes sera criada em paralelo, seguindo o padrao existente.

## Administracao e API

O Admin recebera uma tela de estoque por produto, com saldo fisico, reservado e disponivel, alem de ajuste manual com motivo. A API v1 de catalogo passara a retornar disponibilidade comercial calculada, sem expor custo ou saldo bruto a clientes.

## Testes

Testes de servico cobrirao estoque suficiente/insuficiente, credito suficiente/insuficiente, PIX sem consumo de credito, cancelamento idempotente e reserva unica. Testes de controller cobrirao mensagens de bloqueio e a gravacao bem-sucedida.
