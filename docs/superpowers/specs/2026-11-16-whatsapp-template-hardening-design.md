# WhatsApp Template Hardening Design

## Goal

Endurecer o suporte já existente a templates oficiais da WhatsApp Cloud API no CRM WhatsApp, cobrindo o fluxo de novos números, a janela de 24 horas, parâmetros obrigatórios, autorização e falhas da Meta sem alterar a integração, o banco ou o layout existente.

## Existing behavior

`MetaWhatsAppBusinessGateway` já consulta `message_templates` no Business Account, mantém apenas templates `APPROVED`, interpreta idioma/categoria/componentes e envia pelo endpoint de mensagens do Phone Number ID. `WhatsAppController` já revalida o catálogo antes do envio, bloqueia mensagens livres fora da janela de 24 horas e persiste o resultado no histórico. `WhatsAppConversationService` atualiza `LastInboundAt` para respostas inbound, reabrindo a janela de atendimento.

## Chosen approach

Manter integralmente essas interfaces e acrescentar somente:

- o texto visível da ação de retomada para `Enviar mensagem modelo`;
- testes de comportamento para número novo, janela expirada, resposta inbound, autorização, parâmetros obrigatórios e erros HTTP da Meta;
- teste de contrato da Razor para preservar a ação e o tema azul-ciano/dark mode.

O catálogo continua sendo consultado sob demanda e o envio continua sendo aceito apenas para o par exato nome/idioma que a Meta retornou como aprovado. Nenhuma chamada real será feita pelos testes: os cenários de gateway usam `HttpMessageHandler` local e os cenários de controller usam um gateway rastreável.

## Security and scope

- Não criar templates, não enviar mensagens reais e não acessar Meta, PostgreSQL, Staging ou Produção.
- Não criar migration, seed, alteração de modelo ou mudança de persistência.
- Preservar antiforgery, autorização por conversa, idempotência por `clientRequestId`, renderização segura do histórico e tratamento fail-closed de catálogo/envio.
- Não alterar o JavaScript ou CSS se os testes demonstrarem que o polling já alterna corretamente entre mensagem livre e template; somente ajustar a cópia da ação.

## Acceptance criteria

1. Um número novo, com `LastInboundAt == null`, pode consultar e enviar um template aprovado; o envio é persistido no histórico.
2. Uma conversa fora da janela bloqueia mensagem livre e oferece o fluxo de template; uma conversa com resposta inbound recente libera mensagem livre.
3. Templates não aprovados, parâmetros ausentes/extras/em branco, operadores sem acesso e erros da Meta não enviam mensagem nem vazam detalhes sensíveis.
4. O botão de retomada exibe exatamente `Enviar mensagem modelo` e mantém classes de ação azul-ciano e os contratos dark mode.
5. Build Release, testes focados, `node --check` e `git diff --check` passam.
