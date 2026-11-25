# Plan: navegação administrativa em novas abas do navegador

> **For Codex:** REQUIRED SUB-SKILL: use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** permitir que links de leitura do menu administrativo abram em nova aba
por padrão, com opção persistente de mesma aba, sem criar abas internas nem
alterar ações de gravação.

**Architecture:** o partial `_AdminNavigation.cshtml` renderiza o seletor e
marca o menu; `admin-navigation.js` aplica `target="_blank"` somente às
âncoras do menu, persiste apenas `new`/`same` e registra avisos nativos de
`beforeunload` para formulários POST alterados; `admin-navigation.css` mantém a
interface compacta e acessível nos temas e breakpoints existentes.

**Tech stack:** ASP.NET Core MVC/Razor, JavaScript browser nativo, CSS, xUnit.

## Constraints and non-goals

- Não criar sistema de abas internas.
- Não interceptar ou substituir cliques normais, Ctrl+clique, clique do meio ou
  menu de contexto.
- Não aplicar `target` a botões ou formulários de Salvar, Importar, Confirmar ou
  Excluir.
- Não persistir tokens, senhas, filtros ou valores de formulário em storage.
- Não modificar WMC, importação, preços, estoque, pedidos, regras comerciais,
  banco ou integração automática.
- Não executar migrations, importações, deploy, push ou PR.

## Task 1: especificar os contratos de navegação com testes RED

**Files:**
- Modify: `Orofoods.Web.Tests/Views/AuthenticatedNavigationTests.cs`
- Modify: `Orofoods.Web.Tests/Views/ResponsiveLayoutTests.cs`
- Add: `Orofoods.Web.Tests/Views/AdminNavigationBehaviorTests.cs`

1. Adicionar testes que leiam o partial, layout, script e CSS.
2. Cobrir o padrão `new`, as opções `new`/`same`, a marcação exclusiva de
   links, `rel="noopener"`, a ausência de interceptação de cliques, o
   `beforeunload`, a exclusão de campos sensíveis e os estilos responsivos/
   dark mode.
3. Executar os testes focados e confirmar que falham pela ausência do contrato.

## Task 2: renderizar a preferência no menu compartilhado

**Files:**
- Modify: `Orofoods.Web/Views/Shared/_AdminNavigation.cshtml`

1. Marcar o `nav` compartilhado como menu de navegação administrativa.
2. Renderizar seletor único por prefixo (`desktop`/`mobile`) com `Nova aba`
   selecionada por padrão, `Mesma aba`, label acessível e aviso de que ações
   de gravação permanecem na aba atual.
3. Manter todos os links e URLs Tag Helper existentes sem alterações de rota
   ou query string.

## Task 3: implementar a preferência e o aviso de alterações

**Files:**
- Modify: `Orofoods.Web/wwwroot/js/admin-navigation.js`

1. Preservar o comportamento atual de recolhimento do sidebar.
2. Inicializar a preferência com fallback `new`, validar o enum e sincronizar
   os dois selects.
3. Aplicar/remover `target="_blank"` e `rel="noopener"` somente em
   `a.admin-nav-link[href]`; não instalar listeners de click.
4. Rastrear em memória alterações nos campos editáveis de formulários POST,
   inclusive controles sem `name`, ignorando hidden/antiforgery/password/file,
   e registrar `beforeunload` apenas quando houver mudanças não enviadas.
5. Marcar o submit intencional por formulário e resetar o bypass quando o
   evento for cancelado ou terminar sem descarregar a página, sem usar
   `sessionStorage` ou guardar valores fora da memória da página.
6. Omitir apenas o seletor visual no inbox WhatsApp histórico; os links do
   menu continuam usando a preferência global.

## Task 4: aplicar o estilo compacto e acessível

**Files:**
- Modify: `Orofoods.Web/wwwroot/css/admin-navigation.css`

1. Estilizar o seletor e a ajuda no menu desktop e mobile.
2. Preservar foco visível, dark mode, acento ciano e leitura no sidebar
   recolhido sem alterar geometria ou responsividade existente.

## Task 5: verificar e revisar o escopo

1. Executar testes focados.
2. Executar `dotnet build Orofoods.slnx --no-restore --configuration Release`.
3. Executar a suíte completa com `dotnet test Orofoods.slnx --no-restore
   --configuration Release -m:1 --logger "console;verbosity=minimal"`.
4. Se houver navegador disponível, validar os dois modos, abrir link sem
   perder a página original, gestos nativos e aviso de formulário; caso não
   haja, reportar a limitação sem inferir validação visual.
5. Executar também `node Orofoods.Web.Tests/Browser/admin-navigation-runtime.cjs`
   para exercitar o JavaScript real com um DOM mínimo, sem dependências de
   rede; isso não será apresentado como validação visual de navegador.
6. Confirmar que o diff contém somente documentação, navegação administrativa,
   seus estilos e testes; confirmar que WMC e regras comerciais não foram
   tocados.
7. Criar commit local apenas após toda a verificação e apresentar SHA; não
   executar push, PR, merge ou deploy.
