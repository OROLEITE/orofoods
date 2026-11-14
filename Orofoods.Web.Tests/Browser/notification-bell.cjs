const { chromium } = require(process.env.OROFOODS_PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const html = `<!doctype html><html><body>
<details data-notification-bell class="global-notification-menu" data-updates-url="/Admin/Notifications" data-read-url="/Admin/Notifications/Read" data-read-all-url="/Admin/Notifications/ReadAll" data-fallback-url="/Admin/Notifications">
<summary>Notificações <span data-notification-count></span></summary><div class="global-notification-panel">
<form data-notification-read-all><input name="__RequestVerificationToken" value="synthetic-test-token"><button>Marcar todas como lidas</button></form>
<p data-notification-empty hidden></p><div data-notification-items></div><p data-notification-status hidden></p>
<footer><button class="whatsapp-sound-toggle" type="button"><i></i><span>Som ligado</span></button></footer></div></details></body></html>`;
(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
        let reads = 0, allReads = 0;
        const notifications = [11, 12].map(id => ({ id: String(id), title: 'WhatsApp', message: 'Mensagem sintética', createdAt: '2026-11-14T12:00:00Z', readAt: null, actionUrl: `/Admin/WhatsApp?id=${id === 11 ? 42 : 43}` }));
        const snapshot = () => ({ unreadCount: notifications.filter(item => !item.readAt).length, items: notifications });
        await page.route('https://orofoods.test/**', async route => {
            const req = route.request();
            const url = new URL(req.url());
            if (url.pathname === '/Admin/Notifications' && req.headers().accept?.includes('application/json')) return route.fulfill({ json: snapshot() });
            if (url.pathname.endsWith('/Read') || url.pathname.endsWith('/ReadAll')) {
                const body = new URLSearchParams(req.postData());
                assert.equal(body.get('__RequestVerificationToken'), 'synthetic-test-token');
                assert.equal(req.method(), 'POST');
                if (url.pathname.endsWith('/Read')) {
                    reads++;
                    const item = notifications.find(item => item.id === body.get('id'));
                    item.readAt = '2026-11-14T12:01:00Z';
                    return route.fulfill({ json: { ...snapshot(), actionUrl: item.actionUrl } });
                }
                allReads++;
                notifications.forEach(item => { item.readAt ??= '2026-11-14T12:02:00Z'; });
                return route.fulfill({ json: snapshot() });
            }
            return route.fulfill({ contentType: 'text/html', body: html });
        });
        const load = async () => {
            await page.addScriptTag({ path: path.join(root, 'Orofoods.Web/wwwroot/js/notification-bell.js') });
            await page.addScriptTag({ path: path.join(root, 'Orofoods.Web/wwwroot/js/whatsapp-composer.js') });
        };
        await page.goto('https://orofoods.test/Admin/WhatsApp');
        await load();
        await page.waitForFunction(() => document.querySelector('[data-notification-count]').textContent === '2');
        await page.locator('summary').click();
        await page.waitForTimeout(100);
        assert.equal(reads, 0, 'opening bell does not read notifications');
        assert.equal(await page.locator('[data-notification-id]').count(), 2);
        await page.locator('.whatsapp-sound-toggle').click();
        assert.equal(await page.evaluate(() => localStorage.getItem('orofoods.whatsapp.sound')), 'off');
        await Promise.all([page.waitForURL('**/Admin/WhatsApp?id=42'), page.locator('[data-notification-id="11"] button').click()]);
        assert.equal(reads, 1);
        assert.equal(notifications[1].readAt, null, 'only selected notification is read');
        await load();
        await page.waitForFunction(() => document.querySelector('[data-notification-count]').textContent === '1');
        assert.equal(await page.locator('.whatsapp-sound-toggle').getAttribute('aria-pressed'), 'false', 'sound preference survives navigation');
        await page.locator('summary').click();
        await page.locator('[data-notification-read-all] button').click();
        await page.waitForFunction(() => document.querySelector('[data-notification-count]').hidden);
        assert.equal(allReads, 1);
        await page.reload();
        await load();
        await page.waitForFunction(() => document.querySelector('[data-notification-count]').textContent === '0');
        assert.equal(await page.locator('[data-notification-id]').count(), 2);
        assert.equal(await page.locator('.is-unread').count(), 0, 'old items stay read after refresh');
        console.log('PASS: bell counter/list, no auto-read, own read then conversation navigation, read-all, reload, sound preference');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
