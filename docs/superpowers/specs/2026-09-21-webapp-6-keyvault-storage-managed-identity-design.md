# WEBAPP-6 - Key Vault, Blob Storage e Managed Identity

## 1. Contexto

O Staging da Orofoods já possui Resource Group, VNet, subnets, Private DNS, PostgreSQL Flexible Server privado e o database `orofoods`. O App Service ainda aguarda quota regional para o plano B1.

O codigo ja preparado no projeto possui:

- `DefaultAzureCredential` no provider `AzureBlobProductImageStorage`;
- provider Local para Development;
- configuracao tipada de Storage e Data Protection;
- `Storage:Provider=AzureBlob` com URI do servico e nome de container;
- validacao de `BlobUri` e `KeyVaultKeyIdentifier` quando Data Protection Azure estiver habilitado.

Esta especificacao define os recursos e a sequencia para WEBAPP-6. Ela nao cria infraestrutura, nao move credenciais e nao altera o codigo.

## 2. Objetivos

- Armazenar secrets de Staging no Azure Key Vault.
- Armazenar imagens e o key ring do ASP.NET Core em containers Blob privados.
- Usar identidade gerenciada system-assigned do Web App para acesso aos recursos.
- Manter Development local e Production totalmente separados de Staging.
- Eliminar dependencias de secrets no Git, em `appsettings` e em comandos persistentes.

## 3. Nao objetivos

- Criar Key Vault, Storage Account, identidade ou role assignment nesta etapa.
- Fazer deploy do Web App ou configurar dominio proprio.
- Executar migrations, copiar dados ou alterar o PostgreSQL.
- Ativar Meta, Mercado Pago ou WMC.
- Implementar rotacao automatica de credenciais.

## 4. Arquitetura

```text
Web App Orofoods Staging
  System-assigned Managed Identity
       |-- Key Vault: secrets e uma Key criptografica de Data Protection
       |-- Storage Account: product-images (privado)
       `-- Storage Account: data-protection (privado)
