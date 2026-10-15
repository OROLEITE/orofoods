# Gestão de Usuários e Acessos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Completar a gestão administrativa de contas, perfis, vínculos, senhas temporárias e auditoria com ASP.NET Core Identity.

**Architecture:** Estender `ApplicationUser` e `AdminUserService`, manter mutações protegidas em `UsersController`, registrar auditoria tipada no mesmo contexto e impor troca temporária com uma barreira server-side. Views Admin reutilizam Identity, os vínculos e os estilos atuais.

**Tech Stack:** .NET / ASP.NET Core MVC / ASP.NET Core Identity / EF Core / SQLite e PostgreSQL / xUnit.

**Spec:** `docs/superpowers/specs/2026-10-15-admin-user-management-design.md`

## Global Constraints

- Todas as mutações administrativas exigem role `Administrador` e antiforgery.
- Senhas, hashes e tokens não entram em views, mensagens ou auditoria.
- Só roles já existentes podem ser atribuídas; vínculos Cliente/Vendedor são validados no servidor.
- A senha temporária bloqueia as rotas autenticadas até a troca concluída.
- A conta real de Staging, Key Vault, WMC e acesso aos portais ficam fora de alterações operacionais.
- Criar migrations para SQLite e PostgreSQL, mas aplicá-las somente em bancos locais descartáveis.
- Preservar o commit `4cee8cc`; produzir somente um novo commit local para esta funcionalidade.

## Review Focus

- Nome, login e e-mail repetidos ou inválidos devem manter unicidade e mensagens sem expor dados sensíveis.
- Último Administrador ativo não pode ser desativado nem perder a role, inclusive em submissões concorrentes.
- A marca temporária deve bloquear rotas MVC/API autenticadas sem bloquear a troca de senha ou logout.
- Erros de auditoria/transação não podem deixar alteração de perfil/status sem seu evento correspondente.
- O usuário fictício `staging-customer-test` deve ser local ao teste; a busca deve renderizar o e-mail sem credenciais.

---

### Task 1: Persistência de identidade e auditoria

**Files:**
- Create: `Orofoods.Web/Models/Identity/AdminUserChangeAudit.cs`
- Modify: `Orofoods.Web/Models/Identity/ApplicationUser.cs`
- Modify: `Orofoods.Web/Data/ApplicationDbContext.cs`
- Create: migration SQLite `*_AddAdminUserManagement` e PostgreSQL `*_AddAdminUserManagementPostgreSql`
- Modify: snapshots EF Core SQLite e PostgreSQL
- Test: `Orofoods.Web.Tests/Data/AdminUserManagementMigrationTests.cs`

**Interfaces:** `ApplicationUser.FullName: string`, `ApplicationUser.MustChangePassword: bool`, `ApplicationDbContext.AdminUserChangeAudits: DbSet<AdminUserChangeAudit>`. A entidade registra `Id`, `ActorUserId`, `ActorName`, `TargetUserId`, `TargetName`, `Action`, `Summary`, `ChangedAt`.

- [x] Escrever testes que confirmam schema, colunas Identity e default/backfill da migration em SQLite; confirmar metadados PostgreSQL pelo conjunto de migrations.
- [x] Executar testes para observar falha pela ausência do modelo/migration.
- [x] Adicionar campos, entidade e mapping; gerar a migration PostgreSQL pelo design-time factory existente e escrever a equivalente SQLite sem adicionar o provider SQLite ao projeto Web; atualizar ambos os snapshots.
- [x] Executar testes para confirmar schema, backfill `FullName = UserName` e `MustChangePassword = false`.

### Task 2: Serviço Identity, roles, vínculos e auditoria

**Files:**
- Modify: `Orofoods.Web/Services/Identity/AdminUserService.cs`
- Modify: `Orofoods.Web.Tests/Services/AdminUserServiceTests.cs`
- Test: `Orofoods.Web.Tests/Services/AdminUserAuditServiceTests.cs` (ou teste do serviço existente)

**Interfaces:** `CreateAdminUserInput`/`UpdateAdminUserInput` mantêm `Name` como nome completo e recebem `UserName` independente como campo opcional no fim do record para preservar callers existentes; create também recebe `MustChangePassword`. `AdminUserService` resolve o ator da claim `NameIdentifier` em `IHttpContextAccessor` e rejeita mutações sem ator. Implementar `ResetPasswordAsync(string userId, string password, bool mustChangePassword)`, `ChangeTemporaryPasswordAsync(string userId, string currentPassword, string newPassword)` e `GetHistoryAsync(string targetUserId, int take = 100)`. Todos retornam resultado Identity ou lista tipada de eventos.

