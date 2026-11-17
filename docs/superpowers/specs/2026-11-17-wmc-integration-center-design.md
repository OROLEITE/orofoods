# Central de Integração WMC — Design

## Intenção

Modernizar a tela Admin `/Admin/Integrations/Wmc` para funcionar como uma Central de Integração WMC compacta, legível e responsiva, sem alterar regras de negócio, endpoints, permissões, Firebird/WMC, banco de dados ou workers automáticos.

O público é o administrador que consulta o estado da integração, executa importações manuais de clientes e produtos e investiga a elegibilidade de um `Order.Id`.

## Objetivos

- Organizar a experiência em quatro abas: **Visão Geral**, **Clientes**, **Produtos** e **Diagnóstico**.
- Exibir somente dados existentes: estado de consulta WMC, estado da execução manual, última operação e diagnósticos já fornecidos pelo servidor.
- Preservar os fluxos manuais atuais de prévia, confirmação, regularização, importação e simulação de produtos.
- Dar busca instantânea, contadores, estados de carregamento, mensagens acessíveis e bloqueio de cliques duplicados.
- Aplicar o padrão dark do Admin, com ações primárias `#0E7490`, hover `#0B5D75`, texto branco, cards compactos e layout adaptável.
- Manter o diagnóstico técnico e a elegibilidade por `Order.Id` em painel expansível.

## Não objetivos e antiobjetivos

- Não criar ou alterar endpoints, contratos de serviço, regras de seleção, regras comerciais ou persistência.
- Não habilitar `WmcSyncWorker`, `WmcJsonOrderAutomaticBackgroundService`, `WmcFileDrop` ou qualquer transmissão automática.
- Não criar dados de demonstração, contadores fixos ou estados simulados.
- Não remover o acesso a operações existentes; a reorganização visual não deve mudar autorizações nem tokens antiforgery.
- Não executar Firebird real, banco remoto, Staging, publicação ou deploy durante esta tarefa.

## Abordagens consideradas

1. Reorganizar a view existente, adicionando uma camada de abas em JavaScript, markup semântico, CSS escopado e pequenos atributos de estado. É a abordagem escolhida por preservar os endpoints e os bindings já testados, reduzindo o risco de alteração de negócio.
2. Dividir a tela em parciais Razor por aba. Melhoraria a separação de arquivos, mas aumentaria a chance de perder bindings e mensagens em uma view que já contém vários estados pós-POST.
3. Criar um novo view model e uma nova leitura de saúde do Firebird para a visão geral. Produziria uma API de apresentação mais explícita, mas ampliaria o controller e introduziria uma operação de integração adicional desnecessária.

## Decisões de construção

- A view continua usando `WmcPreviewPageModel`, `ViewBag.IsRunning`, `ViewBag.LastRun`, `WmcCustomerImportPreview`, `WmcCustomerRegularizationPreview` e o componente existente `WmcActivationDiagnostics`.
- A conexão WMC será descrita por estado real da consulta de marcas que a própria tela já realiza: sucesso de carregamento, falha conhecida ou ausência de consulta naquele estado. Nenhuma conexão nova será criada apenas para pintar um card.
- A Visão Geral terá indicadores derivados dos dados reais disponíveis: consulta WMC, execução manual/coordenador, última operação e totais da última execução. Quando não houver registro, a interface exibirá uma ausência explícita em vez de inventar zero.
- Clientes manterá os POSTs atuais `WmcCustomerPreview`, `WmcCustomerImport`, `WmcCustomerRegularizationPreview` e `WmcCustomerRegularization`, com busca/filtro da prévia e confirmação protegida contra duplo envio.
- Produtos terá seleção múltipla para a prévia geral existente via `WmcPreview`, além do fluxo de importação individual já suportado por `WmcProducts`, `WmcConfirmSelection` e `WmcImportProducts`. A seleção de produtos e as três etapas do importador atual permanecem intactas.
- Diagnóstico continuará sendo renderizado pelo componente `WmcActivationDiagnostics`, apenas movido para a aba correspondente e envolvido por `details`/painel expansível sem expor credenciais ou connection strings.
- O comportamento de abas será acessível: `role=tablist`, `role=tab`, `role=tabpanel`, `aria-selected`, foco por teclado, hash opcional e seleção inicial coerente com o estado pós-POST ou com `diagnosticOrderId`.
- O JavaScript será extraído para `wwwroot/js/wmc-integration-center.js` quando isso seguir o padrão do layout; caso a integração de scripts da view exija inline, o código permanecerá estritamente escopado à página. A preferência é arquivo dedicado para facilitar testes e evitar scripts duplicados.
- O CSS ficará em `wwwroot/css/admin-integrations.css`, reaproveitando tokens `--admin-dark-*` e os seletores WMC existentes, com media queries para desktop, tablet e celular.

