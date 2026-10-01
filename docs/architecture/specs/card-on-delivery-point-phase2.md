Prossiga com a implementação da Fase 2 usando as decisões confirmadas pela inspeção.

DECISÕES APROVADAS

1. Não existe atualmente domínio reutilizável de motorista ou entrega.
2. Criar cadastro operacional independente de motorista.
3. Manter histórico de associação entre terminal Mercado Pago Point e motorista.
4. A trava de conclusão do pedido deve ficar no backend, no `AdminOrderService`.
5. A trava deve ser específica para pedidos de Cartão na Entrega sem pagamento aprovado.
6. Não implementar comunicação real com Mercado Pago Point nesta fase.

MOTORISTA

Criar entidade operacional equivalente a:

Driver

Campos mínimos conceituais:

* Id
* Name
* Active
* CreatedAt
* UpdatedAt

Adicionar somente campos realmente necessários.

Não armazenar CPF, CNH, telefone ou outros dados pessoais sem necessidade operacional clara nesta fase.

Se o projeto já tiver convenções de auditoria/base entity, reutilizá-las.

TERMINAL

Criar entidade equivalente a:

PaymentTerminal

ou nome consistente com o domínio.

Campos conceituais:

* Id
* Provider
* DeviceId
* StoreId
* PosId
* Active
* CreatedAt
* UpdatedAt

Provider deve suportar:

MercadoPago

Não armazenar:

* access token;
* secret;
* dados de cartão;
* credenciais.

HISTÓRICO TERMINAL ↔ MOTORISTA

Criar relacionamento histórico, não apenas vínculo atual.

Modelo conceitual:

DriverPaymentTerminalAssignment

Campos:

* Id
* DriverId
* PaymentTerminalId
* StartedAt
* EndedAt nullable
* Active/derivado
* CreatedAt

Regra:

uma associação atual é aquela com:

EndedAt == null

ou equivalente adotado pelo projeto.

INVARIANTES

* Driver deve existir e estar ativo para receber nova associação.
* Terminal deve existir e estar ativo.
* Um terminal não pode ter duas associações ativas simultaneamente.
* Um motorista não deve ter associações ativas conflitantes se a regra operacional adotada for um terminal por motorista.
* Não apagar histórico usado em pagamentos.
* Encerrar associação antiga preenchendo EndedAt, não DELETE físico.

Se houver necessidade futura de motorista com múltiplos terminais, manter o modelo extensível, mas não criar complexidade desnecessária agora.

ADMIN

Criar gerenciamento administrativo para:

Drivers
Payment Terminals
Driver ↔ Terminal Assignments

Proteger por role:

Administrador

Permitir:

Driver:

* criar
* editar nome
* ativar/desativar

Terminal:

* criar
* editar metadados permitidos
* ativar/desativar

Assignment:

* associar terminal a motorista
* encerrar associação
* visualizar histórico

Não integrar com Mercado Pago nesta tela.

Não validar DeviceId externamente nesta fase.

TRAVA DE DELIVERED

Alterar o fluxo no:

AdminOrderService

ou ponto backend real responsável pela mudança de status.

REGRA:

Se o pedido estiver configurado como:

Cartão na Entrega

e o pagamento correspondente NÃO estiver aprovado:

a mudança de status para:

Delivered

deve ser rejeitada.

A rejeição deve ocorrer no backend.

Não confiar em UI.

Uma requisição direta também deve ser bloqueada.

NÃO aplicar essa regra a:

CASH / À vista

ou outras modalidades existentes.

DEFINIÇÃO DE "PAGAMENTO APROVADO"

Reutilizar o conceito real já existente em:

Payment
PaymentStatus
PaymentAttempt

Não criar nova semântica paralela se já houver status Approved equivalente.

Inspecionar qual entidade é fonte de verdade.

A regra deve consultar essa fonte.

Se ainda não existir Payment persistido para Cartão na Entrega, o estado deve ser tratado como:

Pending / Not Approved

