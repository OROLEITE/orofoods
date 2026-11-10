# Consolidated Portal UI/UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corrigir menu, acesso administrativo, busca de clientes e apresentação compartilhada do Portal do Vendedor sem mudar regras comerciais ou de autorização.

**Architecture:** Reutilizar AdminPortalAccessController, CustomerAccessService, SellerWorkspaceService, o contexto de vendedor existente e Areas/Vendedor/Views/_SellerLayout.cshtml. Adicionar respostas de busca limitadas no servidor e um cliente JS compartilhado, mantendo a validação final nos fluxos atuais.

**Tech Stack:** ASP.NET Core MVC/Razor, ASP.NET Core Identity, EF Core, JavaScript e CSS existentes, xUnit.

**Spec:** docs/superpowers/specs/2026-10-15-consolidated-portal-ui-ux-design.md

## Global Constraints

- Trabalhar somente em C:/Dev/Orofoods/.worktrees/fix-consolidated-portal-ui-ux, branch fix/consolidated-portal-ui-ux.
- Não executar fetch, autenticação GitHub, push, PR, merge ou deploy.
- Não alterar checkout principal, outras worktrees, dados, migrations ou configurações do ambiente.
- Pesquisas começam com 1 caractere, usam debounce de 300 ms e retornam no máximo 20 opções.
- Filtrar clientes ativos/aprovados e manter vínculo com o vendedor derivado do contexto autenticado.
- Preservar auditoria, ReturnUrl seguro, identidade do Administrador e regras existentes dos portais.
- Manter claro/escuro sem forçar tema global e CSS fora do site público.
- Não alterar regras comerciais, pedidos, preços, pagamentos ou WMC.

## Review Focus

- ReturnUrl externo ou administrativo inválido após login: deve cair na Central sem loop; cobrir em LoginRedirectTests.
- Consulta vazia, espaços, CNPJ pontuado e código WMC: não carregar a base, normalizar a busca e limitar opções; cobrir em CustomerAccessServiceTests e SellerWorkspaceServiceTests.
- Resposta AJAX antiga após consulta atual: somente a consulta mais recente atualiza a lista; cobrir na validação de navegador.
- Administrador sem vendedor selecionado ou vendedor inativo: não retornar carteira; cobrir em SellerWorkspaceServiceTests e SalesRepresentativeAccessServiceTests existentes.
- Cliente desativado ou removido da carteira após a busca: recusar no servidor e não gravar contexto/auditoria inválidos; cobrir em PortalControllerCatalogTests e SellerWorkspaceServiceTests.

---

### Task 1: Corrigir o dropdown Ações

**Files:**
- Modify: Orofoods.Web/wwwroot/css/admin-users.css
- Test: Orofoods.Web.Tests/Views/AdminUsersViewTests.cs

**Interfaces:**
- Consumes: markup atual de details.admin-users-actions e destinos existentes do UsersController.
- Produces: menu que escapa do recorte do contêiner e fica acima das linhas adjacentes quando aberto.

- [ ] **Step 1: Escrever o teste de regressão**

Adicionar AdminUsersViewTests.Actions_menu_keeps_existing_links_and_is_visible_when_open, verificando os três links e o contrato CSS de overflow/empilhamento.

- [ ] **Step 2: Confirmar que o teste falha**

Executar: dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --filter FullyQualifiedName~AdminUsersViewTests

Esperado: falha pelo contrato de overflow/empilhamento ainda ausente.

- [ ] **Step 3: Aplicar a menor correção CSS**

Permitir que o menu aberto escape do recorte da tabela em desktop e móvel; elevar linha/menu ativo; manter foco visível e posicionamento responsivo. Não trocar rotas nem remover a interação nativa de details.

- [ ] **Step 4: Verificar o teste**

Reexecutar AdminUsersViewTests; esperado: aprovado.

- [ ] **Step 5: Commit**

Commit local: fix: keep admin user actions menu visible

### Task 2: Central de Acesso e redirecionamento do Administrador

