// Run with OROFOODS_PLAYWRIGHT_MODULE pointing to an existing Playwright installation, or install playwright in your test environment.
const { chromium } = require(process.env.OROFOODS_PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const script = path.join(root, 'Orofoods.Web/wwwroot/js/whatsapp-composer.js');
const message = (id, textBody, extra = {}) => ({ id, conversationId: 42, direction: 'inbound', type: 'text', mediaState: 'none', status: 'sent', statusLabel: 'Enviada', textBody, createdAt: '2026-11-14T12:00:00Z', ...extra });
const fixture = selected => `<!doctype html><html><body>
<section class="whatsapp-inbox-page" data-conversation-id="${selected ? '42' : ''}" data-updates-url="/Admin/WhatsApp/Updates" data-service-window-open="true" data-conversation-status="open">
<p id="whatsappSyncFeedback" hidden role="status"></p>
<nav class="whatsapp-conversation-list"><a data-conversation-id="42" class="whatsapp-conversation"><span class="whatsapp-avatar"></span><span class="whatsapp-conversation-title"><strong>Contato</strong></span><span class="whatsapp-conversation-preview"><span></span></span><span class="whatsapp-conversation-meta"></span></a></nav>
${selected ? `<header class="whatsapp-thread-header"><div class="whatsapp-thread-contact"><strong>Contato</strong></div></header>
<div class="whatsapp-messages" style="height:120px;overflow:auto"></div><button class="whatsapp-new-message" hidden></button>
<form class="whatsapp-compose" action="/Admin/WhatsApp/Send" method="post" data-conversation-id="42" data-updates-url="/Admin/WhatsApp/Updates"><input name="clientRequestId" value="12345678123412341234123456789abc"><textarea id="whatsappText" name="text"></textarea><button type="submit"><span>Enviar</span></button><p id="whatsappSendFeedback" hidden></p></form>
<form class="whatsapp-compose whatsapp-template-compose" hidden><input name="clientRequestId" id="whatsappTemplateRequestId" value="different-template-key"></form>` : ''}</section></body></html>`;

async function verifyLostResponseRetry(browser) {
    const page = await browser.newPage();
    let persisted = false;
    let dropFirstResponse = true;
    const requestIds = [];
    const canonicalCreatedAt = '2026-11-14T12:01:00Z';
    let canonical;
    await page.route('https://orofoods.test/**', async route => {
        const url = new URL(route.request().url());
        if (url.pathname.endsWith('/Updates'))
            return route.fulfill({ json: { conversations: [{ id: 42, name: 'Maria WhatsApp', phoneNumber: '5511999999999', status: 'open', unreadCount: 0, lastMessageAt: canonicalCreatedAt, preview: 'Original', messageType: 'text' }], messages: persisted ? [canonical] : [], serviceWindowOpen: true } });
        if (url.pathname.endsWith('/Send')) {
            const match = route.request().postData()?.match(/name="clientRequestId"\r?\n\r?\n([^\r\n]+)/);
            requestIds.push(match?.[1] ?? 'missing');
            canonical = message(77, 'Original', { direction: 'outbound', clientRequestId: requestIds.at(-1), createdAt: canonicalCreatedAt });
            persisted = true;
            if (dropFirstResponse) {
                dropFirstResponse = false;
                return route.abort();
            }
            return route.fulfill({ json: { message: canonical, error: null } });
        }
        return route.fulfill({ contentType: 'text/html', body: fixture(true) });
    });
    await page.goto('https://orofoods.test/Admin/WhatsApp?id=42');
    await page.addScriptTag({ path: script });
    await page.waitForFunction(() => document.querySelector('.whatsapp-conversation-title strong').textContent === 'Maria WhatsApp');
    await page.locator('#whatsappText').fill('  Original  ');
    await page.locator('.whatsapp-compose button[type=submit]').click();
    await page.waitForFunction(() => document.querySelector('[data-message-id="77"]'));
    await page.evaluate(() => window.dispatchEvent(new Event('online')));
    await page.waitForFunction(() => document.querySelector('[data-message-id="77"] .whatsapp-message-status--sent'));
    await page.locator('.whatsapp-compose button[type=submit]').click();
    await page.waitForFunction(() => document.querySelectorAll('[data-message-id="77"]').length === 1);
    assert.equal(requestIds.length, 2, 'the lost response is retried');
    assert.equal(requestIds[1], requestIds[0], 'retry keeps the original idempotency key after canonical whitespace');
    assert.equal(await page.locator('[data-message-id="77"]').count(), 1, 'retry and polling keep one message');
    assert.equal(await page.locator('.whatsapp-message--outbound').count(), 1, 'the retry does not create a second outbound bubble');
    assert.equal(await page.locator('[data-message-id="77"]').getAttribute('data-submitted-text'), '  Original  ', 'the raw draft is preserved for retry comparison');
    await page.close();
    console.log('PASS: lost send response, whitespace normalization and idempotent retry');
}

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        for (const selected of [false, true]) {
            const page = await browser.newPage();
            let messages = [message(1, 'Recebida automaticamente')];
            let state = 'open', windowOpen = true, fail = false, calls = 0, releaseSend;
            const conversation = () => ({ id: 42, name: 'Maria WhatsApp', profileName: 'Maria WhatsApp', phoneNumber: '5511999999999', status: state, unreadCount: 1, lastMessageAt: '2026-11-14T12:00:00Z', preview: 'Nova mensagem', messageType: 'text' });
            await page.route('https://orofoods.test/**', async route => {
                const url = new URL(route.request().url());
                if (url.pathname.endsWith('/Updates')) {
                    calls++;
                    if (fail) return route.fulfill({ status: 503, body: 'temporarily unavailable' });
                    return route.fulfill({ json: { conversations: [conversation()], messages: selected ? messages : [], serviceWindowOpen: selected ? windowOpen : null } });
                }
                if (url.pathname.endsWith('/Send')) {
                    await new Promise(resolve => { releaseSend = resolve; });
                    const clientRequestId = '12345678123412341234123456789abc';
                    const sent = message(2, 'Enviada agora', { direction: 'outbound', clientRequestId });
                    messages.push(message(9, 'Recebida antes da resposta', { createdAt: '2026-11-14T11:59:00Z' }), sent);
                    return route.fulfill({ json: { message: sent, error: null } });
                }
                return route.fulfill({ contentType: 'text/html', body: fixture(selected) });
            });
            await page.goto('https://orofoods.test/Admin/WhatsApp');
            await page.addScriptTag({ path: script });
            await page.waitForFunction(() => document.querySelector('.whatsapp-conversation-title strong').textContent === 'Maria WhatsApp', { timeout: 8000 });
            assert.ok(calls > 0, 'empty inbox also polls');
            if (selected) {
                await page.waitForFunction(() => document.querySelector('[data-message-id="1"]'));
                messages.push(...Array.from({ length: 30 }, (_, index) => message(100 + index, `Histórico ${index}`)));
                await page.evaluate(() => window.dispatchEvent(new Event('online')));
                await page.waitForFunction(() => document.querySelector('[data-message-id="129"]'));
                await page.locator('#whatsappText').fill('Enviada agora');
                await page.locator('.whatsapp-messages').evaluate(element => { element.scrollTop = 100; });
                const readingAnchor = await page.locator('.whatsapp-messages').evaluate(element => {
                    const node = [...element.children].find(node => node.getBoundingClientRect().bottom > element.getBoundingClientRect().top);
                    return { id: node.dataset.messageId, top: node.getBoundingClientRect().top };
                });
                await page.locator('.whatsapp-compose button[type=submit]').click();
                await page.waitForFunction(() => document.querySelectorAll('.whatsapp-message--outbound').length === 1);
                assert.ok(releaseSend, 'send started before server response');
                await page.locator('#whatsappText').fill('Próximo rascunho');
                releaseSend();
                await page.waitForFunction(() => document.querySelector('[data-message-id="2"]'));
                await page.evaluate(() => window.dispatchEvent(new Event('online')));
                await page.waitForTimeout(250);
                assert.equal(await page.locator('.whatsapp-message--outbound').count(), 1, 'acknowledgement and polling deduplicate');
                const order = await page.locator('.whatsapp-messages [data-message-id]').evaluateAll(nodes => nodes.map(node => node.dataset.messageId));
                assert.ok(order.indexOf('9') < order.indexOf('2'), 'server chronology wins over optimistic insertion order');
                const anchorTop = await page.locator(`[data-message-id="${readingAnchor.id}"]`).evaluate(element => element.getBoundingClientRect().top);
                assert.ok(Math.abs(anchorTop - readingAnchor.top) < 1, 'send and synchronization preserve the visible reading anchor');
                assert.equal(await page.locator('#whatsappText').inputValue(), 'Próximo rascunho');
                state = 'closed'; windowOpen = false;
                await page.evaluate(() => window.dispatchEvent(new Event('online')));
                await page.waitForTimeout(250);
                assert.equal(await page.locator('#whatsappText').inputValue(), 'Próximo rascunho', 'state change preserves draft without page reload');
                assert.equal(await page.locator('.whatsapp-compose button[type=submit]').isDisabled(), true);
                state = 'open'; windowOpen = true;
                fail = true;
                await page.evaluate(() => window.dispatchEvent(new Event('online')));
                await page.waitForTimeout(250);
                assert.equal(await page.locator('[data-message-id="1"]').count(), 1, 'network failure retains history');
                fail = false;
                messages.push(message(3, 'Recebida após reconexão'));
                await page.evaluate(() => window.dispatchEvent(new Event('online')));
                await page.waitForFunction(() => document.querySelector('[data-message-id="3"]'));
                assert.equal(await page.locator('#whatsappText').inputValue(), 'Próximo rascunho');
            }
            await page.close();
            console.log(`PASS: ${selected ? 'inbound, send, dedup, draft, state change, reconnect' : 'polling without selection and missing time'}`);
        }
        await verifyLostResponseRetry(browser);
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
