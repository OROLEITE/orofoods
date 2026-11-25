# Design: navegação administrativa em novas abas do navegador

## Contexto

As telas administrativas do Orofoods usam MVC/Razor convencional, com um menu
lateral compartilhado em `_AdminNavigation.cshtml`. A navegação atual substitui
a página aberta e pode interromper filtros, seleções ou um formulário em edição.

## Decisão

Adotar navegação convencional com uma preferência explícita no menu:

- `Nova aba` é o padrão inicial.
- `Mesma aba` mantém o comportamento tradicional.
- A preferência é salva somente como o enum `new` ou `same` em uma chave
  namespaced de `localStorage`.
- Se o armazenamento estiver indisponível ou inválido, o padrão seguro é
  `Nova aba`.
- O valor não contém URL, identidade, token, senha, formulário ou outro dado
  sensível.

Os links administrativos continuam sendo links HTML normais. O JavaScript
apenas aplica ou remove `target="_blank"` e `rel="noopener"` nos links do menu,
sem interceptar cliques. Assim, Ctrl+clique, clique do meio e o menu de
contexto continuam sob controle nativo do navegador.

Somente âncoras marcadas pelo menu (`a.admin-nav-link`) recebem a preferência.
Botões e formulários de Salvar, Importar, Confirmar, Excluir e outras ações de
gravação não passam pelo seletor e continuam na aba atual, preservando os
contratos MVC, antiforgery e POST.

## Estado não salvo

O script acompanha, apenas em memória, os valores iniciais dos controles
editáveis de formulários POST administrativos. Campos ocultos, antiforgery,
senhas e arquivos não entram no rastreamento. Se um formulário for alterado e
a página for realmente descarregada, o navegador recebe o evento
`beforeunload` e apresenta seu aviso nativo. O envio do formulário marca a
navegação como intencional.

Nenhum rascunho ou valor de campo é gravado em `localStorage` ou
`sessionStorage`. Filtros existentes continuam sendo query strings e são
preservados pelo href gerado pelo MVC.

## Interface e acessibilidade

O seletor é renderizado no menu desktop e no offcanvas mobile, com `label`,
`aria-label` e texto explicando que ações de gravação permanecem na aba atual.
Os dois controles representam a mesma preferência e são sincronizados em
memória. O estilo reutiliza o quadro lateral existente, dark mode, foco visível
e acento ciano, sem introduzir abas internas ou alterações no menu lateral.
O frame histórico do inbox WhatsApp mantém sua composição própria: nele o
controle visual é omitido, mas os links do menu continuam respeitando a
preferência já definida em outra tela administrativa.

## Alternativas consideradas

1. Abas internas reais com cache de DOM, rascunhos e restauração de estado.
   Rejeitada: exigiria um novo gerenciador de ciclo de vida, duplicaria
   contratos MVC/antiforgery e aumentaria o risco de dados obsoletos.
2. Navegação MVC/Razor convencional com escolha entre mesma aba e nova aba.
   Escolhida: altera somente o destino de links de leitura, preserva o
   comportamento nativo do navegador e não toca nos serviços ou entidades.

## Escopo e limites

Esta mudança não altera a Central WMC, serviços de importação, preços,
estoque, pedidos, regras comerciais, autenticação, autorização ou banco. Não
há migrations, importações reais, deploy, push ou PR neste trabalho.
