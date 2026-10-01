# Operação: cartão na entrega com Point

## Preparação administrativa

1. Em **Administração → Motoristas**, cadastre o motorista e mantenha-o ativo.
2. Em **Administração → Terminais Point**, cadastre o terminal Mercado Pago. Para este fluxo de validação, use somente o dispositivo virtual `SBX0000001`.
3. Em **Vínculos de terminais**, associe o motorista ao terminal. O vínculo ativo é histórico e somente um motorista pode usar o terminal por vez.
4. Configure `ASPNETCORE_ENVIRONMENT=Test`, `Payments__CardOnDeliveryEnabled=true` e `Payments__MercadoPagoPointEnabled=true` apenas no processo local de validação. A aplicação força Cartão na Entrega desligado fora de `Test`; o Point também é recusado fora desse ambiente.

## Cobrança e estados

No detalhe de um pedido **Cartão na entrega**, selecione o vínculo ativo e use **COBRAR NA MAQUININHA**. A tentativa e a chave de idempotência ficam persistidas antes da chamada ao Point. Repetir o envio após timeout reutiliza a tentativa e a mesma chave; não crie outra cobrança manualmente.

O status pode ser **Aguardando pagamento**, **Aguardando terminal**, **Ação necessária no terminal**, **Pagamento aprovado**, **Pagamento recusado**, **Pagamento cancelado**, **Cobrança expirada**, **Pagamento estornado** ou **Erro de comunicação**. A tela consulta o status por até 60 segundos, a cada 5 segundos, e encerra a consulta ao receber um estado final.

## Retry e entrega

- Em timeout ou falha de comunicação, a tentativa fica preservada. Aguarde a consulta automática ou use **Consultar status** antes de tentar novamente.
- Uma tentativa ainda ativa não pode ser duplicada. Depois de rejeição, cancelamento ou expiração, o operador pode iniciar nova tentativa se os guards continuarem válidos.
- `action_required` exige que o operador verifique o terminal e consulte novamente; não inicia uma nova cobrança enquanto a tentativa estiver ativa.
- **Delivered** fica bloqueado até o pagamento estar aprovado/pago. O backend também rejeita a transição antes da aprovação.
- Estorno é mostrado como estado final e não libera uma nova entrega.

O histórico administrativo registra pedido, motorista, terminal, vínculo, usuário Admin, horário, order externa, status, número da tentativa e resultado sanitizado. Ele não armazena credenciais, cabeçalhos Authorization ou dados do cartão.
