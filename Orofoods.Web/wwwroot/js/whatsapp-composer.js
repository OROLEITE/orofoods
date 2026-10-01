(() => {
    const textarea = document.getElementById('whatsappText');
    const form = document.querySelector('.whatsapp-compose');
    const messageList = document.querySelector('.whatsapp-messages');
    const conversationList = document.querySelector('.whatsapp-conversation-list');
    const newMessageButton = document.querySelector('.whatsapp-new-message');
    const minHeight = 46;
    const maxHeight = 160;

    const resizeComposer = () => {
        if (!textarea) return;
        textarea.style.height = 'auto';
        const contentHeight = textarea.scrollHeight;
        textarea.style.height = `${Math.min(Math.max(contentHeight, minHeight), maxHeight)}px`;
        textarea.style.overflowY = contentHeight > maxHeight ? 'auto' : 'hidden';
    };

    if (textarea) {
        textarea.addEventListener('input', resizeComposer);
        window.addEventListener('resize', resizeComposer);
        resizeComposer();
    }

    if (!form || !messageList || !conversationList) return;

    const selectedConversationId = Number(form.dataset.conversationId);
    const updatesUrl = form.dataset.updatesUrl;
    let requestInFlight = false;
    let timerId = null;

    const isNearBottom = () => messageList.scrollHeight - messageList.scrollTop - messageList.clientHeight < 64;
    const scrollToBottom = () => {
        messageList.scrollTop = messageList.scrollHeight;
        if (newMessageButton) newMessageButton.hidden = true;
    };
    scrollToBottom();

    newMessageButton?.addEventListener('click', scrollToBottom);
    messageList.addEventListener('scroll', () => {
        if (isNearBottom() && newMessageButton) newMessageButton.hidden = true;
    }, { passive: true });

    const iconForType = type => ({
        image: 'fa-image', audio: 'fa-microphone', video: 'fa-video', document: 'fa-file', sticker: 'fa-note-sticky'
    })[type];
    const statusIcon = status => status === 'pending' ? 'fa-clock'
        : status === 'failed' ? 'fa-circle-exclamation'
            : status === 'delivered' || status === 'read' ? 'fa-check-double' : 'fa-check';
    const formatTime = value => value
        ? new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' }).format(new Date(value)) : '';
    const formatMessageTime = value => value
        ? new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(value)) : '';
    const formatSize = bytes => {
        if (!bytes) return '';
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    };
    const safeMediaUrl = value => {
        if (!value) return null;
        const url = new URL(value, window.location.origin);
        return url.origin === window.location.origin ? url.href : null;
    };
    const appendIcon = (parent, classes) => {
        const icon = document.createElement('i');
        icon.className = classes;
        icon.setAttribute('aria-hidden', 'true');
        parent.append(icon);
        return icon;
    };
    const appendText = (parent, tag, value, className) => {
        const element = document.createElement(tag);
        if (className) element.className = className;
        element.textContent = value;
        parent.append(element);
        return element;
    };

    const renderMessage = (article, message) => {
        const key = [message.type, message.mediaState, message.mediaUrl, message.textBody, message.caption,
            message.fileName, message.mediaSizeBytes, message.status].join('|');
        if (article.dataset.renderKey === key) return;
        article.dataset.renderKey = key;
        article.dataset.messageStatus = message.status;
        article.className = `whatsapp-message whatsapp-message--${message.direction} whatsapp-message--${message.type}`;
        article.replaceChildren();
        const source = safeMediaUrl(message.mediaUrl);

        if (message.mediaState === 'available' && source) {
            if (message.type === 'image' || message.type === 'sticker') {
                const link = document.createElement('a');
                link.className = 'whatsapp-media-image-link';
                link.href = source;
                link.target = '_blank';
                link.rel = 'noopener';
                const image = document.createElement('img');
                image.className = `whatsapp-media-image${message.type === 'sticker' ? ' whatsapp-media-sticker' : ''}`;
                image.src = source;
                image.alt = message.type === 'sticker' ? 'Figurinha recebida' : 'Imagem recebida';
                image.loading = 'lazy';
                link.append(image);
                article.append(link);
            } else if (message.type === 'audio') {
                const heading = document.createElement('div');
                heading.className = 'whatsapp-media-heading';
                appendIcon(heading, `fa-solid ${message.isVoiceMessage ? 'fa-microphone' : 'fa-volume-high'}`);
                appendText(heading, 'span', message.isVoiceMessage ? 'Mensagem de voz' : 'Áudio');
                article.append(heading);
                const audio = document.createElement('audio');
                audio.controls = true;
                audio.preload = 'metadata';
                audio.src = source;
                article.append(audio);
            } else if (message.type === 'video') {
                const video = document.createElement('video');
                video.controls = true;
                video.preload = 'metadata';
                video.playsInline = true;
                video.src = source;
                article.append(video);
            } else if (message.type === 'document') {
                const link = document.createElement('a');
                link.className = 'whatsapp-document-card';
                link.href = source;
                link.download = '';
                appendIcon(link, 'fa-regular fa-file-lines');
                const copy = document.createElement('span');
                appendText(copy, 'strong', message.fileName || 'Documento');
                appendText(copy, 'small', formatSize(message.mediaSizeBytes));
                link.append(copy);
                appendIcon(link, 'fa-solid fa-download');
                article.append(link);
            }
        } else if (message.mediaState === 'pending') {
            const state = appendText(article, 'p', ' Processando mídia…', 'whatsapp-media-state');
            state.prepend(Object.assign(document.createElement('i'), { className: 'fa-solid fa-spinner fa-spin' }));
        } else if (message.mediaState === 'failed' || message.mediaState === 'rejected') {
            const state = appendText(article, 'p', ' Mídia indisponível', 'whatsapp-media-state');
            state.prepend(Object.assign(document.createElement('i'), { className: 'fa-solid fa-triangle-exclamation' }));
        } else if (message.textBody) {
            appendText(article, 'p', message.textBody);
        }

        if (message.caption) appendText(article, 'p', message.caption, 'whatsapp-media-caption');
        const footer = document.createElement('footer');
        const time = appendText(footer, 'time', formatMessageTime(message.createdAt));
        time.dateTime = message.createdAt;
        const status = document.createElement('span');
        status.className = `whatsapp-message-status${message.direction === 'outbound' ? ` whatsapp-message-status--${message.status}` : ''}`;
        status.setAttribute('aria-label', message.direction === 'outbound' ? message.statusLabel : 'Mensagem recebida');
        appendIcon(status, `fa-solid ${message.direction === 'outbound' ? statusIcon(message.status) : 'fa-arrow-down'}`);
        if (message.direction === 'outbound') appendText(status, 'span', message.statusLabel);
        footer.append(status);
        article.append(footer);
    };

    const updateMessages = messages => {
        const keepBottom = isNearBottom();
        let appended = false;
        messageList.querySelector('.whatsapp-messages-empty')?.remove();
        for (const message of messages) {
            let article = messageList.querySelector(`[data-message-id="${message.id}"]`);
            if (!article) {
                article = document.createElement('article');
                article.dataset.messageId = message.id;
                messageList.append(article);
                appended = true;
            }
            renderMessage(article, message);
        }
        if (!appended) return;
        if (keepBottom) scrollToBottom();
        else if (newMessageButton) newMessageButton.hidden = false;
    };

    const conversationElement = conversation => {
        const link = document.createElement('a');
        link.className = 'whatsapp-conversation';
        link.dataset.conversationId = conversation.id;
        link.href = `${window.location.pathname}?id=${encodeURIComponent(conversation.id)}`;
        const avatar = appendText(link, 'span', conversation.identified ? conversation.initials : '',
            `whatsapp-avatar${conversation.identified ? '' : ' whatsapp-avatar--unknown'}`);
        avatar.setAttribute('aria-hidden', 'true');
        if (!conversation.identified) appendIcon(avatar, 'fa-regular fa-user');
        const copy = document.createElement('span');
        copy.className = 'whatsapp-conversation-copy';
        copy.innerHTML = '<span class="whatsapp-conversation-title"><strong></strong><time></time></span><span class="whatsapp-conversation-preview"><span></span><span class="whatsapp-unread-badge"></span></span><span class="whatsapp-conversation-meta"></span>';
        link.append(copy);
        return link;
    };

    const updateConversation = (element, conversation) => {
        element.classList.toggle('is-active', conversation.id === selectedConversationId);
        if (conversation.id === selectedConversationId) element.setAttribute('aria-current', 'page');
        element.querySelector('.whatsapp-conversation-title strong').textContent = conversation.name;
        const time = element.querySelector('.whatsapp-conversation-title time');
        time.textContent = formatTime(conversation.lastMessageAt);
        time.dateTime = conversation.lastMessageAt || '';
        const preview = element.querySelector('.whatsapp-conversation-preview > span:first-child');
        preview.replaceChildren();
        const icon = iconForType(conversation.messageType);
        if (icon) appendIcon(preview, `fa-solid ${icon}`);
        preview.append(document.createTextNode(`${icon ? ' ' : ''}${conversation.preview}`));
        const badge = element.querySelector('.whatsapp-unread-badge');
        badge.hidden = conversation.unreadCount < 1;
        badge.textContent = conversation.unreadCount > 99 ? '99+' : conversation.unreadCount;
        badge.setAttribute('aria-label', `${conversation.unreadCount} mensagens não lidas`);
        element.querySelector('.whatsapp-conversation-meta').textContent = conversation.status === 'open'
            ? 'Em atendimento' : conversation.status === 'pending' ? 'Pendente' : 'Encerrada';
    };

    const updateConversations = conversations => {
        conversationList.querySelector('.whatsapp-list-empty')?.remove();
        for (const conversation of conversations) {
            let element = conversationList.querySelector(`[data-conversation-id="${conversation.id}"]`);
            if (!element) element = conversationElement(conversation);
            updateConversation(element, conversation);
            conversationList.append(element);
        }
    };

    const synchronize = async () => {
        if (requestInFlight || document.hidden || !updatesUrl) return;
        requestInFlight = true;
        try {
            const url = new URL(updatesUrl, window.location.origin);
            url.searchParams.set('conversationId', selectedConversationId);
            const response = await fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
            if (!response.ok) return;
            const payload = await response.json();
            updateConversations(payload.conversations || []);
            updateMessages(payload.messages || []);
        } catch (error) {
            if (error?.name !== 'AbortError') console.debug('WhatsApp sync unavailable.');
        } finally {
            requestInFlight = false;
        }
    };

    const schedule = () => {
        window.clearInterval(timerId);
        timerId = document.hidden ? null : window.setInterval(synchronize, 4000);
    };
    document.addEventListener('visibilitychange', () => {
        schedule();
        if (!document.hidden) void synchronize();
    });
    schedule();
})();
