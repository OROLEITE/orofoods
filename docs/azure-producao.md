# Configuração da aplicação no Azure Produção

## PostgreSQL

O aplicativo lê a conexão por `ConnectionStrings:DefaultConnection`. No App Service de Produção, configure a chave de ambiente `ConnectionStrings__DefaultConnection` com uma referência a um segredo do Azure Key Vault. Guarde o valor completo da connection string somente no Key Vault e conceda à identidade gerenciada do App Service apenas a permissão de leitura desse segredo.

Use o host, banco e usuário dedicados ao PostgreSQL de Produção e os parâmetros TLS exigidos pelo servidor. Não coloque a connection string em `appsettings*.json`, no repositório, em scripts de publicação ou em logs. A aplicação falha na inicialização se a configuração estiver ausente.

Esta configuração é exclusiva do App Service de Produção; não altera nem substitui as configurações de Staging.

## Inicialização

Com `ASPNETCORE_ENVIRONMENT=Production`, o startup não aplica migrations, não executa o seed de papéis e não registra os workers automáticos WMC de homologação JSON, processamento automático de pedidos JSON ou sincronização Firebird. Planeje migrations como uma etapa separada e autorizada antes da publicação.

A exportação manual de pedidos continua disponível no painel administrativo e gera o arquivo WMC sob demanda.
