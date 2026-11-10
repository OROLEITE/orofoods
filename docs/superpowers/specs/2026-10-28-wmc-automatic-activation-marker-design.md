# Marco seguro para ativar o processamento automático WMC

## Intenção

Permitir a ativação futura do worker HTTP/JSON somente para pedidos criados depois do marco de ativação no Staging. A primeira ativação não deve selecionar pedidos históricos que já estejam aprovados e pendentes.

## Requisitos e limites

- O worker deve continuar desabilitado por padrão.
- O worker não processa nada sem um marco de ativação válido.
- Pedidos com `Order.Id` menor ou igual ao marco nunca entram na fila automática.
- A seleção do worker e a reserva atômica do pedido aplicam o mesmo limite.
- O marco é obtido por leitura de `MAX(Order.Id)` no Staging imediatamente antes da ativação; nenhum pedido é atualizado.
- A transmissão continua exclusivamente HTTP/JSON, com auditoria e reconciliação existentes.
- FileDrop/TXT continua manual; não há migração, alteração de pedido ou transmissão durante a implementação.
- A ativação das flags só ocorre depois do merge e deploy aprovados no Staging.

## Abordagens consideradas

1. **Maior `Order.Id` existente (selecionada):** usa um high-water mark preciso e monotônico; requer consultar o maior ID no Staging antes de ativar.
2. **Timestamp UTC em `CreatedAt`:** evita a consulta prévia, mas depende de timestamps históricos, conversões de fuso e ausência de backfills.

## Desenho

Adicionar `WmcJsonOrder__AutomaticWorkerStartAfterOrderId` como configuração opcional. O worker e o serviço de reserva falham fechados quando o valor está ausente ou inválido e aceitam somente `Approved` + `Pending` com `Id > AutomaticWorkerStartAfterOrderId`. Permanecem os bloqueios atuais para TXT exportado, transmissão iniciada, confirmação anterior e resultado incerto.

Depois do merge e deploy no Staging, a ativação controlada lê `MAX(Id)` em modo somente leitura e grava esse valor como marco junto às flags `WmcJsonOrder__Enabled=true` e `WmcJsonOrder__AutomaticWorkerEnabled=true`; FileDrop automático permanece desabilitado. O primeiro pedido elegível deve ser acompanhado pela auditoria HTTP e pela reconciliação `nropedido.php` antes de declarar a integração confirmada.

## Testes

- Sem marco, mesmo com as flags de execução habilitadas, o worker não cria escopo nem transmite.
- Pedidos aprovados pendentes com ID até o marco são ignorados; um pedido posterior pode ser processado.
- A reserva atômica também recusa IDs até o marco.
- Os testes usam handlers HTTP simulados e não transmitem pedidos reais.