```

Staging e Production terao Resource Group, Web App, Key Vault, Storage Account, key ring e secrets distintos. Nenhum desses recursos sera compartilhado entre ambientes.

## 5. Key Vault

Proposta:

- Nome: `kv-orofoods-stg-01`.
- Resource Group: `rg-orofoods-stg-brazilsouth`.
- Regiao: `brazilsouth`.
- Autorizacao: Azure RBAC, sem politicas legadas de acesso.
- Soft delete: habilitado.
- Purge protection: habilitado antes de armazenar credenciais reais.
- Public network access: habilitado inicialmente para o bootstrap do App Service, protegido por RBAC e TLS; deve ser restringido depois de uma estrategia Private Endpoint e DNS privado aprovada.
- Tags: `Application=Orofoods`, `Environment=Staging`, `Region=BrazilSouth`, `ManagedBy=AzureCLI`.

Para V1/Staging, o endpoint publico controlado e a opcao operacional mais simples porque o App Service ainda nao existe e seus enderecos de saida nao estao confirmados. RBAC autentica e autoriza, mas nao substitui o controle de rede. O fechamento do endpoint exige Private Endpoint, DNS privado e verificacao de roteamento antes de ser aplicado.

## 6. Secrets

Somente os valores realmente usados pelo projeto devem ser criados:

| Secret | Origem no projeto | Uso |
|---|---|---|
| `orofoods-stg-default-connection` | `ConnectionStrings:DefaultConnection` | PostgreSQL privado |
| `orofoods-stg-jwt-key` | `Jwt:Key` | assinatura JWT |
| `orofoods-stg-mercadopago-access-token` | `MercadoPago:AccessToken` | API Mercado Pago |
| `orofoods-stg-mercadopago-webhook-secret` | `MercadoPago:WebhookSecret` | validacao de webhook |
| `orofoods-stg-meta-access-token` | `WhatsAppBusiness:AccessToken` | API Meta |
| `orofoods-stg-meta-app-secret` | `WhatsAppBusiness:AppSecret` | assinatura Meta |
| `orofoods-stg-meta-verify-token` | `WhatsAppBusiness:VerifyToken` | verificacao do webhook |
| `orofoods-stg-wmc-firebird-password` | `WmcFirebird:Password` | futuro WMC |

O primeiro grupo de secrets pode ser criado somente quando o respectivo fluxo for habilitado. `PhoneNumberId`, `BusinessAccountId`, `GraphApiVersion`, `MercadoPago:BaseAddress` e `WmcFirebird:Host`, `Port`, `Database`, `User` sao configuracoes, nao secrets, embora devam ser fornecidos por configuracao de ambiente e nao por dados de producao no Git.

O arquivo temporario da senha administrativa do PostgreSQL permanece local, modo `600`, ate a credencial ser importada e validada no Key Vault. Depois da validacao, ele deve ser removido por uma operacao manual segura.

## 7. Data Protection Key

Data Protection usa dois tipos de material distintos:

- **Secret**: valores de aplicacao, como senha, token e JWT key.
- **Key**: objeto criptografico do Key Vault usado por `ProtectKeysWithAzureKeyVault` para proteger o key ring.

Proposta:

- Key Vault Key: `dp-orofoods-stg`.
- Key type: RSA conforme suporte vigente do provider .NET/Azure Key Vault.
- Key material: nunca exportado para Git, logs ou relatorio.
- Blob URI: container privado `data-protection`, com key ring persistente.

O fluxo Staging sera `DefaultAzureCredential` -> Key Vault Key para proteger o key ring e `DefaultAzureCredential` -> Blob Storage para persistir o key ring. A configuracao deve usar `PersistKeysToAzureBlobStorage` e `ProtectKeysWithAzureKeyVault` somente quando os URIs e identificadores estiverem completos.

## 8. Storage Account

Proposta:

- Nome: `storofoodsstg01`.
- Resource Group: `rg-orofoods-stg-brazilsouth`.
- Regiao: `brazilsouth`.
- Performance: Standard.
- Redundancia inicial: LRS.
- HTTPS only: habilitado.
- Minimum TLS: 1.2 ou maior conforme a politica vigente.
- Public blob access: desabilitado.
- Shared Key access: manter apenas se uma dependencia comprovada exigir; a aplicacao deve preferir Managed Identity/RBAC.
- Tags: mesmas tags do Key Vault.

O nome precisa ser validado globalmente no momento do provisionamento. Staging e Production usarao contas diferentes.

## 9. Containers

### `product-images`

- Acesso: privado.
- Uso: provider `AzureBlobProductImageStorage`.
- Operacoes da identidade: read, write e delete de Blob.
- Entrega ao navegador: endpoint autenticado da aplicacao como estrategia principal; SAS curto e escopado pode ser adotado depois para downloads especificos.

O provider atual retorna a URI do blob e faz upload/delete, mas nao gera SAS nem implementa endpoint de leitura. Portanto, habilitar container privado exige uma etapa funcional posterior para servir a imagem pela aplicacao ou gerar SAS curto. O container nao deve ser tornado publico para contornar essa lacuna.

### `data-protection`

- Acesso: privado.
- Uso exclusivo: key ring do ASP.NET Core.
- Nome inicial do arquivo: definido pelo provider, sem depender de um nome fixo como `keys.xml`.
- Operacoes da identidade: read e write de Blob.
- Nao armazenar imagens, secrets ou arquivos de deploy neste container.

## 10. Managed Identity

O Web App futuro usara identidade **system-assigned**:

1. Criar o Web App.
2. Habilitar a identidade system-assigned.
3. Obter o `principalId` retornado pelo recurso.
4. Atribuir roles somente nos escopos do Key Vault, da Key e dos containers necessarios.
5. Configurar referencias e URIs.
6. Validar acesso sem imprimir valores.

Nenhuma identidade e criada nesta especificacao. Development continua usando credenciais locais/User Secrets quando necessario; a selecao `Local` nao deve depender de Azure.

## 11. RBAC

Roles minimas propostas para a identidade do Web App:

- Key Vault: `Key Vault Secrets User`, escopo no Key Vault de Staging.
- Key Vault Key: `Key Vault Crypto User`, escopo somente na key `dp-orofoods-stg`.
- Storage `product-images`: `Storage Blob Data Contributor`, escopo no container `product-images`, pois o provider atual faz read/write/delete.
- Storage `data-protection`: `Storage Blob Data Contributor`, escopo no container `data-protection`, pois o key ring precisa ser lido e persistido.

Nao conceder `Owner`, `Contributor` ou `Key Vault Administrator`. O escopo de container deve ser usado quando a operacao de role assignment e a governanca local o suportarem; caso contrario, usar escopo da conta somente com justificativa registrada.

## 12. Networking

### Opcao inicial escolhida: endpoint publico protegido

Para Staging V1, Key Vault e Storage usarao endpoint publico com HTTPS, RBAC, public blob access desabilitado e sem secrets expostos. O App Service acessara esses endpoints por saida HTTPS. A rede privada ja protege o PostgreSQL.

Essa escolha evita criar subnets, Private Endpoints e zonas DNS adicionais durante o bootstrap. O firewall nao deve ser confundido com autenticacao: Managed Identity/RBAC valida quem acessa, enquanto a rede decide de onde o acesso e permitido.

### Opcao futura: Private Endpoint

Private Endpoint e a opcao recomendada para endurecimento de Production e para Staging quando houver necessidade de isolamento de egress. Exige subnets adequadas, Private DNS zones para `vault.azure.net` e `blob.core.windows.net`, links com a VNet, validacao de DNS pelo App Service e roteamento correto. Nao deve ser adicionado automaticamente.

Se um firewall for aplicado ao endpoint publico, ele deve ser validado depois que o Web App existir e seus egressos forem conhecidos. Nao fechar o endpoint apenas com base na identidade: identidade nao atravessa uma restricao de rede bloqueada.

## 13. Configuracao por ambiente

### Development

- PostgreSQL local/User Secrets.
- `Storage:Provider=Local`.
- Data Protection em `DataProtectionKeys` local.
- Nenhuma dependencia de Azure real para iniciar a aplicacao.

### Staging

- PostgreSQL `psql-orofoods-stg-01`, database `orofoods`.
- Key Vault e Storage exclusivos de Staging.
- `DefaultAzureCredential` resolvendo para Managed Identity no App Service.
- `Storage:Provider=AzureBlob`.
- Data Protection Azure habilitado somente apos Key Vault Key e Blob estarem prontos.

### Production

- Recursos, secrets, key ring e credenciais exclusivos de Production.
- Nao reutilizar Key Vault, Storage, key ring ou tokens de Staging.
- Preferir Private Endpoints apos a topologia de rede ser validada.

## 14. Bootstrap

1. Criar Storage Account de Staging.
2. Criar `product-images` e `data-protection` privados.
3. Criar Key Vault com RBAC, soft delete e purge protection.
4. Criar a Key `dp-orofoods-stg`.
5. Importar a credencial PostgreSQL temporaria sem exibi-la.
6. Aguardar quota do App Service.
7. Criar App Service Plan e Web App.
8. Habilitar identidade system-assigned e obter `principalId`.
9. Atribuir as roles minimas.
10. Configurar referencias do Key Vault e URIs do Blob.
11. Validar acesso a secrets, imagens e key ring sem registrar valores.
12. Validar Data Protection em reinicio controlado.
13. Remover manualmente o arquivo temporario da senha.

Migrations, deploy de codigo e habilitacao de integracoes externas sao etapas posteriores e separadas.

## 15. Failure modes

- Key Vault indisponivel: falhar rapidamente se uma configuracao critica for necessaria no startup; nao usar fallback silencioso para credencial antiga.
- Storage indisponivel: provider Azure deve falhar a operacao de imagem e registrar apenas identificadores tecnicos, sem dados sensiveis.
- Managed Identity sem role: startup/health check deve indicar configuracao incompleta sem expor token.
- Secret ausente: falhar rapidamente quando o recurso correspondente estiver habilitado.
- Blob de Data Protection ausente: nao gerar key ring local alternativo em Staging/Production.
- Key de Data Protection ausente: nao iniciar o provider Azure como se estivesse protegido.
- Container privado sem mecanismo de entrega: nao tornar o container publico; corrigir a entrega pela aplicacao ou SAS curto.

## 16. Seguranca

- Nenhum secret real nesta especificacao.
- Nenhum token, senha, header de autorizacao ou material criptografico em logs.
- PostgreSQL continua sem acesso publico.
- Key Vault e Storage sao separados por ambiente.
- Identidade recebe somente roles de dados necessarias.
- HTTPS only, TLS 1.2+ e public blob access desabilitado.
- Nao colocar senha em shell history, Git, `appsettings` ou relatorios.

## 17. Observabilidade

Logs podem registrar:

- provider Azure selecionado;
- Blob provider inicializado;
- Data Protection Azure configurado;
- nome logico do recurso e estado de conectividade.

Logs nao podem registrar valores de secret, tokens, senha, key material, connection string completa ou Authorization header.

## 18. Nomenclatura

| Recurso | Nome proposto |
|---|---|
| Key Vault | `kv-orofoods-stg-01` |
| Storage Account | `storofoodsstg01` |
| Container de imagens | `product-images` |
| Container de Data Protection | `data-protection` |
| Key de Data Protection | `dp-orofoods-stg` |

Os nomes globais devem ser verificados antes da criacao. Production tera sufixos e contas distintos.

## 19. Sequencia de provisionamento

A ordem operacional futura e:

1. Validar subscription, quota e nomes.
2. Criar Storage Account.
3. Criar os dois containers privados.
4. Criar Key Vault com RBAC, soft delete e purge protection.
5. Criar Key de Data Protection.
6. Aguardar e validar quota do App Service.
7. Criar App Service Plan e Web App.
8. Habilitar Managed Identity.
9. Aplicar RBAC nos escopos minimos.
10. Configurar Key Vault references e Azure Blob/Data Protection.
11. Validar acesso ao PostgreSQL privado via VNet Integration.
12. Validar upload, entrega autenticada de imagem e persistencia do key ring.
13. Somente depois executar deploy e migrations controladas.

## 20. Criterios de aceite

- Nenhum secret real aparece no documento, Git ou logs.
- Staging e Production nao compartilham Key Vault, Storage, key ring ou secrets.
- A senha PostgreSQL e importada no Key Vault sem ser exibida e o arquivo local e removido somente apos validacao.
- A identidade system-assigned acessa apenas os escopos necessarios.
- `product-images` e `data-protection` permanecem privados.
- Data Protection usa uma Key criptografica do Key Vault, distinta dos Secrets.
- Development inicia com provider Local sem Azure real.
- O App Service consegue acessar PostgreSQL privado, Key Vault e Storage conforme a topologia escolhida.
- Nenhum recurso Azure foi criado por esta especificacao.
- Nenhuma alteracao funcional de codigo foi feita nesta etapa.