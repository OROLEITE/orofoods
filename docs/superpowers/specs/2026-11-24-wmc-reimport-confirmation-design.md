# Confirmação explícita de reimportação WMC

## Intenção

Tornar inequívoca a autorização para atualizar produtos já cadastrados na confirmação da importação manual WMC. A opção deve permanecer desativada por padrão e o backend deve tratar a ausência do campo como recusa segura.

## Estado atual e diagnóstico

- A confirmação renderiza radios com `reimportExisting=false` selecionado por padrão.
- O controller encaminha `true` ao serviço apenas quando recebe exatamente o texto `true`.
- O serviço já preserva a regra vigente: sem autorização, produto existente é ignorado; com autorização, o fluxo unificado aplica as regras existentes.
- A prévia calcula `ChangedProducts` e `UnchangedProducts`, mas a confirmação não os apresenta separadamente.
- Os campos alterados aparecem em um `details` fechado, dificultando a conferência.

## Abordagem aprovada

Usar um único checkbox sem `checked`, com o texto “Atualizar produtos já cadastrados conforme WMC”, explicação dos campos autorizados e estado visual “desativada”/“ativada”. O controller aceitará o checkbox ausente como `false`, validará valores inesperados e encaminhará o bool normalizado ao serviço.

A confirmação exibirá contadores de produtos novos, existentes com alterações e existentes sem alterações. A lista de campos, preços e unidades ficará aberta por padrão, sem mudar cálculos ou regras de aplicação.

## Não objetivos e restrições

- Não alterar preços, unidades, estoque, seleção de campos, transação ou lógica de importação.
- Não criar migration, alterar banco, executar importação real ou fazer deploy.
- Não alterar Clientes, pedidos WMC, Firebird ou PostgreSQL.

## Verificação

Adicionar testes de view e controller para checkbox desmarcado, autorização explícita, campo ausente tratado como `false`, persistência da seleção entre POSTs e classificação de alterações/inalterados. Os testes devem demonstrar que produto existente não é atualizado sem autorização e pode seguir o fluxo quando `true` é enviado.
