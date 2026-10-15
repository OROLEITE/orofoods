# OROFOODS — Correção consolidada de UI/UX e navegação dos portais


Status: proposta para aprovação antes da implementação
Base: origin/main, commit 61501d01cb451734e7eeb0112a861bc4004ab279
Branch/worktree: fix/consolidated-portal-ui-ux

## Objetivo

Corrigir os problemas de navegação, busca e apresentação identificados após o PR #106 nos fluxos administrativos e do Portal do Vendedor. Reutilizar serviços, o shell do vendedor, a sessão administrativa e os mecanismos de autorização existentes.

## Desenho

### Usuários e Acessos

Manter Editar, Redefinir senha e Histórico para o Administrador, junto das ações existentes. Corrigir abertura e empilhamento do menu em todas as linhas, inclusive a última, com mouse, teclado e toque. A correção atua no recorte do contêiner e na linha aberta, preservando rotas e permissões do backend.

### Central de Acesso e autenticação

Manter /Admin/Portais como Central de Acesso e dar à página três destinos: Painel Administrativo, seleção do Portal do Cliente e seleção do Portal do Vendedor. Preservar a auditoria existente e o acesso pelo menu administrativo.

Após login sem destino local válido, o Administrador vai para /Admin/Portais. Retornos locais válidos continuam preservados, inclusive seleção explícita de cliente. Destinos que poderiam criar loops ou abrir um portal sem contexto voltam para a Central. Destinos padrão de Cliente, Vendedor, GerenteComercial e Operador não mudam.

Nos dois portais acessados como Administrador, oferecer retorno à Central. No Portal do Cliente, manter a troca de cliente e identificar corretamente a Central no link existente. No Portal do Vendedor, oferecer Painel Administrativo, Central, troca de vendedor e seleção do Portal do Cliente. Mostrar o vendedor ativo no shell compartilhado quando houver contexto administrativo. A identidade e sessão continuam sendo as do Administrador; não há impersonação nem novo login.

### Busca de clientes

Usar o mesmo comportamento acessível nos três pontos: seleção administrativa de cliente, Minha Carteira e seleção de cliente para novo pedido. A pesquisa começa com um caractere, aguarda 300 ms, cancela ou ignora respostas antigas e não repete a mesma consulta. Limpar a consulta restaura o estado inicial.

Pesquisar nome fantasia, razão social, CNPJ normalizado e código WMC. O servidor retorna no máximo 20 opções, sem enviar toda a base ao navegador. A busca administrativa retorna somente clientes ativos e aprovados. A busca do vendedor deriva o vendedor do contexto autenticado e retorna somente clientes ativos, aprovados e vinculados a ele.

As opções mostram nome, razão social, CNPJ e código WMC. No seletor administrativo, Abrir Portal permanece desabilitado até uma opção válida ser selecionada; o POST revalida o cliente, mantém a sessão administrativa e registra auditoria. No novo pedido, a escolha continua sendo revalidada pelo serviço de carteira. A carteira atualiza resultados sem recarregar a página.

A UI permite teclado, mouse e toque, anuncia carregamento, erro e ausência de resultados e constrói conteúdo sem interpretar dados retornados como HTML.

### Shell e apresentação do Portal do Vendedor

Manter Areas/Vendedor/Views/_SellerLayout.cshtml como shell único e consolidar nele os links e o indicador de contexto administrativo. Corrigir fundo contínuo, espaçamento, contraste, campos, cards, botões e foco nas telas abrangidas. CSS alterado permanece escopado ao Admin ou ao Portal do Vendedor, sem regras globais sobre o site público.

As telas respondem ao tema ativo claro ou escuro. A seleção de vendedor usa a apresentação do Admin quando esse tema estiver ativo. Não forçar tema escuro globalmente nem criar mecanismo novo de persistência de preferência.

Manter menu lateral fixo, compacto e responsivo. Padronizar tipografia, ícones, espaçamento e estados hover/ativo dos links. No Dashboard, identificar o vendedor atual e indicar discretamente o contexto administrativo. Na carteira, reduzir campo/botão de busca e manter o estado vazio compacto. Nos rascunhos, corrigir o fundo cortado e o card sem resultados. Na seleção de cliente para novo pedido, preservar breadcrumbs e contraste.

Preservar indicadores e atalhos do Dashboard, a carteira vinculada ao vendedor e o ciclo de criação, edição e recuperação dos rascunhos. Não alterar regras de pedido, checkout, preço, pagamento ou integração WMC.

### Verificação da carteira no Staging

Fazer somente leitura de dados existentes para a carteira de TARCISIO: contar clientes ativos e aprovados vinculados ao vendedor selecionado, confirmar a identidade de vendedor usada pelo contexto administrativo e comparar com a resposta e apresentação da carteira. Se não houver clientes elegíveis, registrar que o zero está correto. Se houver diferença, localizar o filtro ou a apresentação responsável.

Não alterar vínculos, sincronização WMC, usuários ou configuração do ambiente. Não consultar Key Vault nem acessar valores secretos. Se a conexão de leitura ao Staging não estiver disponível, registrar a verificação como bloqueada, sem inferir o resultado.

## Segurança e limites

- Toda busca é protegida no backend e aplica filtros de cliente/vendedor na consulta ao banco.
- A seleção final é revalidada no servidor; uma sugestão não concede acesso.
- A sessão do Administrador, a auditoria e as restrições dos portais permanecem em vigor.
- Não mudar regras comerciais, permissões, WMC, pagamentos, dados, migrations, Staging ou Produção.
- Não carregar toda a base de clientes no navegador.
- Não fazer push, PR, merge ou deploy.

## Critérios de aceite

1. Ações mostra e permite usar Editar, Redefinir senha e Histórico em qualquer linha, em desktop e móvel.
2. O login administrativo padrão abre a Central; retornos seguros continuam corretos e os demais perfis mantêm destinos.
3. A Central contém três opções, preserva auditoria e pode ser aberta pelo menu e pelos portais.
4. O Portal do Vendedor mostra o contexto administrativo e links corretos, mantendo a sessão de Administrador.
5. As três buscas atendem campos, limite, debounce e filtros definidos; resposta antiga não sobrescreve a atual.
6. Seleções inválidas ou fora da carteira são recusadas no backend.
7. Telas suportam claro/escuro e larguras desktop, 360 px, 390 px e 768 px, quando houver navegador.
8. Build Release e testes focados passam; verificações de Staging/browser indisponíveis são relatadas, não simuladas.

## Não objetivos

Não redesenhar outros módulos do Admin ou do portal público. Não alterar dashboard comercial, cálculos de pedidos, filtros de negócio, WMC, pagamentos ou vínculos da carteira. Não criar migration.