- [x] Adicionar testes para busca/atualização independente de nome e login, unicidade de login/e-mail, vínculo válido, bloqueio do último Administrador, reset via Identity, marca temporária, security stamp e auditoria sem credenciais.
- [x] Executar os testes e confirmar falhas esperadas nos campos/operações ainda ausentes.
- [x] Implementar validação Identity, transações, invalidação de sessões, auditoria de criação/edição/role/vínculos/status/reset e consulta limitada do histórico.
- [x] Executar novamente os testes de serviço; todos devem passar e os payloads de auditoria não devem conter campos de senha/token/hash.

### Task 3: Barreira e troca obrigatória de senha

**Files:**
- Create: `Orofoods.Web/Infrastructure/MustChangePasswordMiddleware.cs`
- Create: `Orofoods.Web/Areas/Identity/Controllers/AccountController.cs`
- Create: `Orofoods.Web/Areas/Identity/Views/Account/ChangeTemporaryPassword.cshtml`
- Modify: `Orofoods.Web/Program.cs`
- Test: `Orofoods.Web.Tests/Authorization/MustChangePasswordTests.cs`

**Interfaces:** `MustChangePasswordMiddleware.InvokeAsync(HttpContext, UserManager<ApplicationUser>)` consulta a conta autenticada e, quando a marca está ativa, permite apenas `POST/GET /Identity/Account/ChangeTemporaryPassword` e a saída `/Identity/Account/Logout`; os demais caminhos respondem 403 para API e redirecionam páginas para a troca. O controller POST usa `ChangeTemporaryPasswordAsync` e antiforgery.

- [x] Escrever testes de integração que provam bloqueio de rotas protegidas e acesso à troca/logout para conta temporária.
- [x] Executar testes para observar bloqueio ainda não implementado.
- [x] Implementar barreira na ordem correta do pipeline após roteamento/autenticação e antes da autorização; implementar POST antiforgery de troca.
- [x] Confirmar que após trocar a senha as rotas autenticadas voltam a responder e a marca/sessão são renovadas.

### Task 4: Interface Admin e histórico

**Files:**
- Modify: `Orofoods.Web/Areas/Admin/Controllers/UsersController.cs`
- Modify: `Orofoods.Web/ViewModels/AdminUserViewModels.cs`
- Modify: views `Areas/Admin/Views/Users/{Index,Create,Edit,ResetPassword}.cshtml`
- Create: view de histórico do usuário
- Modify: `Views/Shared/_AdminNavigation.cshtml` e estilos compactos dos usuários
- Test: `Orofoods.Web.Tests/Views/AdminUsersViewTests.cs`
- Test: `Orofoods.Web.Tests/Authorization/AdminUsersAuthorizationTests.cs`

**Interfaces:** GET `History(id)` protegido por `Administrador`; listagem pesquisa `FullName`, `Email` e `UserName`, incluindo e-mail visível; reset recebe senha, confirmação e `MustChangePassword` sem renderizar segredos de retorno.

- [x] Adicionar testes de view/controller para caminho Configurações → Usuários e Acessos, campos separados nome/login, busca por login/e-mail/nome, formulário de senha temporária, histórico e antiforgery.
- [x] Executar os testes para confirmar as lacunas esperadas.
- [x] Implementar formulários e controller, preencher login/e-mail do usuário pesquisado e histórico auditável, preservando temas claro/escuro e layout responsivo.
- [x] Rodar os testes e inspecionar HTML renderizado para confirmar ausência de hashes/tokens e CSRF nas mutações.

### Task 5: Homologação local, regressão e commit

**Files:** apenas correções nos arquivos das tarefas anteriores.

- [x] Usar fixture local `staging-customer-test` com e-mail sintético e provar busca/renderização/reset sem alterar ambiente Staging.
- [x] Aplicar e reverter a migration SQLite em banco local descartavel; validar as operacoes e o script gerado da migration PostgreSQL. O daemon PostgreSQL local nao estava disponivel para aplicacao fisica.
- [x] Executar testes focados de Identity, autorização, login, usuário, vínculos, auditoria e migrations; executar regressões da seleção/acesso administrativo aos portais Cliente e Vendedor.
- [x] Executar build Release da solução e `git diff --check`; revisar o diff inteiro e o estado da worktree.
- [x] Confirmar que `4cee8cc` permanece ancestral e criar um único commit novo, local, com assunto `feat: manage admin users and access`.
