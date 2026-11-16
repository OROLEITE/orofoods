# Correção do updateConversation do CRM WhatsApp

## Diagnóstico

Cards server-side em `Index.cshtml` omitem o elemento `<time>` quando `LastMessageAt` é nulo. Cards criados por `conversationElement` sempre incluem `<time>`. `updateConversation` presume a estrutura dinâmica e escreve em `time.textContent`, causando `TypeError` no polling `update-conversations`; a exceção também interrompe o loop das demais conversas.

## Abordagem escolhida

O HTML server-side sempre renderizará `<time></time>`, alinhando-o ao card dinâmico. O JavaScript manterá guards para `time`, badge e demais elementos opcionais, registrará erro técnico quando uma estrutura obrigatória estiver ausente e `updateConversations` continuará processando as próximas conversas após uma falha isolada.

## Objetivos e não objetivos

- Atualizar cards existentes e novos, previews, horários, badges e mensagens sem interromper outras conversas.
- Preservar badge opcional e cards sem horário como estados válidos.
- Não alterar contratos HTTP, banco, migrations, deploy ou código de Produção/Staging.
- Não engolir falhas: registrar `conversationId` e o elemento ausente/erro sem incluir conteúdo sensível.

## Revisão de recuperação e contrato visual

Quando um card existente não puder ser atualizado por incompatibilidade estrutural, o reconciliador criará um card novo com `conversationElement`, atualizará seus dados e o substituirá com `replaceWith`, preservando a posição e evitando duplicatas. O ID somente entrará em `visibleIds` depois de uma reconciliação bem-sucedida; se a reconstrução falhar, o card antigo será removido e o erro ficará isolado para nova tentativa no próximo ciclo.

O endpoint de atualização fornecerá `lastInboundAt`, e Razor/JavaScript renderizarão o mesmo ícone (`fa-arrow-down` ou `fa-comment`) e um span de texto separado em `.whatsapp-conversation-meta`. Isso permite atualizar o texto sem remover o ícone.