## Decisões fechadas na revisão do design

- “Última operação” na Visão Geral será rotulada como **Última sincronização manual** e virá exclusivamente de `WmcSyncCoordinator.LastRun`; importações diretas de clientes/produtos continuarão mostrando seus próprios feedbacks e não serão misturadas com os totais do ciclo global.
- O estado de conexão terá três estados honestos: **Disponível** quando a consulta de marcas existente terminar com sucesso, inclusive com lista vazia; **Indisponível** quando essa consulta falhar; e **Não consultado** em respostas que não executaram essa leitura. O controller só transportará esse resultado já conhecido, sem criar uma chamada adicional.
- Depois de POST, o estado do servidor vence o hash: `Model.Step` abre Produtos, uma prévia de clientes/regularização abre Clientes e `diagnosticOrderId` abre Diagnóstico. Em GET sem estado de workflow, o hash pode escolher a aba inicial; sem hash, a inicial é Visão Geral.
- `diagnosticOrderId` abre automaticamente Diagnóstico, mantém o parâmetro no formulário de consulta e coloca foco no resultado quando houver resultado; submissões de outras abas não carregarão esse parâmetro artificialmente.
- O indicador de execução será explicitamente **Sincronização manual/coordenador** e refletirá apenas `IsRunning`/`LastRun`. Operações diretas terão seus estados na própria aba e nas mensagens retornadas pelos endpoints existentes.

## Arquivos previstos

- Modificar `Orofoods.Web/Areas/Admin/Views/Integrations/Wmc.cshtml` para a estrutura das quatro abas, indicadores, mensagens, carregamento e preservação dos formulários.
- Modificar `Orofoods.Web/Areas/Admin/Views/Shared/Components/WmcActivationDiagnostics/Default.cshtml` apenas para o painel expansível e a apresentação compacta, preservando todos os campos técnicos existentes.
- Modificar `Orofoods.Web/wwwroot/css/admin-integrations.css` para o tema, cards, abas, tabelas, estados e breakpoints responsivos.
- Criar `Orofoods.Web/wwwroot/js/wmc-integration-center.js` para tabs, busca/filtros, contadores, loading e bloqueio de duplo clique, se o layout permitir carregamento dedicado.
- Modificar `Orofoods.Web/Areas/Admin/Controllers/IntegrationsController.cs` somente se for necessário transportar um estado de consulta já obtido para a Visão Geral; não serão adicionadas chamadas ou endpoints.
- Criar ou estender testes de contrato de view em `Orofoods.Web.Tests/Views/` para as quatro abas, preservação dos actions, tokens, diagnósticos, valores cromáticos, estados e responsividade por seletores.

## Verificação planejada

- Executar primeiro os testes focados de views WMC e observar a falha dos novos contratos antes da implementação.
- Executar os testes focados novamente após cada bloco funcional.
- Executar `dotnet build Orofoods.Web/Orofoods.Web.csproj -c Release --no-restore`.
- Executar `dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj -c Release --no-restore`.
- Executar `git diff --check` e revisar o diff completo para confirmar que só a interface, seus testes e o documento foram alterados.
- Não declarar correção visual/responsiva sem validação no navegador; se o ambiente de browser estiver disponível, validar desktop, tablet e celular com a tela local e sem autenticação remota.
