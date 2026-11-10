# Gestão de Usuários e Acessos no Admin

## Objetivo

Completar a área administrativa para que Administradores localizem, criem e mantenham contas, perfis e vínculos comerciais, redefinam senhas com Identity e consultem o histórico das alterações sem expor credenciais. O fluxo existente de acesso administrativo aos portais Cliente e Vendedor e as regras comerciais permanecem intactos.

## Estado atual

- `UsersController` e `AdminUserService` já fazem CRUD básico, atribuem um perfil existente, gerenciam status e vínculos Cliente/Vendedor, redefinem senha via ASP.NET Core Identity e impedem retirar o último Administrador ativo.
- O nome informado é gravado em `IdentityUser.UserName`, não há campo de nome separado, a busca não contempla um nome independente, o reset não exige troca posterior, e não existe histórico de gestão.
- O controller inteiro exige a role `Administrador`, mutações são POST com antiforgery, e `SecurityStampValidatorOptions.ValidationInterval` é zero.
- As migrations são mantidas em conjuntos separados para SQLite e PostgreSQL.

## Desenho aprovado

### Interface e capacidades

Em Admin → Configurações → Usuários e Acessos, oferecer pesquisa por nome completo, e-mail ou login; listagem compacta com e-mail cadastrado, status, perfil e vínculo comercial; criação, edição, ativação/desativação, redefinição de senha e histórico por usuário. Manter a atribuição de um perfil existente por conta, conforme o formulário atual; permissões continuam definidas pelas roles existentes e não será criado um editor de permissões novo. Nome completo e login são campos independentes. Cliente e vendedor permanecem vínculos opcionais, validados no servidor.

### Identity e sessões

Adicionar `FullName` e `MustChangePassword` a `ApplicationUser`. A migration preenche `FullName` das contas existentes com o `UserName` atual e `MustChangePassword` com `false`. Criação e redefinição podem marcar uma senha inicial/temporária para troca obrigatória. Um reset marcado como senha temporária redefine a senha pelo `UserManager`, marca a troca obrigatória e atualiza o security stamp; a senha temporária fica apenas no POST de cadastro/redefinição e nunca é incluída em telas de resposta ou auditoria.

Uma barreira server-side restringe a conta marcada à página de troca de senha e à saída da sessão. A troca usa `UserManager.ChangePasswordAsync`, limpa o indicador e atualiza o security stamp. Os demais endpoints, inclusive ações do Admin e portais, não ficam acessíveis antes da troca. Contas sem a marca continuam seguindo os fluxos atuais.

JWT API sessions also carry and validate the current Identity security stamp on every request, so password resets, status changes, and profile changes revoke existing bearer tokens. Accounts requiring a temporary password change cannot receive API tokens and cannot reuse that temporary credential.

### Auditoria e proteção

Persistir eventos para criação, alteração de dados, perfil/vínculos, status e redefinição/troca de senha. Cada evento identifica ator, alvo, ação, instante e resumo de campos alterados; não armazena senha, hash, token ou conteúdo de formulário sensível. A consulta do histórico exige `Administrador`. As mudanças de identidade e o respectivo evento são gravados de forma transacional onde suportado.

Toda ação continua autorizada no backend, usa antiforgery nas mutações web, respeita concurrency stamp, validadores de Identity e as validações atuais de vínculos. Ativar/desativar ou trocar perfil não pode deixar o sistema sem ao menos um Administrador ativo. Alterar identidade/status/perfil invalida sessões com security stamp.

### Dados e compatibilidade

Criar uma migration local para SQLite e outra para PostgreSQL, com os mesmos campos e tabela lógica de auditoria; atualizar snapshots e validar scripts/aplicação/rollback em bancos descartáveis locais. Não adicionar dependência SQLite ao projeto Web, que usa PostgreSQL em runtime. Não aplicar migrations fora do ambiente local. Não consultar nem modificar a conta real de Staging ou segredos do Key Vault. A homologação automatizada usa somente um usuário fictício com login `staging-customer-test` para provar pesquisa e exibição do e-mail cadastrado na interface.

### Aparência e limites

Reutilizar views, componentes, estilos e padrões compactos do Admin, preservando responsividade e temas claro/escuro. Não alterar regras comerciais, integração WMC, nem a implementação existente de acesso aos portais.

## Critérios de aceite

1. Administrador encontra conta por nome completo, e-mail ou login e vê o e-mail cadastrado; o usuário fictício `staging-customer-test` prova o fluxo sem dados reais.
2. Administrador cria/edita nome, login, e-mail, role, estado e vínculos validados.
3. Reset por Identity aceita senha temporária com troca obrigatória e invalida sessões anteriores; a barreira bloqueia ações até a troca.
4. Nenhuma view, resultado ou evento revela senha, hash ou token.
5. Nenhuma ação permite desativar/remover o perfil do último Administrador ativo; alterações sensíveis geram histórico consultável por Administradores.
6. Migrations SQLite e PostgreSQL são testadas localmente; build Release e testes focados passam, incluindo regressões do acesso administrativo aos portais Cliente/Vendedor.
7. A mudança fica em um segundo commit local sobre `4cee8cc`, sem push, PR, merge, deploy ou aplicação remota de migration.
