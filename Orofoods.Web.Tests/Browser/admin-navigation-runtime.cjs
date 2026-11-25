const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const repositoryRoot = path.resolve(__dirname, '../..');
const scriptPath = path.join(repositoryRoot, 'Orofoods.Web', 'wwwroot', 'js', 'admin-navigation.js');
const script = fs.readFileSync(scriptPath, 'utf8');

function createElement(tagName, attributes = {}) {
    const listeners = new Map();
    const attrs = new Map(Object.entries(attributes));
    const element = {
        tagName: tagName.toUpperCase(),
        type: attributes.type || (tagName === 'select' ? 'select-one' : 'text'),
        name: attributes.name || '',
        id: attributes.id || '',
        value: attributes.value || '',
        checked: attributes.checked === true,
        disabled: attributes.disabled === true,
        multiple: attributes.multiple === true,
        selectedOptions: attributes.selectedOptions || [],
        addEventListener(type, handler) {
            listeners.set(type, handler);
        },
        dispatch(type, event = {}) {
            const handler = listeners.get(type);
            if (!handler) return;
            handler({ target: element, ...event });
        },
        hasListener(type) {
            return listeners.has(type);
        },
        getAttribute(name) {
            return attrs.has(name) ? attrs.get(name) : null;
        },
        setAttribute(name, value) {
            attrs.set(name, String(value));
        },
        removeAttribute(name) {
            attrs.delete(name);
        }
    };
    return element;
}

function createForm(elements) {
    const listeners = new Map();
    return {
        elements,
        addEventListener(type, handler) {
            listeners.set(type, handler);
        },
        dispatch(type, event = {}) {
            listeners.get(type)?.(event);
        }
    };
}

function createRuntime() {
    const firstLink = createElement('a', { href: '/Admin/Products' });
    const secondLink = createElement('a', {
        href: '/Admin/Orders?status=open',
        target: '_self',
        rel: 'noreferrer'
    });
    const links = [firstLink, secondLink];
    const desktopMode = createElement('select', { value: 'new' });
    const mobileMode = createElement('select', { value: 'new' });
    const namedField = createElement('input', { name: 'description', value: 'initial' });
    const unnamedField = createElement('input', { id: 'quantity-line-1', value: '1' });
    const hiddenField = createElement('input', { type: 'hidden', value: 'secret-token' });
    const passwordField = createElement('input', { type: 'password', name: 'password', value: 'not-tracked' });
    const firstForm = createForm([namedField]);
    const secondForm = createForm([unnamedField]);
    const sensitiveForm = createForm([hiddenField, passwordField]);
    let beforeUnload;
    const storage = new Map();
    const localStorage = {
        getItem(key) { return storage.has(key) ? storage.get(key) : null; },
        setItem(key, value) { storage.set(key, String(value)); }
    };
    const shell = { querySelector() { return null; } };
    const document = {
        querySelector(selector) {
            assert.equal(selector, '[data-admin-shell]');
            return shell;
        },
        querySelectorAll(selector) {
            if (selector === '[data-admin-navigation-menu] a.admin-nav-link[href]') return links;
            if (selector === '[data-admin-navigation-mode]') return [desktopMode, mobileMode];
            if (selector === 'form[method="post" i]') return [firstForm, secondForm, sensitiveForm];
            return [];
        },
        addEventListener() { }
    };
    const window = {
        bootstrap: undefined,
        addEventListener(type, handler) {
            if (type === 'beforeunload') beforeUnload = handler;
        }
    };

    vm.runInNewContext(script, {
        Array,
        Map,
        Set,
        WeakMap,
        document,
        localStorage,
        setTimeout,
        window
    }, { filename: scriptPath });

    const unload = () => {
        let prevented = false;
        const event = {
            preventDefault() { prevented = true; },
            returnValue: undefined
        };
        beforeUnload(event);
        return { prevented, returnValue: event.returnValue };
    };

    return {
        links,
        desktopMode,
        mobileMode,
        namedField,
        unnamedField,
        hiddenField,
        passwordField,
        firstForm,
        secondForm,
        localStorage,
        unload
    };
}

async function main() {
    const runtime = createRuntime();
    assert.equal(runtime.desktopMode.value, 'new');
    assert.equal(runtime.mobileMode.value, 'new');
    assert.equal(runtime.links[0].getAttribute('target'), '_blank');
    assert.match(runtime.links[0].getAttribute('rel'), /noopener/);
    assert.equal(runtime.links[1].getAttribute('target'), '_blank');

    runtime.desktopMode.value = 'same';
    runtime.desktopMode.dispatch('change');
    assert.equal(runtime.desktopMode.value, 'same');
    assert.equal(runtime.mobileMode.value, 'same');
    assert.equal(runtime.localStorage.getItem('orofoods.admin.navigation.mode'), 'same');
    assert.equal(runtime.links[0].getAttribute('target'), null);
    assert.equal(runtime.links[0].getAttribute('rel'), null);
    assert.equal(runtime.links[1].getAttribute('target'), '_self');
    assert.equal(runtime.links[1].getAttribute('rel'), 'noreferrer');
    assert.equal(runtime.links[0].hasListener('click'), false);

    assert.equal(runtime.unload().prevented, false);
    runtime.unnamedField.value = '2';
    assert.equal(runtime.unload().prevented, true);
    runtime.unnamedField.value = '1';

    runtime.namedField.value = 'changed';
    runtime.unnamedField.value = '2';
    runtime.firstForm.dispatch('submit', { defaultPrevented: false });
    assert.equal(runtime.unload().prevented, true, 'a dirty second form must still warn');
    runtime.namedField.value = 'initial';
    runtime.unnamedField.value = '1';

    runtime.namedField.value = 'changed-again';
    const canceledSubmit = { defaultPrevented: false };
    runtime.firstForm.dispatch('submit', canceledSubmit);
    canceledSubmit.defaultPrevented = true;
    await new Promise((resolve) => setTimeout(resolve, 10));
    assert.equal(runtime.unload().prevented, true, 'a canceled submit must not suppress the warning');
    runtime.namedField.value = 'initial';

    runtime.hiddenField.value = 'changed-hidden';
    runtime.passwordField.value = 'changed-password';
    assert.equal(runtime.unload().prevented, false, 'hidden and password fields are not persisted or tracked');
}

main().then(() => {
    console.log('admin-navigation runtime checks passed');
}).catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
