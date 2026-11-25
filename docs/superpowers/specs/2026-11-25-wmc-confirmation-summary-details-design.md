# Confirmação WMC: resumo e dados cadastrais — desenho

## Intenção

Corrigir a etapa de confirmação da importação manual WMC para que seus contadores representem categorias mutuamente exclusivas e para que o operador veja, antes de confirmar, os dados oficiais WMC que o serviço de importação efetivamente persistirá. O caso de referência é o produto novo AVIAÇÃO `101900`, mas o comportamento deve ser genérico para todas as marcas.

## Objetivos

- Separar visualmente produtos consultados, selecionados e bloqueados.
- Classificar a seleção em `Novos`, `Existentes com alterações` e `Existentes sem alterações`, sem contar um produto novo como inalterado.
- Exibir, por produto selecionado, preço proposto, unidade principal, unidade master, fatores, EAN, DUN, `QTDE_EMB`, `QTDE_CONV` e todas as unidades alternativas disponíveis no snapshot WMC.
- Para produtos existentes, mostrar os valores atuais e os valores WMC somente quando fornecidos pelos objetos de proposta/diferença existentes.
- Manter o layout compacto, o tema dark e os tokens/botões ciano atuais.

## Não objetivos e antiobjetivos

- Não alterar `WmcProductImportService`, `WmcProductReader`, regras de preço, estoque, unidades, transação, rollback, idempotência ou bloqueio de inativos.
- Não criar conversões, códigos, valores ausentes ou campos que o serviço não grava.
- Não alterar banco, migrations, sincronização automática, configuração `WmcSync__Enabled` ou integração de pedidos.
- Não executar importações reais, push, PR ou deploy.

## Abordagens consideradas

1. Corrigir somente a expressão de contagem no controller e ampliar a view. É o menor diff, mas deixa a regra de classificação acoplada ao fluxo HTTP.
2. **Fábrica pura de resumo, escolhida.** Adicionar `WmcProductImportSummary.From(candidates, selected)` no modelo de apresentação, com contadores de consulta, seleção, bloqueio e categorias mutuamente exclusivas; a view passa a consumir esses nomes sem duplicar regra. Os dados cadastrais são renderizados a partir de `WmcProductImportCandidate.UnitProposal` e `Units`.
3. Criar um view model/partial completo por item de confirmação. Centraliza mais markup, mas amplia a superfície da alteração sem necessidade para este ajuste.

## Desenho escolhido

`WmcProductImportSummary` receberá todos os candidatos e a seleção validada e produzirá:

- `Consulted`: quantidade de candidatos consultados;
- `Selected`: quantidade selecionada;
- `Blocked`: candidatos não selecionáveis;
- `NewProducts`: selecionados sem `ProductId`;
- `ExistingWithChanges`: existentes com `Changes.Count > 0`;
- `ExistingWithoutChanges`: existentes com `Changes.Count == 0`.

Assim, `Selected = NewProducts + ExistingWithChanges + ExistingWithoutChanges`. A view não exibirá um total de existentes junto das duas subdivisões, evitando dupla contagem visual.

Na confirmação, cada item selecionado exibirá somente dados já presentes no candidato: `PrecoVenda`/`PriceProposal`, códigos de unidade da `UnitProposal`, valores atuais e propostos dos fatores, EAN, DUN, `QTDE_EMB`, `QTDE_CONV`, diferenças do serviço e a lista `Units` com código, descrição, fator, barcode e indicação de master. Valores nulos serão renderizados como indisponíveis, nunca inferidos.

## Testes

- Teste unitário do resumo: produto novo sem `Changes` fica somente em `NewProducts`; existentes são divididos entre com e sem alterações; a soma das categorias é `Selected`.
- Teste de controller com novo produto AVIAÇÃO `101900`, confirmando contadores e ausência de `ExistingWithoutChanges`.
- Teste de controller com produto existente com e sem alterações.
- Teste de contrato da view confirmando os novos contadores, campos cadastrais WMC e preservação do checkbox de reimportação, dark mode e tokens ciano.
- Build Release, testes focados e suíte completa.
