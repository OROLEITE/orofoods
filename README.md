# Orofoods — portal B2B de pedidos recorrentes

Primeira versão funcional em ASP.NET Core MVC 10, Razor Views, Entity Framework Core, SQLite, Identity e Bootstrap 5. O produto foi desenhado em torno do fluxo **“pedido em 30 segundos”**, não como um e-commerce genérico.

## O que está implementado

- Home institucional responsiva com identidade premium e imagem original.
- Dashboard B2B com último pedido, condição comercial, pedido mínimo, crédito e dia regional de entrega.
- Repetição do último pedido com quantidades pré-preenchidas e edição sem recarregar a página.
- Preço personalizado por cliente, múltiplos endereços, mínimos por caixa, mínimo do pedido e limite de crédito.
- Produto indisponível com indicação de substituto.
- Checkout compacto, persistência real do pedido e número `ORO-AAAA-000000`.
- Catálogo pesquisável com preços da tabela do cliente.
- Identity com roles Administrador, Vendedor e Cliente, lockout e schema incluído na migration.

## Arquitetura desta primeira versão

O projeto está organizado por responsabilidade dentro de `Orofoods.Web`: `Models`, `ViewModels`, `Data`, `Controllers`, `Views` e `wwwroot`. É uma escolha intencional para o MVP; quando regras administrativas e integrações crescerem, os modelos e serviços podem ser extraídos para projetos Domain/Application/Infrastructure.

Entidades centrais: `Customer`, `CustomerAddress`, `Product`, `CustomerPrice`, `Order` e `OrderItem`. O preço efetivo segue `CustomerPrice -> Product.BasePrice`. A confirmação recalcula valores e valida regras no servidor — a interface JavaScript serve apenas como feedback imediato.

## Executar

Requisitos: .NET SDK 10.

```powershell
dotnet restore
dotnet run --project .\Orofoods.Web\Orofoods.Web.csproj
```

Abra a URL exibida no terminal e use **Área do cliente**. A base `orofoods.db` e os dados demonstrativos são criados automaticamente pela migration na primeira execução.

## Dados demonstrativos

O cliente de demonstração é a **Burger da Vila**, com dois endereços, tabela “Hamburgueria Parceira”, limite de crédito, condições de pagamento e um pedido entregue pronto para repetição.

## Próximos módulos recomendados

1. Vincular `Customer` ao usuário Identity autenticado e proteger o portal com `[Authorize]`.
2. Completar cadastro/aprovação de clientes e telas administrativas CRUD.
3. Adicionar histórico/status, favoritos, pedidos-modelo e recuperação de senha por provedor real.
4. Migrar SQLite para SQL Server em produção e adicionar testes automatizados.
5. Implementar notificações e integrações de ERP/WhatsApp via interfaces de aplicação.

## Ativo visual

`wwwroot/images/orofoods-hero.png` foi gerado para este projeto com a ferramenta integrada de geração de imagens. Prompt final: fotografia publicitária food-service, hambúrguer smash com pão brioche e pães frescos à direita, cozinha profissional escura, luz âmbar, espaço negativo à esquerda, sem texto, logo, pessoas ou embalagem.
