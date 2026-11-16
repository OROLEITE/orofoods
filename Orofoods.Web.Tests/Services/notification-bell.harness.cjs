async function runNotificationBellTests(script) {
    const assert = (condition, message) => { if (!condition) throw new Error(message); };
    class Element {
        constructor(tag = 'div') {
            this.tagName = tag.toUpperCase(); this.dataset = {}; this.children = [];
            this.listeners = {}; this.attributes = {}; this.hidden = false; this.textContent = '';
            this.className = ''; this.disabled = false; this.open = false;
            this.classList = { contains: name => this.className.split(' ').includes(name) };
        }
        appendChild(child) { child.remove(); this.children.push(child); child.parentElement = this; return child; }
        append(...children) { children.forEach(child => this.appendChild(child)); }
        remove() { if (this.parentElement) this.parentElement.children = this.parentElement.children.filter(x => x !== this); this.parentElement = null; }
        replaceChildren(...children) { this.children.forEach(x => x.parentElement = null); this.children = []; this.append(...children); }
        setAttribute(key, value) { this.attributes[key] = String(value); }
        getAttribute(key) { return this.attributes[key] ?? null; }
        addEventListener(name, handler) { this.listeners[name] = handler; }
        matches(selector) {
            if (selector === '[data-notification-id]') return this.dataset.notificationId !== undefined;
            if (selector === '[data-notification-read-all]') return this.dataset.notificationReadAll !== undefined;
            return false;
        }
        closest(selector) { for (let el = this; el; el = el.parentElement) if (el.matches(selector)) return el; return null; }
        contains(element) { for (let el = element; el; el = el.parentElement) if (el === this) return true; return false; }
        querySelector(selector) {
            if (this.selectors?.[selector]) return this.selectors[selector];
            for (const child of this.children) {
                if (child.matches(selector)) return child;
                const result = child.querySelector(selector); if (result) return result;
            }
            return null;
        }
        querySelectorAll(selector) { return this.children.filter(x => x.matches(selector)); }
    }
    const flush = async () => { for (let n = 0; n < 20; n++) await Promise.resolve(); };
    const notification = (id, actionUrl = '/Admin/WhatsApp?id=42', readAt = null) => ({
        id: String(id), title: '<img src=x onerror=alert(1)>', message: 'Mensagem',
        createdAt: '2026-11-14T12:00:00Z', readAt, actionUrl
    });
    function setup() {
        const menu = new Element('details'), items = new Element(), badge = new Element('span');
        const empty = new Element('p'), status = new Element('p'), all = new Element('form'), slot = new Element();
        all.dataset.notificationReadAll = ''; all.action = '/Admin/Notifications/ReadAll';
        const token = new Element('input'); token.value = 'csrf-token';
        menu.dataset = { notificationBell: '', updatesUrl: '/Admin/Notifications', readUrl: '/Admin/Notifications/Read', readAllUrl: all.action, fallbackUrl: '/Admin/Notifications' };
        menu.selectors = {
            '[data-notification-items]': items, '[data-notification-count]': badge,
            '[data-notification-empty]': empty, '[data-notification-status]': status,
            '[data-notification-read-all]': all, '[name="__RequestVerificationToken"]': token,
            '[data-notification-sound]': slot
        };
        menu.append(items, badge, empty, status, all, slot);
        const sound = new Element('button');
        slot.appendChild(sound);
        let soundClicks = 0;
        sound.addEventListener('click', () => { soundClicks++; });
        const timers = [], requests = [], navigations = [];
        const document = {
            readyState: 'complete', hidden: false,
            querySelectorAll: selector => selector === '[data-notification-bell]' ? [menu] : [],
            querySelector: selector => selector === '.whatsapp-header-actions .whatsapp-sound-toggle' ? sound : null,
            createElement: tag => new Element(tag), addEventListener() {}
        };
        const window = {
            location: { assign: url => navigations.push(url) },
            setTimeout: (callback, delay) => { const timer = { callback, delay }; timers.push(timer); return timer; },
            clearTimeout: timer => { const i = timers.indexOf(timer); if (i >= 0) timers.splice(i, 1); }
        };
        const fetch = (url, options) => new Promise((resolve, reject) => requests.push({
            url, options, resolve: data => resolve({ ok: true, json: async () => data }),
            fail: () => reject(new Error('temporary network failure')),
            reject: () => resolve({ ok: false, status: 400 })
        }));
        class Params {
            constructor() { this.values = {}; }
            set(key, value) { this.values[key] = String(value); }
            append(key, value) { this.set(key, value); }
            toString() { return Object.keys(this.values).map(key => encodeURIComponent(key) + '=' + encodeURIComponent(this.values[key])).join('&'); }
        }
        class Abort { constructor() { this.signal = {}; } abort() {} }
        new Function('document', 'window', 'fetch', 'URLSearchParams', 'AbortController', script)(document, window, fetch, Params, Abort);
        const poll = async () => {
            const timerIndex = timers.findIndex(x => x.delay !== 12000);
            assert(timerIndex >= 0, 'poll must schedule a retry');
            const timer = timers.splice(timerIndex, 1)[0]; timer.callback(); await flush();
        };
        const submit = form => {
            let prevented = false;
            const event = { target: form, preventDefault() { prevented = true; } };
            const result = menu.listeners.submit?.(event);
            assert(prevented, 'notification submission must be intercepted to persist before navigating');
            return result;
        };
        return { menu, items, badge, empty, status, all, slot, sound, timers, requests, navigations, poll, submit, soundClicks: () => soundClicks };
    }

    const page = setup();
    assert(page.requests.length === 1, 'bell must automatically request an initial JSON update');
    assert(page.requests[0].options.cache === 'no-store', 'poll must bypass cached private responses');
    assert(page.requests[0].options.credentials === 'same-origin', 'poll must remain authenticated');
    page.requests[0].resolve({ unreadCount: 2, items: [notification(1), notification(1), notification(2, '//evil.test/')] });
    await flush();
    assert(page.items.children.length === 2, 'duplicate notification IDs must render once');
    assert(page.badge.textContent === '2', 'poll must update counter');
    const first = page.items.children[0];
    assert(first.children.some(x => x.tagName === 'BUTTON'), 'items must remain POST forms');
    assert(page.slot.children[0] === page.sound, 'sound control must remain in notification menu');
    page.sound.listeners.click();
    assert(page.soundClicks() === 1, 'bell must leave the existing sound handler alone');
    page.menu.open = true;
    page.menu.listeners.toggle?.();
    await flush();
    assert(page.requests.every(x => (x.options.method ?? 'GET') === 'GET'), 'opening menu must never mark notifications read');
    // Complete any open-triggered refresh, then begin a poll that will return after the POST.
    for (let i = 1; i < page.requests.length; i++) page.requests[i].resolve({ unreadCount: 2, items: [notification(1), notification(2)] });
    await flush();
    await page.poll();
    const stale = page.requests[page.requests.length - 1];
    page.submit(page.items.children[0]);
    await flush();
    const post = page.requests[page.requests.length - 1];
    assert(post.options.method === 'POST', 'click must POST before navigation');
    assert(post.options.headers.Accept === 'application/json', 'POST must request a state response');
    assert(post.options.body.toString().includes('__RequestVerificationToken=csrf-token'), 'POST must include antiforgery token');
    assert(post.options.body.toString().includes('id=1'), 'POST must address only the clicked ID');
    assert(page.navigations.length === 0, 'navigation must wait for persisted read');
    const beforeDuplicate = page.requests.length;
    page.submit(page.items.children[0]);
    assert(page.requests.length === beforeDuplicate, 'pending clicks must not duplicate POSTs');
    post.resolve({ unreadCount: 1, items: [notification(1, '/Admin/WhatsApp?id=42', '2026-11-14T12:01:00Z'), notification(2)], actionUrl: '/Admin/WhatsApp?id=42' });
    await flush();
    stale.resolve({ unreadCount: 2, items: [notification(1), notification(2)] });
    await flush();
    assert(page.badge.textContent === '1', 'stale poll must not restore read counter');
    assert(!page.items.children[0].children.find(x => x.tagName === 'BUTTON').className.includes('is-unread'), 'stale poll must not restore read styling');
    assert(page.navigations[0] === '/Admin/WhatsApp?id=42', 'successful read must navigate to the returned local action');

    page.submit(page.all);
    await flush();
    const allPost = page.requests[page.requests.length - 1];
    assert(allPost.url === '/Admin/Notifications/ReadAll', 'mark-all must POST to its own endpoint');
    allPost.resolve({ unreadCount: 0, items: [notification(1, '/', 'now'), notification(2, '/', 'now')] });
    await flush();
    assert(page.badge.hidden && page.badge.textContent === '0', 'persisted mark-all must clear badge');

    await page.poll();
    const failedPoll = page.requests[page.requests.length - 1];
    failedPoll.fail(); await flush();
    assert(page.items.children.length === 2, 'transient failures must retain the list');
    await page.poll();
    page.requests[page.requests.length - 1].resolve({ unreadCount: 1, items: [notification(3)] });
    await flush();
    assert(page.items.children.length === 1 && page.badge.textContent === '1', 'poll must recover and replace removed entries');
    const previousNavigationCount = page.navigations.length;
    page.submit(page.items.children[0]); await flush();
    page.requests[page.requests.length - 1].reject(); await flush();
    assert(page.navigations.length === previousNavigationCount, 'failed read must not navigate');
    assert(page.badge.textContent === '1', 'failed read must retain unread count');
    assert(page.status.textContent.length > 0, 'failed read must expose retry feedback');

    for (const url of ['https://evil.test/', '//evil.test/', '/\\evil.test/', 'javascript:alert(1)', '/path\nLocation: evil']) {
        const unsafe = setup();
        unsafe.requests[0].resolve({ unreadCount: 1, items: [notification(1, url)] }); await flush();
        unsafe.submit(unsafe.items.children[0]); await flush();
        unsafe.requests[1].resolve({ unreadCount: 0, items: [], actionUrl: url }); await flush();
        assert(unsafe.navigations[0] === '/Admin/Notifications', 'unsafe server/client URLs must fall back locally: ' + url);
    }
    return 'Notification bell behavior passed';
}

if (typeof require !== 'undefined' && require.main === module) {
    const fs = require('node:fs');
    const path = require('node:path');
    const scriptPath = path.resolve(__dirname, '../../Orofoods.Web/wwwroot/js/notification-bell.js');
    runNotificationBellTests(fs.existsSync(scriptPath) ? fs.readFileSync(scriptPath, 'utf8') : '')
        .then(result => console.log(result))
        .catch(error => { console.error(error); process.exitCode = 1; });
}