**Files:**
- Modify: Orofoods.Web/Areas/Identity/Pages/Account/Login.cshtml.cs
- Modify: Orofoods.Web/Views/AdminPortalAccess/Index.cshtml
- Modify: Orofoods.Web/Views/Shared/_PortalLayout.cshtml
- Modify: Orofoods.Web/Views/Shared/_AdminNavigation.cshtml
- Test: Orofoods.Web.Tests/Identity/LoginRedirectTests.cs
- Test: Orofoods.Web.Tests/Views/AuthenticatedNavigationTests.cs
- Create: Orofoods.Web.Tests/Views/AdminPortalAccessViewTests.cs

**Interfaces:**
- Consumes: rota /Admin/Portais, filtros atuais de ReturnUrl e auditoria.
- Produces: fallback administrativo /Admin/Portais e Central com três destinos visíveis.

- [ ] **Step 1: Escrever testes de redirecionamento e view**

Atualizar Admin_without_return_url_redirects_to_admin_dashboard para esperar /Admin/Portais; cobrir ReturnUrl externo e rota de portal sem contexto; preservar retorno administrativo local, /Portal/SelectCustomer e destinos dos outros perfis. Adicionar teste para os três links e a seção de auditoria da Central; verificar que a navegação do Portal do Cliente retorna à Central.

- [ ] **Step 2: Confirmar falha**

Executar filtros LoginRedirectTests, AuthenticatedNavigationTests e AdminPortalAccessViewTests; esperado: falha nas expectativas da Central.

- [ ] **Step 3: Implementar a Central**

Mudar somente o fallback administrativo, rotular o link lateral e o retorno do Portal do Cliente como Central de Acesso e acrescentar o cartão Painel Administrativo; manter os cartões Cliente/Vendedor e a auditoria existente.

- [ ] **Step 4: Verificar os testes**

Reexecutar os filtros; esperado: aprovado, com retorno à Central e sem mudanças para Cliente, Vendedor, GerenteComercial ou Operador.

- [ ] **Step 5: Commit**

Commit local: feat: make admin access center the default

### Task 3: Busca segura e limitada no backend

**Files:**
- Create: Orofoods.Web/Models/Customers/CustomerSearchSuggestion.cs
- Modify: Orofoods.Web/Services/Identity/CustomerAccessService.cs
- Modify: Orofoods.Web/Services/Sellers/SellerWorkspaceService.cs
- Modify: Orofoods.Web/Controllers/AdminPortalAccessController.cs
- Modify: Orofoods.Web/Controllers/PortalController.cs
- Modify: Orofoods.Web/Areas/Vendedor/Controllers/CustomersController.cs
- Modify: Orofoods.Web/Areas/Vendedor/Controllers/OrderDraftsController.cs
- Test: Orofoods.Web.Tests/Services/CustomerAccessServiceTests.cs
- Test: Orofoods.Web.Tests/Services/SellerWorkspaceServiceTests.cs
- Test: Orofoods.Web.Tests/Controllers/AdminPortalAccessControllerTests.cs
- Test: Orofoods.Web.Tests/Controllers/PortalControllerCatalogTests.cs

**Interfaces:**
- Produces: CustomerSearchSuggestion(int Id, string TradeName, string LegalName, string Cnpj, string? WmcCode, string? City).
- Produces: CustomerAccessService.SearchApprovedCustomerSuggestionsAsync(string? query, int maxResults, CancellationToken cancellationToken = default), limitada a 20.
- Produces: SellerWorkspaceService.SearchCustomerSuggestionsAsync(ClaimsPrincipal user, string? query, int maxResults, CancellationToken cancellationToken = default), nula sem contexto válido e limitada a 20.
- Produces: GET /Admin/Portais/Clientes?q=, GET /Vendedor/Clientes/Buscar?q= e GET /Vendedor/Rascunhos/Clientes?q=.

- [ ] **Step 1: Escrever testes de serviço e autorização**

Cobrir nome fantasia, razão social, CNPJ com/sem pontuação e WMC; excluir cliente inativo/não aprovado; limitar a 20; manter carteira exclusiva do vendedor ativo selecionado; não retornar resultados para Administrador sem seleção.

- [ ] **Step 2: Confirmar falha**

Executar CustomerAccessServiceTests, SellerWorkspaceServiceTests e AdminPortalAccessControllerTests; esperado: contratos de sugestão/endpoints ausentes.

