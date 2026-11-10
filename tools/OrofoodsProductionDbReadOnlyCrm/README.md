# OrofoodsProductionDbReadOnly

Diagnóstico temporário, independente e somente leitura do PostgreSQL de Produção. O executável exige `ASPNETCORE_ENVIRONMENT=Production`, aceita somente o host `psql-orofoods-prd-01.postgres.database.azure.com` e lê `ConnectionStrings__DefaultConnection` do ambiente já autorizado no App Service.

O utilitário não é exposto por rota HTTP nem registrado no processo web. A publicação da aplicação o coloca em `tools/` na raiz de conteúdo do App Service (`/home/site/wwwroot/tools`), fora da pasta estática ASP.NET `wwwroot/`. As consultas usam uma transação `REPEATABLE READ` com `SET TRANSACTION READ ONLY`; a transação é sempre revertida e nenhum telefone, credencial ou dado de cliente é impresso.

## Execução autorizada dentro do App Service

```powershell
$resourceGroup = '<resource-group>'
az webapp exec -g $resourceGroup -n app-orofoods-prd-01 --mode shell --shell /bin/sh
```

No shell do App Service:

```sh
dotnet /home/site/wwwroot/tools/OrofoodsProductionDbReadOnly.dll
```

O comando deve ser executado apenas após a sessão autorizada pela VNet já fornecer as variáveis de ambiente. Não copie a connection string para argumentos, arquivos ou logs.

`az webapp exec --mode execute --shell-command "dotnet /home/site/wwwroot/tools/OrofoodsProductionDbReadOnly.dll"` também tem sintaxe válida, mas é fire-and-forget e não é adequado para visualizar o resultado/exit code; use o modo `shell` acima para a leitura controlada.

## Saída esperada

O resultado contém somente o ambiente, host e banco confirmados, a confirmação `READ ONLY` com rollback, contagens agregadas de grupos/registros duplicados e os estados `APPLIED`, `PENDING` ou `APPLIED_NOT_IN_PROJECT` das migrations.

## Limpeza

O pacote não altera o banco nem cria endpoint. Após registrar a saída redigida, remova o arquivo temporário no shell autorizado:

```sh
rm -f /home/site/wwwroot/tools/OrofoodsProductionDbReadOnly.dll
```

Se a publicação for substituída pelo próximo pacote, a limpeza manual não é necessária. Não remova outros arquivos de `tools/`.
