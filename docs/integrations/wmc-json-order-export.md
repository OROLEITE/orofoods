# Exportacao automatica de pedidos WMC por JSON

O transmissor segue o contrato extraido do WMC Mobile:

- POST em {BaseUrl}/exportar.php?imei={DeviceId}&serial={SalesRepresentativeSerials[SalesRepresentativeId]}.
- Corpo application/x-www-form-urlencoded com o unico campo HTTP_JSON.
- HTTP_JSON e codificado em UTF-8 e URL-encoded como no URLEncoder do aplicativo: espacos viram +, caracteres seguros sao letras, digitos e *._-, e os outros bytes UTF-8 usam %HH maiusculo.
- O Content-Type nao declara charset, como no helper form-urlencoded do aplicativo.
- Sucesso exige HTTP 200 e corpo exatamente "importado com sucesso" depois de Trim(), com comparacao ordinal.

## JSON emitido

O JSON e um array plano: um objeto pedidos seguido pelos objetos ped_prods dos itens.

Order.Id permanece a chave interna da Orofoods. O campo id do WMC usa WmcTransmissionId, uma sequencia PostgreSQL propria iniciada em 1. O mesmo valor e usado no cabecalho e nos itens. WmcTransmissionId e persistido no pedido, tem indice unico e e mantido em tentativas posteriores. O sequence PostgreSQL nao recicla valores.

nropedido e enviado como inteiro 0 para cabecalho e itens, seguindo o padrao do APK para um pedido sem numero atribuido. Apos o POST confirmado, o aplicativo consulta nropedido.php por POST sem corpo, com idpedido igual ao id transmitido e codvendedor igual ao codigo WMC real. O codigo do vendedor deve estar entre 1 e 32767, conforme o short usado pelo APK. A resposta esperada e um array JSON cujo primeiro objeto contem NROPEDIDO positivo; zero ou valores negativos continuam pendentes.

O cabecalho usa idcliente como Order.CustomerId, codcliente como codigo WMC textual real, codvendedor como codigo WMC numerico configurado, totqtde como soma das quantidades, valprods, valtotal e valorfrete como numeros, e obs como texto. Datas disponiveis usam os formatos observados no aplicativo. dataentrega so aparece quando ha data solicitada.

Cada item usa nroitem sequencial a partir de 1, codproduto textual real, descricao, qtde, preco, valtotal, preco_s_desc, un e undig com os tipos do contrato. Nenhuma propriedade e serializada como JSON null.

nrodocumento e nroserie permanecem como 0, defaults comprovados para o ramo sem serie do APK. nromanifesto, nroest e datacontingencia permanecem omitidos no ramo aplicavel. Campos sem fonte equivalente sao omitidos. O transmissor nao calcula nem valida preco, credito, estoque ou prazo.

O construtor recusa pedidos nao persistidos, clientes ou produtos sem codigo WMC e vendedores sem associacao WMC explicita.

## Homologacao controlada

Mantenha os valores fora do repositorio, em configuracao protegida:

WmcJsonOrder__Enabled=true
WmcJsonOrder__AutomaticWorkerEnabled=false
WmcJsonOrder__HomologationWorkerEnabled=true
WmcJsonOrder__HomologationOrderId=<Order.Id selecionado>
WmcJsonOrder__HomologationOrderIdConfirmedUnique=true
WmcJsonOrder__BaseUrl=<URL de homologacao emitida pelo WMC>
WmcJsonOrder__DeviceId=<deviceId exclusivo da Orofoods emitido pelo WMC>
WmcJsonOrder__SalesRepresentativeCodes__<SalesRepresentativeId>=<codigo WMC real>
WmcJsonOrder__SalesRepresentativeSerials__<SalesRepresentativeId>=<serial proprio do vendedor emitido pelo WMC>

O worker HTTP de homologacao e separado de WmcFileDrop__Enabled e consulta apenas o Order.Id configurado, se estiver Pending. Nao percorre outros pedidos. Para uma homologacao de um pedido, mantenha `AutomaticWorkerEnabled=false`; essa flag pertence ao worker geral e nao respeita `HomologationOrderId`.

