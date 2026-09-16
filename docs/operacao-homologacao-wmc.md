# Homologação WMC por arquivo

## Objetivo

Validar a geração manual de pedidos Orofoods em arquivo `.txt` antes de habilitar qualquer automação com o ERP WMC.

O arquivo atual é baseado exclusivamente no exemplo recebido. Ele é adequado para teste controlado, mas não substitui o layout oficial, suas regras de obrigatoriedade, tabelas de códigos e retorno de processamento.

## Pré-requisitos

1. PostgreSQL acessível e aplicação em execução.
2. Diretório compartilhado ou local autorizado para entrada do WMC.
3. `WmcFileDrop:Enabled` configurado somente no ambiente de homologação.
4. `WmcFileDrop:AutoRetryEnabled` mantido como `false`.
5. Cliente aprovado com o código WMC preenchido.
6. Todos os produtos do pedido com o código WMC preenchido.

Exemplo de configuração de homologação:

```json
"WmcFileDrop": {
  "Enabled": true,
  "AutoRetryEnabled": false,
  "OutputDirectory": "C:\\WMC\\NEOGRID\\IN"
}
```

## Roteiro de teste

1. No painel Orofoods/JPL, confirme o código WMC do cliente em **Clientes**.
2. Confirme o código WMC de cada item em **Produtos**.
3. Crie ou selecione um pedido confirmado com endereço e condição de pagamento válidos.
4. Abra **Integrações ERP** e use **Exportar WMC** no pedido desejado.
5. Verifique o download do arquivo e, quando aplicável, a entrada no diretório configurado.
6. Abra o detalhe do pedido e confirme o novo item no histórico de exportações WMC.
7. No painel de integrações, confirme data, operador, nome do arquivo e resultado da última exportação.
8. Refaça a exportação do mesmo pedido e confirme que o arquivo existente não é sobrescrito pelo adaptador de arquivo.

## Resultado esperado

- Arquivo com o nome `WMC_<numero-do-pedido>.txt`.
- Cliente e produtos sem código WMC impedem a exportação e registram uma auditoria de falha.
- Exportação bem-sucedida registra usuário, e-mail, data e arquivo no pedido.
- Uma falha resolvida por exportação posterior não permanece como pendência no indicador do painel.
- O endpoint `/health` retorna `erp: homologation-file-drop-ready` quando o diretório configurado existe.

## Evidências para guardar

- Número do pedido Orofoods.
- Arquivo exportado.
- Captura ou retorno do WMC após a importação.
- Resultado do histórico de exportações WMC no painel.
- Referência `X-Correlation-ID` do navegador se houver erro.

## Limites antes da produção

Não habilitar retentativa automática ou tratar a exportação como integração definitiva antes de receber e validar:

- Layout WMC oficial e sua versão.
- Regras de cada tipo de registro e seus tamanhos.
- Codificações, separadores e final de linha exigidos.
- Tabelas de códigos para cliente, produto, condição de pagamento, endereço e transportadora.
- Arquivo ou API de retorno, confirmação de recebimento e tratamento de rejeições.
- Cenários de cancelamento, alteração, duplicidade e reprocessamento.
