# Importação manual JSON WMC por pedido

## Intenção

Adicionar ao grid administrativo de pedidos uma ação explícita para importar um
único pedido elegível ao WMC via o transmissor JSON já existente, mantendo a
gravação do pedido e o TXT manual independentes.

## Escopo e restrições

- A fila e a ação ficam disponíveis para Administrador, Vendedor e Operador;
  o vendedor só acessa pedidos de clientes do próprio escopo.
- A ação é POST com antiforgery e confirmação contendo número, cliente e valor.
- Só aparece para pedido aprovado, pendente/falho de integração, com a
  integração e `ManualImportEnabled` explicitamente habilitados e sem auditoria
  JSON/TXT em andamento, incerta ou concluída.
- O mesmo `WmcJsonOrderErpIntegration` e `OrderIntegrationService` serão
  reutilizados; nenhum transmissor, fila ou migration nova será criada.
- `ManualImportEnabled` será falso por padrão. O worker automático permanece
  restrito à configuração de homologação atual.
- Estados `Started`, `Unknown`, `Succeeded` e `WmcOrderExportState.Imported`
  bloqueiam novo envio. Uma falha determinística pode ser tentada novamente;
  resultado incerto permanece bloqueado.
- Não haverá alteração de regras comerciais, cadastro, banco, Azure, deploy ou
  transmissão real durante os testes.

## Abordagem escolhida

1. Reservar atomicamente o pedido e persistir essa reserva antes da chamada
   HTTP; retornar o resultado selecionado e bloquear auditorias JSON/TXT
   ativas, incertas ou concluídas.
2. Adicionar uma fila compartilhada e uma ação POST antiforgery que valida o
   escopo do usuário, chama o transmissor JSON e informa o resultado.
3. Expor somente linhas elegíveis com confirmação nativa de pedido, cliente e
   total. Confirmar o estado Importado e mostrar o número reconciliado.

## Testes

Cobrirão: elegibilidade e marcação no grid, acesso dos três perfis e escopo de
vendedor, confirmação/antiforgery, sucesso com reconciliação, falha determinística,
bloqueio de auditoria incerta ou duplicada e concorrência de reivindicações.
