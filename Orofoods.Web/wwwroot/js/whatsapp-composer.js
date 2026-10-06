(() => {
    const textarea = document.getElementById('whatsappText');
    const form = document.querySelector('.whatsapp-compose');
    const messageList = document.querySelector('.whatsapp-messages');
    const conversationList = document.querySelector('.whatsapp-conversation-list');
    const conversationSearch = document.querySelector('.whatsapp-search-input');
    const newMessageButton = document.querySelector('.whatsapp-new-message');
    const soundToggle = document.querySelector('.whatsapp-sound-toggle');
    const minHeight = 46;
    const maxHeight = 160;
    const soundPreferenceKey = 'orofoods.whatsapp.sound';
    let soundEnabled = localStorage.getItem(soundPreferenceKey) !== 'off';
    let audioContext;
    let initialState = true;
    const notificationSeenInboundIds = new Set();

    const updateSoundToggle = () => {
        if (!soundToggle) return;
        soundToggle.setAttribute('aria-pressed', String(soundEnabled));
        soundToggle.title = soundEnabled ? 'Som ligado' : 'Som desligado';
        soundToggle.setAttribute('aria-label', soundToggle.title);
        const label = soundToggle.querySelector('span');
        if (label) label.textContent = soundEnabled ? 'Som ligado' : 'Som desligado';
        const icon = soundToggle.querySelector('i');
        if (icon) icon.className = `fa-solid ${soundEnabled ? 'fa-bell' : 'fa-bell-slash'}`;
    };
    const unlockAudio = () => {
        if (!soundEnabled || !window.AudioContext) return;
        try {
            audioContext ??= new AudioContext();
            if (audioContext.state === 'suspended') void audioContext.resume();
        } catch { /* browser policy; polling remains active */ }
    };
    const playInboundBeep = () => {
        if (!soundEnabled || !window.AudioContext) return;
        try {
            unlockAudio();
            if (!audioContext || audioContext.state !== 'running') return;
            const oscillator = audioContext.createOscillator();
            const gain = audioContext.createGain();
            oscillator.frequency.value = 660;
            gain.gain.setValueAtTime(0.0001, audioContext.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.045, audioContext.currentTime + 0.01);
            gain.gain.exponentialRampToValueAtTime(0.0001, audioContext.currentTime + 0.12);
            oscillator.connect(gain).connect(audioContext.destination);
            oscillator.start();
            oscillator.stop(audioContext.currentTime + 0.13);
        } catch { /* autoplay failures are intentionally silent */ }
    };
    updateSoundToggle();
    soundToggle?.addEventListener('click', () => { soundEnabled = !soundEnabled; localStorage.setItem(soundPreferenceKey, soundEnabled ? 'on' : 'off'); updateSoundToggle(); if (soundEnabled) unlockAudio(); });
    ['pointerdown', 'keydown', 'click'].forEach(eventName =>
        document.addEventListener(eventName, unlockAudio, { once: true, passive: true }));

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

    const customerDialog = document.getElementById('whatsappCustomerDialog');
    const customerSearch = document.getElementById('whatsappCustomerSearch');
    const customerResults = document.getElementById('whatsappCustomerResults');
    const customerIdInput = document.getElementById('whatsappCustomerId');
    const customerConversationId = document.getElementById('whatsappCustomerConversationId');
    const customerSubmit = document.getElementById('whatsappCustomerSubmit');
    const customerTrigger = document.querySelector('.whatsapp-link-customer-trigger');
    let customerSearchController;
    let customerSearchTimer;

    const closeCustomerDialog = () => {
        customerSearchController?.abort();
        if (customerDialog?.open) customerDialog.close();
    };
    const renderCustomerResults = results => {
        if (!customerResults) return;
        customerResults.replaceChildren();
        if (!results.length) {
            appendText(customerResults, 'p', 'Nenhum cliente encontrado.', 'whatsapp-customer-results-empty');
            return;
        }
        for (const customer of results) {
            const option = document.createElement('button');
            option.type = 'button';
            option.className = 'whatsapp-customer-result';
            option.dataset.customerId = customer.id;
            option.setAttribute('role', 'option');
            appendText(option, 'strong', customer.name || `Cliente ${customer.id}`);
            const details = [customer.code, customer.cnpj, customer.phone].filter(Boolean).join(' · ');
            if (details) appendText(option, 'small', details);
            option.addEventListener('click', () => {
                customerIdInput.value = customer.id;
                customerSubmit.disabled = false;
                customerResults.querySelectorAll('.whatsapp-customer-result').forEach(item => item.classList.remove('is-selected'));
                option.classList.add('is-selected');
            });
            customerResults.append(option);
        }
    };
    const searchCustomers = async () => {
        const term = customerSearch?.value.trim() || '';
        customerSearchController?.abort();
        if (term.length < 2) { renderCustomerResults([]); return; }
        customerSearchController = new AbortController();
        try {
            const url = new URL(customerTrigger.dataset.customerSearchUrl, window.location.origin);
            url.searchParams.set('q', term);
            const response = await fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin', cache: 'no-store', signal: customerSearchController.signal });
            if (!response.ok) throw new Error('customer search failed');
            renderCustomerResults((await response.json()).results || []);
        } catch (error) {
            if (error?.name !== 'AbortError') renderCustomerResults([]);
        }
    };
    customerTrigger?.addEventListener('click', () => {
        customerConversationId.value = customerTrigger.dataset.conversationId;
        customerIdInput.value = '';
        customerSubmit.disabled = true;
        customerSearch.value = '';
        renderCustomerResults([]);
        if (typeof customerDialog?.showModal === 'function') customerDialog.showModal();
        else customerDialog?.setAttribute('open', '');
        customerSearch?.focus();
    });
    customerSearch?.addEventListener('input', () => {
        window.clearTimeout(customerSearchTimer);
        customerSearchTimer = window.setTimeout(searchCustomers, 250);
    });
    document.querySelector('.whatsapp-dialog-cancel')?.addEventListener('click', closeCustomerDialog);

    if (!form || !messageList || !conversationList) return;

    const selectedConversationId = Number(form.dataset.conversationId);
    const updatesUrl = form.dataset.updatesUrl;
    const renderedMessageElements = new Map(
        [...messageList.querySelectorAll('[data-message-id]')]
            .map(element => [String(element.dataset.messageId), element]));
    let requestInFlight = false;
    let sendInFlight = false;
    let timerId = null;
    const sendButton = form.querySelector('button[type="submit"]');
    const sendFeedback = document.getElementById('whatsappSendFeedback');

    const showSendFeedback = (message, isError = false) => {
        if (!sendFeedback) return;
        sendFeedback.textContent = message;
        sendFeedback.hidden = !message;
        sendFeedback.classList.toggle('whatsapp-send-feedback--error', isError);
    };

    const handleComposerKeydown = event => {
        if (event.key !== 'Enter' || event.shiftKey || event.isComposing || event.keyCode === 229) return;

        event.preventDefault();
        if (!textarea?.value.trim() || sendInFlight) return;

        form.requestSubmit(sendButton ?? undefined);
    };
    textarea?.addEventListener('keydown', handleComposerKeydown);

    const isNearBottom = () => messageList.scrollHeight - messageList.scrollTop - messageList.clientHeight < 64;
    const scrollToBottom = () => {
        messageList.scrollTop = messageList.scrollHeight;
        if (newMessageButton) newMessageButton.hidden = true;
    };
    const scrollToBottomAfterUpdate = () => new Promise(resolve => {
        window.requestAnimationFrame(() => {
            scrollToBottom();
            resolve();
        });
    });
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
    const saoPauloTimeZone = 'America/Sao_Paulo';
    const saoPauloDate = new Intl.DateTimeFormat('en-CA', { timeZone: saoPauloTimeZone, year: 'numeric', month: '2-digit', day: '2-digit' });
    const sameSaoPauloDay = value => value && saoPauloDate.format(new Date(value)) === saoPauloDate.format(new Date());
    const formatTime = value => {
        if (!value) return '';
        const options = sameSaoPauloDay(value)
            ? { hour: '2-digit', minute: '2-digit' }
            : { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' };
        return new Intl.DateTimeFormat('pt-BR', { timeZone: saoPauloTimeZone, ...options }).format(new Date(value));
    };
    const formatMessageTime = value => value
        ? new Intl.DateTimeFormat('pt-BR', { timeZone: saoPauloTimeZone, day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(value)) : '';
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
        article.dataset.renderKey = key;
    };

    const updateMessages = messages => {
        const keepBottom = isNearBottom();
        let appended = false;
        messageList.querySelector('.whatsapp-messages-empty')?.remove();
        for (const message of messages) {
            if (message.id == null) continue;
            const messageId = String(message.id);
            if (message.direction === 'inbound') notificationSeenInboundIds.add(messageId);

            let article = renderedMessageElements.get(messageId);
            const isNewMessage = !article;
            if (isNewMessage) {
                article = document.createElement('article');
                article.dataset.messageId = messageId;
            }

            try {
                renderMessage(article, message);
            } catch (error) {
                console.error('WhatsApp message reconciliation failed.', {
                    messageId,
                    name: error?.name ?? null,
                    message: error?.message ?? null
                });
                continue;
            }

            if (isNewMessage) {
                messageList.append(article);
                renderedMessageElements.set(messageId, article);
                appended = true;
            }
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
        element.classList.remove('whatsapp-conversation--open', 'whatsapp-conversation--pending', 'whatsapp-conversation--closed');
        element.classList.add(`whatsapp-conversation--${conversation.status}`);
        if (conversation.id === selectedConversationId) element.setAttribute('aria-current', 'page');
        element.querySelector('.whatsapp-conversation-title strong').textContent = conversation.name;
        if (conversation.phoneNumber) element.dataset.searchPhone = conversation.phoneNumber;
        const time = element.querySelector('.whatsapp-conversation-title time');
        time.textContent = formatTime(conversation.lastMessageAt);
        time.dateTime = conversation.lastMessageAt || '';
        const preview = element.querySelector('.whatsapp-conversation-preview > span:first-child');
        preview.replaceChildren();
        const icon = iconForType(conversation.messageType);
        if (icon) appendIcon(preview, `fa-solid ${icon}`);
        preview.append(document.createTextNode(`${icon ? ' ' : ''}${conversation.preview}`));
        let badge = element.querySelector('.whatsapp-unread-badge');
        if (!badge && conversation.unreadCount > 0) {
            badge = document.createElement('span');
            badge.className = 'whatsapp-unread-badge';
            element.querySelector('.whatsapp-conversation-preview').append(badge);
        }
        if (badge) {
            badge.hidden = conversation.unreadCount < 1;
            badge.textContent = conversation.unreadCount > 99 ? '99+' : conversation.unreadCount;
            badge.setAttribute('aria-label', `${conversation.unreadCount} mensagens não lidas`);
        }
        element.querySelector('.whatsapp-conversation-meta').textContent = conversation.status === 'open'
            ? 'Em atendimento' : conversation.status === 'pending' ? 'Novo' : 'Finalizado';
    };

    const applyConversationFilter = () => {
        if (!conversationList || !conversationSearch) return;
        const query = conversationSearch.value.trim().toLocaleLowerCase('pt-BR');
        const conversations = [...conversationList.querySelectorAll('.whatsapp-conversation')];
        let visibleCount = 0;
        for (const conversation of conversations) {
            const searchable = `${conversation.textContent} ${conversation.dataset.searchPhone || ''}`.toLocaleLowerCase('pt-BR');
            conversation.hidden = query.length > 0 && !searchable.includes(query);
            if (!conversation.hidden) visibleCount++;
        }
        const emptyResult = conversationList.querySelector('.whatsapp-search-empty');
        if (emptyResult) emptyResult.hidden = query.length === 0 || visibleCount > 0;
    };
    conversationSearch?.addEventListener('input', applyConversationFilter);

    const updateConversations = conversations => {
        conversationList.querySelector('.whatsapp-list-empty')?.remove();
        const visibleIds = new Set();
        for (const conversation of conversations) {
            visibleIds.add(String(conversation.id));
            if (conversation.lastMessageDirection === 'inbound' && conversation.lastMessageId != null)
                notificationSeenInboundIds.add(String(conversation.lastMessageId));
            let element = conversationList.querySelector(`[data-conversation-id="${conversation.id}"]`);
            if (!element) element = conversationElement(conversation);
            updateConversation(element, conversation);
            conversationList.append(element);
        }
        conversationList.querySelectorAll('[data-conversation-id]').forEach(element => {
            if (!visibleIds.has(String(element.dataset.conversationId))) element.remove();
        });
        applyConversationFilter();
    };

    const synchronize = async () => {
        if (requestInFlight || document.hidden || !updatesUrl) return;
        let syncStage = 'start';
        requestInFlight = true;
        const controller = new AbortController();
        const timeoutId = window.setTimeout(() => controller.abort(), 8000);
        try {
            syncStage = 'fetch';
            const url = new URL(updatesUrl, window.location.origin);
            url.searchParams.set('conversationId', selectedConversationId);
            const response = await fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin', cache: 'no-store', signal: controller.signal });
            if (!response.ok) return;
            syncStage = 'parse';
            const payload = await response.json();
            const before = notificationSeenInboundIds.size;
            syncStage = 'update-conversations';
            updateConversations(payload.conversations || []);
            syncStage = 'update-messages';
            updateMessages(payload.messages || []);
            syncStage = 'notification';
            const newInbound = notificationSeenInboundIds.size > before && !initialState;
            initialState = false;
            if (newInbound) {
                syncStage = 'beep';
                playInboundBeep();
            }
        } catch (error) {
            if (error?.name !== 'AbortError') {
                console.error('WhatsApp sync failed.', {
                    stage: syncStage,
                    name: error?.name ?? null,
                    message: error?.message ?? null
                });
            }
        } finally {
            window.clearTimeout(timeoutId);
            requestInFlight = false;
        }
    };

    form.addEventListener('submit', async event => {
        if (sendInFlight) {
            event.preventDefault();
            return;
        }

        event.preventDefault();
        const submittedText = textarea?.value ?? '';
        if (!submittedText.trim()) return;

        sendInFlight = true;
        form.querySelector('.whatsapp-send-feedback--error')?.remove();
        showSendFeedback('');

        const keepAtBottom = isNearBottom();
        const previousScrollTop = messageList.scrollTop;
        const buttonLabel = sendButton?.querySelector('span');
        const previousButtonLabel = buttonLabel?.textContent;
        if (sendButton) {
            sendButton.disabled = true;
            sendButton.setAttribute('aria-busy', 'true');
        }
        if (buttonLabel) buttonLabel.textContent = 'Enviando…';
        form.setAttribute('aria-busy', 'true');

        try {
            const response = await fetch(form.action, {
                method: form.method || 'POST',
                body: new FormData(form),
                credentials: 'same-origin',
                headers: { Accept: 'text/html' },
                redirect: 'follow'
            });
            const responseUrl = new URL(response.url || form.action, window.location.href);
            const responseDocument = new DOMParser().parseFromString(await response.text(), 'text/html');
            const serverError = responseDocument.querySelector('.whatsapp-send-feedback--error')?.textContent.trim();

            if (serverError) {
                showSendFeedback(serverError, true);
                return;
            }
            if (!response.ok || responseUrl.origin !== window.location.origin ||
                !/\/Admin\/WhatsApp(?:\/|$)/i.test(responseUrl.pathname) ||
                !responseDocument.querySelector('.whatsapp-compose')) {
                throw new Error('Não foi possível confirmar o envio. Confira sua conexão e tente novamente.');
            }

            await synchronize();
            if (textarea && textarea.value === submittedText) {
                textarea.value = '';
                resizeComposer();
            }
            textarea?.focus();
            await scrollToBottomAfterUpdate();
        } catch {
            if (!keepAtBottom) messageList.scrollTop = previousScrollTop;
            showSendFeedback('Não foi possível confirmar o envio. Confira sua conexão e tente novamente.', true);
        } finally {
            sendInFlight = false;
            form.removeAttribute('aria-busy');
            if (sendButton) {
                sendButton.disabled = false;
                sendButton.removeAttribute('aria-busy');
            }
            if (buttonLabel && previousButtonLabel !== undefined) buttonLabel.textContent = previousButtonLabel;
        }
    });

    const schedule = () => {
        window.clearInterval(timerId);
        timerId = document.hidden ? null : window.setInterval(synchronize, 4000);
    };
    document.addEventListener('visibilitychange', () => {
        schedule();
        if (!document.hidden) void synchronize();
    });
    schedule();
    void synchronize();
})();
