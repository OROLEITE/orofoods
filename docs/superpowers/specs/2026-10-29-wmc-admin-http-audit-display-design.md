# Diagnóstico HTTP/JSON WMC no Admin

## Objetivo

Permitir que Administradores investiguem tentativas WMC existentes na página de detalhes do pedido, usando os dados já persistidos em `WmcExportAudits`. Isso deve atender o pedido 17 e tentativas futuras sem executar novas chamadas ao WMC.

## Requisitos e limites

- Exibir status e resposta de `exportar.php`, ID de transmissão e código do vendedor, status e resposta de `nropedido.php`, erro detalhado da reconciliação e horários UTC e de Brasília com fuso explícito.
- Sanitizar segredos e dados pessoais reconhecíveis das respostas e erros. Manter a codificação HTML padrão do Razor; não usar `Html.Raw`.
- Manter o conteúdo na página Admin de detalhes do pedido, que já carrega as auditorias e exige a função `Administrador`.
- Não adicionar endpoints, consultas HTTP, campos persistidos, migrations, alterações Azure, estados ou lógica de retransmissão.
- Os horários exibidos pertencem ao registro da auditoria (`ExportedAt`). O modelo atual não guarda um horário separado para a consulta `nropedido.php`.

## Abordagens

1. **Estender o histórico Admin existente (escolhida).** Apresentar os campos carregados na auditoria com um formatador puro para mascaramento e conversão explícita `America/Sao_Paulo`. Evita consultas e superfícies de acesso novas.
2. Criar uma rota/endpoint de diagnóstico separado. Duplicaria carregamento, autorização e contrato de saída, sem benefício porque a página atual já recebe a auditoria.

## Segurança e aceitação

- Valores ausentes aparecem como não registrados.
- Respostas e erros têm segredos, identificadores pessoais e dados de cliente reconhecíveis mascarados e continuam codificados pelo Razor.
- A tela rotula explicitamente UTC e Brasília com offset. A hora persistida permanece intacta.
- Marco 16, `AutomaticWorkerEnabled=true`, idempotência e bloqueios contra duplicidade permanecem intactos.
- Testes cobrem mascaramento, conversão de fuso, campos renderizados e autorização administrativa.
