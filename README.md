# Orofoods - portal B2B de pedidos recorrentes

Portal B2B da Orofoods, marca comercial da JPL Serviços e Transportes Ltda., desenvolvido em ASP.NET Core MVC 10, Razor Views, Entity Framework Core, PostgreSQL, Identity, Bootstrap e Font Awesome.

O sistema atende dois contextos distintos:

- **Portal do cliente:** empresas compradoras consultam o catálogo, preços e condições comerciais, montam pedidos e acompanham o histórico.
- **Administração Orofoods (JPL):** equipe interna controla clientes, catálogo, estoque, regras comerciais, pedidos, usuários e integrações.

## Recursos implementados

- Catálogo público responsivo com produtos, busca, categorias, marcas e imagens.
- Cadastro e aprovação comercial de clientes empresariais.
- Autenticação com perfis `Administrador`, `Vendedor` e `Cliente`; somente administradores acessam a área administrativa.
- Preços por cliente, endereços de entrega, condições de pagamento, pedido mínimo, crédito e disponibilidade.
- Carrinho, checkout, confirmação de pedido e numeração no formato `ORO-AAAA-000000`.
- Histórico de pedidos, repetição de pedido, favoritos e produtos frequentes.
- Administração de produtos, categorias, imagens, estoque, ajustes, clientes, usuários, tabelas de preço e relatórios.
- Banco de dados PostgreSQL com migrations separadas para o provedor ativo.
- Exportação WMC em arquivo `.txt` para homologação, com códigos WMC de cliente e produto, auditoria por pedido e proteção contra sobrescrita em reprocessamentos.
- Painel de integrações com falhas ERP e o status da exportação WMC mais recente de cada pedido.
- Logs diários locais em `Orofoods.Web/Logs`, correlação por requisição com `X-Correlation-ID`, página de erro em português e cabeçalhos de segurança.

## Integração WMC

A exportação atual usa o exemplo de arquivo fornecido para **homologação**. Ela não deve ser considerada o contrato definitivo do WMC enquanto o layout oficial não for disponibilizado.

Para habilitar a saída por arquivo em desenvolvimento, configure `WmcFileDrop` em `appsettings.Development.json`:

```json
"WmcFileDrop": {
  "Enabled": true,
  "AutoRetryEnabled": false,
  "OutputDirectory": "C:\\WMC\\NEOGRID\\IN"
}
```

O modo automático permanece desativado durante a homologação. A exportação manual valida os códigos WMC obrigatórios e registra usuário, data, arquivo e resultado no histórico do pedido.

## Executar localmente

Requisitos:

- .NET SDK 10
- PostgreSQL disponível

Configure a conexão em `ConnectionStrings:DefaultConnection` e a chave JWT por User Secrets ou variável de ambiente. Não grave credenciais reais no repositório.

```powershell
dotnet restore
dotnet run --project .\Orofoods.Web\Orofoods.Web.csproj
```

O endpoint `GET /health` informa a disponibilidade do banco e o estado da configuração de saída WMC, sem expor o caminho do diretório.

## Estrutura principal

- `Orofoods.Web/Areas/Admin`: administração da Orofoods/JPL.
- `Orofoods.Web/Controllers` e `Views`: portal público e do cliente.
- `Orofoods.Web/Services`: regras de catálogo, comercial, pedidos, identidade, relatórios e integrações.
- `Orofoods.Web/Integrations/Erp/Wmc`: geração e entrega de arquivos WMC.
- `Orofoods.Web/Data/MigrationsPostgreSql`: migrations do PostgreSQL.
- `Orofoods.Web.Tests`: testes automatizados de serviços, integração, segurança, controllers e views.

## Observabilidade e segurança

- `X-Correlation-ID` identifica cada requisição e aparece nos logs diários.
- Os arquivos de log persistem somente o identificador de correlação dos escopos; dados de clientes não são incluídos automaticamente.
- Respostas autenticadas usam `Cache-Control: no-store`.
- Cabeçalhos `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy` e `Permissions-Policy` são aplicados globalmente.

## Validação

```powershell
dotnet build .\Orofoods.Web\Orofoods.Web.csproj --no-restore
dotnet test .\Orofoods.Web.Tests\Orofoods.Web.Tests.csproj --no-restore
```