- [ ] **Step 3: Implementar DTO, consultas e endpoints**

Filtrar no EF antes de materializar, normalizar CNPJ, limitar resultados no servidor e retornar somente campos do DTO. Manter AdminPortalAccessController protegido por Administrator e os controllers do vendedor nas políticas atuais. Fazer GET/POST de SelectCustomer renderizar sem carregar todas as empresas.

- [ ] **Step 4: Verificar os testes**

Reexecutar os filtros; esperado: aprovado para clientes autorizados e rejeição de resultados fora do escopo.

- [ ] **Step 5: Commit**

Commit local: feat: add authorized customer suggestions

### Task 4: Busca compartilhada e telas de seleção/carteira

**Files:**
- Create: Orofoods.Web/wwwroot/js/customer-search.js
- Create: Orofoods.Web/wwwroot/css/customer-search.css
- Modify: Orofoods.Web/Views/Portal/SelectCustomer.cshtml
- Modify: Orofoods.Web/Areas/Vendedor/Views/Customers/Index.cshtml
- Modify: Orofoods.Web/Areas/Vendedor/Views/OrderDrafts/Editor.cshtml
- Test: Orofoods.Web.Tests/Views/SelectCustomerViewTests.cs
- Create: Orofoods.Web.Tests/Views/CustomerSearchViewTests.cs

**Interfaces:**
- Consumes: endpoints da Task 3 e DTO CustomerSearchSuggestion.
- Produces: raiz [data-customer-search] com data-search-url e data-search-mode (select, links ou cards); campo [data-customer-search-input], resultados [data-customer-search-results], status [data-customer-search-status], seleção [data-customer-search-selected-id] e botão [data-customer-search-submit].
- Produces: cada endpoint retorna JSON com id, tradeName, legalName, cnpj, wmcCode e city; IDs de cliente nunca são aceitos como prova final de autorização.

- [ ] **Step 1: Escrever contratos de markup**

Cobrir campo de busca, ID selecionado e botão inicialmente desabilitado no seletor Admin; resultados acessíveis; estados vazios; inclusão do script comum; links no editor e na carteira.

- [ ] **Step 2: Confirmar falha**

Executar SelectCustomerViewTests e CustomerSearchViewTests; esperado: atributos e resultados dinâmicos ausentes.

- [ ] **Step 3: Implementar o cliente comum e integrar as telas**

Aplicar debounce de 300 ms, cancelar/ignorar respostas antigas, evitar repetição da mesma consulta, exigir um caractere e limpar estado ao apagar a busca. Construir resultados com textContent, suportar setas/Enter/Escape, ponteiro/toque e aria-live, respeitando o limite do servidor. Habilitar o acesso Admin só após seleção e manter revalidação dos destinos no backend.

- [ ] **Step 4: Verificar contratos e comportamento**

Reexecutar SelectCustomerViewTests e CustomerSearchViewTests; esperado: aprovado. Se houver navegador, confirmar que respostas fora de ordem não substituem resultados atuais.

- [ ] **Step 5: Commit**

Commit local: feat: add accessible async customer search

### Task 5: Shell, contexto administrativo e temas do vendedor

**Files:**
- Modify: Orofoods.Web/Areas/Vendedor/Views/_SellerLayout.cshtml
- Modify: Orofoods.Web/Areas/Vendedor/Views/Dashboard/Index.cshtml
- Modify: Orofoods.Web/Areas/Vendedor/Views/Customers/Index.cshtml
- Modify: Orofoods.Web/Areas/Vendedor/Views/OrderDrafts/Index.cshtml
- Modify: Orofoods.Web/Areas/Vendedor/Views/OrderDrafts/Editor.cshtml
- Modify: Orofoods.Web/Views/Shared/_Layout.cshtml
- Modify: Orofoods.Web/Views/AdminPortalAccess/SelectSeller.cshtml
- Create: Orofoods.Web/wwwroot/css/admin-portal-access.css
- Modify: Orofoods.Web/wwwroot/css/seller-area.css
- Modify: Orofoods.Web/wwwroot/css/seller-order-drafts.css
- Test: Orofoods.Web.Tests/Views/SellerPortalShellTests.cs
- Modify: Orofoods.Web.Tests/Views/AdminPortalAccessViewTests.cs
- Create: Orofoods.Web.Tests/Views/SellerPortalVisualContractTests.cs