Antes de homologar, confira no PostgreSQL da Orofoods qual sera o proximo WmcTransmissionId e confirme em consulta somente leitura ao WMC que esse id ainda nao existe no ambiente alvo. O identificador nao deriva de Order.Id. Mantenha outros transmissores JSON desligados entre a verificacao e o envio selecionado. A flag HomologationOrderIdConfirmedUnique registra a confirmacao operacional; ela nao faz a consulta por conta propria. Os testes locais nao consultam o WMC.

O worker TXT continua independente e usa WmcFileDrop__Enabled. Durante a homologacao HTTP controlada, mantenha-o desligado para evitar disputa pelo mesmo pedido.

## Processamento automatico geral

`WmcJsonOrder__AutomaticWorkerEnabled` e falso por padrao e so tem efeito junto com `WmcJsonOrder__Enabled=true`. Quando habilitado, um worker separado do FileDrop consulta a cada cinco minutos ate 20 pedidos `Approved` com integracao `Pending`. Ele usa a reserva atomica e bloqueios de auditoria/estado existentes; nao seleciona `Failed`, nao reenvia tentativas `Started`/`Unknown` e exclui pedidos com artefato TXT. Uma resposta ambigua aciona somente a consulta `nropedido.php`; um numero positivo confirma a importacao, enquanto a ausencia de numero mantem `Unknown` e bloqueia nova exportacao. A reconciliacao manual posterior tambem consulta somente `nropedido.php`.

Ativar essa flag permite o envio de todos os pedidos elegiveis da fila, nao de um pedido escolhido por `HomologationOrderId`. O servico de integracao mantém o bloqueio existente fora dos ambientes Development, Staging e Test. Para homologar um pedido isolado, use somente `HomologationWorkerEnabled` com `HomologationOrderId` e `HomologationOrderIdConfirmedUnique`, mantendo `AutomaticWorkerEnabled=false`. A flag do worker geral deve permanecer desligada ate haver aprovacao operacional para a fila completa.

## Importacao manual selecionada

A fila `Pedidos pendentes WMC` permite que Administrador, Vendedor e Operador importem somente o pedido escolhido, após confirmação com número, cliente e total. Vendedores veem apenas clientes do próprio escopo; administradores e operadores usam seus acessos autorizados à fila. O endpoint exige POST com antiforgery e grava o usuário, o resultado de `exportar.php` e a reconciliação de `nropedido.php` na auditoria existente.

O envio manual exige as duas chaves abaixo; `ManualImportEnabled` é falso por padrão. Essa opção libera somente o fluxo iniciado por uma pessoa. O worker de homologação automática continua restrito ao `HomologationOrderId` e não é alterado.

WmcJsonOrder__Enabled=true
WmcJsonOrder__ManualImportEnabled=true

A fila inclui pedidos recebidos enquanto aguardam aprovação, mas só libera a ação depois da aprovação e quando não há exportação TXT, importação anterior, tentativa ativa ou resultado incerto. O serviço reserva o pedido e persiste a tentativa antes da chamada HTTP. Uma confirmação do WMC marca o pedido como Importado; se `nropedido.php` não retornar o número, a importação continua bloqueada para reenvio e a tela informa que a reconciliação está pendente.

## Auditoria e duplicidade

O pedido e reivindicado atomicamente antes do envio. A auditoria registra o identificador da tentativa, WmcTransmissionId, codigo WMC do vendedor e URL base do destino usados, estado, resposta e status HTTP do POST, alem da resposta e status da consulta de NROPEDIDO.

Erro de rede, status inesperado, corpo de POST diferente do texto exato ou falha ao persistir a resposta deixam a transmissao Unknown; pedidos nesse estado nao sao reenviados automaticamente. Se o POST for confirmado e a consulta de NROPEDIDO falhar, a transmissao permanece Succeeded. Um administrador pode acionar Consultar numero WMC na tela de Integracoes ERP; essa acao repete somente nropedido.php usando o destino, WmcTransmissionId e codvendedor salvos na auditoria. Ela nao chama exportar.php e deixa o numero pendente enquanto NROPEDIDO nao for positivo.

## Validacao local

Os testes usam handlers HTTP simulados e o script PostgreSQL e gerado sem aplicar migracoes. Nenhum pedido foi transmitido e nenhum servidor WMC foi acessado. A concorrencia real da alocacao PostgreSQL ainda precisa ser exercitada em uma instancia descartavel de PostgreSQL.