e portanto bloquear Delivered.

MENSAGEM

Usar mensagem clara e não técnica, equivalente a:

“O pedido utiliza Cartão na Entrega e o pagamento ainda não foi aprovado.”

Seguir padrão de localização/mensagens do projeto.

UI

No Admin, quando aplicável, exibir:

Forma de pagamento:
Cartão na entrega

Pagamento:
Pendente / Aprovado

Se Pending:

a UI pode desabilitar a ação de Entregue.

Mas isso é apenas UX.

O backend permanece autoridade final.

POINT PROVIDER

Criar somente abstração necessária para próxima fase, por exemplo:

IPointPaymentProvider

Não implementar HTTP real.

Criar:

DisabledPointPaymentProvider

ou equivalente.

Qualquer tentativa de uso deve retornar erro controlado informando que a integração Point não está habilitada.

External calls nesta fase:

ZERO

MIGRATION

Criar migration mínima para:

* Driver
* PaymentTerminal
* DriverPaymentTerminalAssignment

e somente colunas adicionais realmente necessárias.

Adicionar:

* FKs;
* índices;
* unique constraints necessários para evitar associações ativas inválidas quando tecnicamente possível.

Não executar migration em Staging.
Não executar em Production.

FEATURE FLAG

Manter Cartão na Entrega:

OFF

Não habilitar em Staging.

Não habilitar em Production.

TESTES

Adicionar testes cobrindo no mínimo:

DRIVER

1. criar Driver válido;
2. desativar Driver;
3. Driver inativo não recebe nova associação.

TERMINAL

4. criar terminal válido;
5. terminal inativo não pode ser associado;
6. nenhum secret é persistido.

ASSIGNMENTS

7. criar associação motorista-terminal;
8. impedir associação ativa duplicada do mesmo terminal;
9. encerrar associação preserva histórico;
10. nova associação depois do encerramento é permitida;
11. histórico permanece consultável.

DELIVERY GUARD

12. CardOnDelivery + pagamento Pending -> Delivered BLOQUEADO.
13. CardOnDelivery + sem Payment -> Delivered BLOQUEADO.
14. CardOnDelivery + pagamento Approved -> Delivered PERMITIDO.
15. CASH -> comportamento anterior preservado.
16. outra modalidade existente -> comportamento anterior preservado.
17. chamada direta ao service também respeita a regra.

ADMIN AUTHORIZATION

18. CRUD de Driver exige Administrador.
19. CRUD de Terminal exige Administrador.
20. associação exige Administrador.

POINT

21. DisabledPointPaymentProvider não faz chamada externa.
22. feature flag OFF mantém operação desabilitada.

REGRESSÃO

Executar:

restore
build Release
testes focados
suíte completa
git diff --check

Não aceitar regressões.

NÃO IMPLEMENTAR

Nesta fase não:

* chamar Mercado Pago;
* usar Access Token;
* criar Store;
* criar POS;
* registrar terminal real;
* criar order Point;
* simular pagamento Point;
* criar webhook novo;
* associar maquininha física;
* alterar WMC;
* fazer deploy;
* alterar Azure;
* executar migration em ambiente.

RELATÓRIO FINAL

Entregar:

* Driver model: CREATED/OTHER
* Terminal model: CREATED/OTHER
* Assignment history: IMPLEMENTED/NO
* Migration created: YES/NO
* Migration executed: NO
* Admin CRUD Driver: YES/NO
* Admin CRUD Terminal: YES/NO
* Assignment management: YES/NO
* Delivered backend guard: YES/NO
* CardOnDelivery Pending blocked: YES/NO
* CardOnDelivery Approved allowed: YES/NO
* CASH regression: PASS/FAIL
* Point HTTP integration: NO
* External calls: 0
* Feature flag: OFF
* focused tests:
* full suite:
* build Release:
* git diff --check:
* files changed:
* risks:
* Phase 3 prerequisites:

Não fazer commit, push ou deploy sem autorização separada.