**Interfaces:**
- Consumes: SalesRepresentativeAccessService.GetActiveRepresentativeAsync(User) e rotas da Task 2.
- Produces: indicador do vendedor selecionado e navegação administrativa no shell compartilhado; estilos escopados às experiências Admin/Vendedor.

- [ ] **Step 1: Escrever testes de contexto e tema**

Cobrir contexto condicional do Admin, vendedor ativo pelo nome, links Administração/Central/Trocar Vendedor/Portal Cliente, contrato de Dashboard/carteira/rascunhos/novo pedido e seletores CSS para claro/escuro e breakpoints 360/390/768.

- [ ] **Step 2: Confirmar falha**

Executar SellerPortalShellTests e AdminPortalAccessViewTests; esperado: novos links/contexto/contratos visuais ausentes.

- [ ] **Step 3: Atualizar shell e CSS localizado**

Ler o vendedor no contexto autorizado existente sem alterar identidade ou sessão. Padronizar menu lateral, fundo, espaçamento, tipografia, contraste, cards, campos e foco; reduzir a busca da carteira; compactar seu estado vazio; corrigir fundo e estado vazio dos rascunhos; padronizar links com ícones e estados. Fazer as rotas Admin fora da área renderizarem o contexto de tema do shell Admin e manter o Portal do Vendedor atento ao tema ativo. Não tocar CSS global do site público.

- [ ] **Step 4: Verificar os testes**

Reexecutar os filtros; esperado: aprovado para ambos os temas e breakpoints cobertos pelos contratos CSS.

- [ ] **Step 5: Commit**

Commit local: fix: unify seller portal shell and admin context

### Task 6: Regressão, build e verificação de Staging somente leitura

**Files:**
- Verify: Orofoods.Web/Orofoods.Web.csproj
- Verify: Orofoods.Web.Tests/Orofoods.Web.Tests.csproj
- Verify: arquivos alterados nas Tasks 1–5

**Interfaces:**
- Consumes: alterações das Tasks 1–5.
- Produces: build/testes Release e relatório das verificações browser/Staging que puderem ser feitas.

- [ ] **Step 1: Executar build Release**

Executar: dotnet build Orofoods.Web/Orofoods.Web.csproj --configuration Release

Esperado: zero erros.

- [ ] **Step 2: Executar testes focados**

Executar dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --configuration Release com filtros AdminUsersViewTests, LoginRedirectTests, AuthenticatedNavigationTests, AdminPortalAccessControllerTests, AdminPortalAccessViewTests, PortalControllerCatalogTests, CustomerAccessServiceTests, SellerWorkspaceServiceTests, SellerPortalShellTests, SelectCustomerViewTests, CustomerSearchViewTests e SellerPortalVisualContractTests.

Esperado: testes aprovados sem regressões de autenticação, autorização, auditoria ou escopo da carteira.

- [ ] **Step 3: Revisar o diff e UI disponível**

Executar git diff --check e revisar a lista de arquivos. Se houver navegador, percorrer busca/navegação, claro/escuro e larguras desktop, 360 px, 390 px e 768 px. Se não houver, relatar a limitação.

- [ ] **Step 4: Consultar Staging sem escrita**

Somente com acesso de leitura já disponível sem recuperar segredos, comparar clientes elegíveis do vendedor TARCISIO com a busca e o contexto apresentados pela aplicação. Não alterar vínculos nem sincronização. Sem acesso, registrar como bloqueado.

- [ ] **Step 5: Revisar e commitar a implementação**

Confirmar ausência de mudanças comerciais, WMC, pagamentos e migrations; fazer o commit local final. Não publicar nem integrar a branch.

## Handoff

A branch e worktree existentes estão limpas na base 61501d0. Nenhum código de produto foi alterado nesta etapa documental. A implementação aguarda aprovação da especificação e do plano; recomendo execução nativa pelo agente para manter consistentes os contratos de busca e sessão compartilhados.
