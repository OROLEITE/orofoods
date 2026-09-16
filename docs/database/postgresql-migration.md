# Migracao para PostgreSQL

## Conexao

Defina a conexao antes de executar a aplicacao ou comandos EF Core:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=SERVIDOR;Port=5432;Database=orofoods;Username=orofoods;Password=SENHA_SEGURA'
```

Nao armazene a senha em `appsettings.json`.

## Corte seguro

1. Interrompa novos pedidos no banco SQLite atual.
2. Copie o arquivo SQLite para um local de backup imutavel.
3. Crie o banco PostgreSQL vazio e o usuario com permissao apenas nesse banco.
4. Gere e aplique uma baseline PostgreSQL a partir do modelo atual com `dotnet ef migrations add PostgreSqlBaseline` e `dotnet ef database update` usando a conexao acima.
5. Importe dados respeitando dependencias: roles, usuarios, clientes, catalogo, precos, enderecos, condicoes, pedidos e historico.
6. Compare contagens de registros e valide login de administrador, login de cliente, catalogo, carrinho e um checkout.
7. Altere a variavel de conexao do ambiente de producao somente apos a validacao.
8. Mantenha o backup SQLite para reversao ate a aceitacao operacional.

## Observacao

As migrations existentes foram criadas para SQLite e contem anotacoes especificas desse provedor. Nao devem ser aplicadas diretamente em PostgreSQL. A baseline PostgreSQL deve ser gerada contra uma instancia PostgreSQL provisionada.
