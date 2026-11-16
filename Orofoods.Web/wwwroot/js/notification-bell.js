(() => {
    'use strict';

    document.querySelectorAll('[data-notification-bell]').forEach(menu => {
        if (menu.dataset.notificationBellReady) return;
        menu.dataset.notificationBellReady = 'true';
        const items = menu.querySelector('[data-notification-items]');
        const badge = menu.querySelector('[data-notification-count]');
        const empty = menu.querySelector('[data-notification-empty]');
        const status = menu.querySelector('[data-notification-status]');
        const token = menu.querySelector('[name="__RequestVerificationToken"]')?.value;
        const fallback = menu.dataset.fallbackUrl;
        let revision = 0;
        let reading = false;
        let polling = false;
        let pollTimer;

        const safeLocalUrl = value => typeof value === 'string' &&
            /^\/(?![\/\\])/.test(value) && !/[\\\u0000-\u0020\u007f]/.test(value)
            ? value : fallback;
        const feedback = message => {
            status.textContent = message;
            status.hidden = !message;
        };
        const input = (name, value) => {
            const element = document.createElement('input');
            element.type = 'hidden';
            element.name = name;
            element.value = value;
            return element;
        };
        const render = snapshot => {
            if (!snapshot || !Array.isArray(snapshot.items) || !Number.isInteger(snapshot.unreadCount) || snapshot.unreadCount < 0)
                throw new Error('Invalid notification response');
            const seen = new Set();
            const forms = [];
            for (const notification of snapshot.items) {
                const id = String(notification.id);
                if (!/^\d+$/.test(id) || seen.has(id)) continue;
                seen.add(id);
                const form = document.createElement('form');
                form.method = 'post';
                form.action = menu.dataset.readUrl;
                form.dataset.notificationId = id;
                const button = document.createElement('button');
                button.type = 'submit';
                button.className = 'global-notification-item' + (notification.readAt ? '' : ' is-unread');
                const icon = document.createElement('i');
                icon.className = 'fa-solid fa-circle-info';
                icon.setAttribute('aria-hidden', 'true');
                const copy = document.createElement('span');
                const title = document.createElement('strong');
                title.textContent = notification.title;
                const message = document.createElement('small');
                message.textContent = notification.message;
                const time = document.createElement('time');
                const date = new Date(notification.createdAt);
                time.dateTime = notification.createdAt;
                time.textContent = Number.isNaN(date.getTime()) ? '' : date.toLocaleString('pt-BR', {
                    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
                });
                copy.append(title, message, time);
                button.append(icon, copy);
                form.append(input('__RequestVerificationToken', token ?? ''), input('id', id), input('navigate', 'true'), button);
                forms.push(form);
            }
            items.replaceChildren(...forms);
            empty.hidden = forms.length > 0;
            badge.textContent = snapshot.unreadCount > 99 ? '99+' : String(snapshot.unreadCount);
            badge.hidden = snapshot.unreadCount === 0;
            feedback('');
        };

        const request = async (url, options = {}) => {
            const controller = new AbortController();
            const timeout = window.setTimeout(() => controller.abort(), 12000);
            try {
                const response = await fetch(url, {
                    credentials: 'same-origin', cache: 'no-store', signal: controller.signal,
                    ...options, headers: { Accept: 'application/json', ...options.headers }
                });
                if (!response.ok) throw new Error('Notification request failed');
                return await response.json();
            } finally {
                window.clearTimeout(timeout);
            }
        };
        const schedule = () => {
            window.clearTimeout(pollTimer);
            pollTimer = window.setTimeout(poll, 15000);
        };
        const poll = async () => {
            if (polling || reading) { schedule(); return; }
            polling = true;
            const startedAt = revision;
            try {
                const snapshot = await request(menu.dataset.updatesUrl);
                // A read that started after this request invalidates its entire snapshot.
                if (!reading && revision === startedAt) render(snapshot);
            } catch {
                // Keep the last good list and counter; the next timer retries.
            } finally {
                polling = false;
                schedule();
            }
        };

        menu.addEventListener('submit', async event => {
            const form = event.target.closest('[data-notification-id]') ?? event.target.closest('[data-notification-read-all]');
            if (!form || !menu.contains(form)) return;
            event.preventDefault();
            if (reading) return;
            if (!token) { feedback('Recarregue a página para atualizar as notificações.'); return; }
            reading = true;
            revision++;
            menu.setAttribute('aria-busy', 'true');
            const id = form.dataset.notificationId;
            const body = new URLSearchParams();
            body.set('__RequestVerificationToken', token);
            if (id) body.set('id', id);
            try {
                const snapshot = await request(id ? menu.dataset.readUrl : menu.dataset.readAllUrl, {
                    method: 'POST', body,
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8' }
                });
                render(snapshot);
                if (id) window.location.assign(safeLocalUrl(snapshot.actionUrl));
            } catch {
                feedback('Não foi possível marcar como lida. Tente novamente.');
            } finally {
                reading = false;
                menu.setAttribute('aria-busy', 'false');
                schedule();
            }
        });
        menu.addEventListener('toggle', () => { if (menu.open) void poll(); });
        void poll();
    });
})();
