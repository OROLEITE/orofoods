# Importação genérica de unidades oficiais WMC

## Intenção

Permitir que a importação manual da Central WMC use o cadastro oficial de unidades do WMC para qualquer marca. Produtos como AVIAÇÃO com unidade principal `UN` e unidade master `CX` devem ser importáveis quando o snapshot de `PRODUTOS` e `PRODUTOS_UNCONV` for consistente, sem exigir a existência de `PC`.

## Objetivos

- Validar a unidade principal informada em `PRODUTOS.UN` contra exatamente uma unidade do snapshot oficial.
- Importar todas as unidades, fatores, códigos de barras e marcações `UNMASTER` confirmados em `PRODUTOS_UNCONV`.
- Usar, quando fornecidos e válidos, o identificador da unidade principal para `Ean`/`Ean13` e o identificador GTIN-14 da unidade master para `Dun14`.
- Persistir `QTDE_EMB` e `QTDE_CONV` em `WmcPackageQuantity` e `WmcConversionQuantity`, sem derivar um campo do outro ou do fator da unidade.
- Manter preço, reimportação explícita, transação, rollback, idempotência e bloqueio backend de produtos inativos.
- Exibir na prévia códigos genéricos de unidade, fatores, identificadores, quantidades e motivos de bloqueio.

## Não objetivos

- Alterar `WmcInitialLoadReadinessEvaluator`, usado pela sincronização automática.
- Alterar `WmcBimboProductRules`, `WmcBimboStagingImportService`, `WmcOrderUnitResolver` ou o formato de exportação de pedidos PCT/CX.
- Gravar unidades WMC em `CommercialUnitCode`, `LogisticsUnitCode` ou `PackagesPerCase` do Orofoods.
- Alterar estoque físico, preços promocionais, conversões comerciais locais, schema, migrations, banco, configuração de sincronização ou deploy.

## Validação manual escolhida

A validação específica da importação manual terá estes motivos explícitos:

- snapshot de `PRODUTOS_UNCONV` incompleto, ausente ou vazio;
- código de unidade vazio ou duplicado;
- `PRODUTOS.UN` vazio ou ausente do snapshot;
- mais de uma unidade marcada como master;
- fator ausente, zero ou negativo em unidade oficial;
- código de barras informado com tamanho/GTIN inválido;
- `UN_ALTERN` informado mas ausente do snapshot;
- `QTDE_EMB` ou `QTDE_CONV` informada com valor zero ou negativo.

A ausência de `PC`, `CX`, de uma master não informada pelo WMC, ou de um código de barras não fornecido não será, isoladamente, motivo de bloqueio.

Os códigos de barras fornecidos serão aceitos como GTIN-8, GTIN-12, GTIN-13 ou GTIN-14 com dígito verificador. O código da unidade principal será usado para `Ean`; somente um GTIN-13 será usado para `Ean13`. Somente um GTIN-14 da unidade master será usado para `Dun14`. Campos locais existentes não serão limpos quando o WMC não fornecer um valor equivalente.

## Separação de readiness

Será criado um avaliador exclusivo da importação manual. Ele manterá os bloqueios de situação WMC ativa, produto local desabilitado, preço `PRECOVENDA` inválido, estoque físico/quantidade disponível e ajuste manual de inventário, mas validará unidades pelo contrato genérico acima e não verificará PCT/CX.

O `WmcSyncService` continuará chamando o avaliador compartilhado atual, sem alteração de comportamento automático.

## Fluxo de gravação

`WmcProductImportService` continuará relendo o WMC dentro da operação, rejeitando qualquer seleção que contenha produto inativo antes de gravar. A confirmação continuará exigindo seleção elegível e reimportação explícita para produtos existentes. As unidades somente serão reconciliadas quando o snapshot for válido; em caso de inconsistência, o produto permanecerá bloqueado e os dados locais de unidade não serão substituídos.

`BasePrice` continuará recebendo diretamente `PRECOVENDA` conforme o contrato de preço atual, sem fator, `QTDE_EMB` ou `QTDE_CONV` aplicado ao preço.

## Testes e riscos controlados

Os testes cobrirão AVIAÇÃO (`UN`/`CX`), BIMBO (`PC`/`CX`), unidades alternativas `PT`/`CP`, múltiplas marcas, ausência de PC, master ausente ou duplicada, fator/código inválido, quantidades WMC, preço, inativo enviado manualmente e preservação da configuração comercial local. Os testes existentes do avaliador automático e do staging BIMBO permanecerão como proteção contra alteração de escopo.

Não haverá importação real, migration, alteração de banco ou deploy durante a implementação.
