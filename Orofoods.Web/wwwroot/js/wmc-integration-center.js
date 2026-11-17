(() => {
    const tablist = document.querySelector('[data-wmc-tabs]');
    if (!tablist) return;

    const tabs = [...tablist.querySelectorAll('[data-wmc-tab]')];
    const panels = [...document.querySelectorAll('[data-wmc-tab-panel]')];
    const serverState = tablist.dataset.wmcServerState === 'true';
    const validTabs = new Set(tabs.map(tab => tab.dataset.wmcTab));

    const activate = (name, updateHash = true) => {
        const selected = validTabs.has(name) ? name : 'overview';
        tabs.forEach(tab => {
            const isSelected = tab.dataset.wmcTab === selected;
            tab.setAttribute('aria-selected', isSelected.toString());
            tab.tabIndex = isSelected ? 0 : -1;
        });
        panels.forEach(panel => {
            const isActive = panel.dataset.wmcTabPanel === selected;
            panel.hidden = !isActive;
            panel.toggleAttribute('data-wmc-active', isActive);
        });
        if (updateHash && !serverState && window.history?.replaceState) {
            window.history.replaceState(null, '', `#${selected}`);
        }
        const panel = panels.find(item => item.dataset.wmcTabPanel === selected);
        if (panel && document.activeElement?.matches('[data-wmc-tab]')) panel.focus({ preventScroll: true });
    };

    const hashTab = window.location.hash.slice(1);
    activate(serverState ? tablist.dataset.wmcInitialTab : hashTab || tablist.dataset.wmcInitialTab || 'overview', false);

    tabs.forEach((tab, index) => {
        tab.addEventListener('click', () => activate(tab.dataset.wmcTab));
        tab.addEventListener('keydown', event => {
            const key = event.key;
            if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(key)) return;
            event.preventDefault();
            const nextIndex = key === 'Home' ? 0 : key === 'End' ? tabs.length - 1 : (index + (key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
            tabs[nextIndex].focus();
            activate(tabs[nextIndex].dataset.wmcTab);
        });
    });

    window.addEventListener('hashchange', () => {
        if (!serverState) activate(window.location.hash.slice(1), false);
    });

    const multiSearch = document.querySelector('[data-wmc-search="multi-brand"]');
    const multiRows = [...document.querySelectorAll('[data-wmc-brand-row]')];
    const multiChecks = [...document.querySelectorAll('[data-wmc-brand-checkbox]')];
    const multiCount = document.getElementById('wmcMultiBrandSelectionCount');
    const updateMultiBrands = () => {
        const term = (multiSearch?.value || '').trim().toLocaleLowerCase();
        multiRows.forEach(row => {
            row.hidden = term.length > 0 && !(row.dataset.brandSearch || '').toLocaleLowerCase().includes(term);
        });
        const count = multiChecks.filter(input => input.checked).length;
        if (multiCount) multiCount.textContent = `${count} marca${count === 1 ? '' : 's'} selecionada${count === 1 ? '' : 's'}`;
    };
    multiSearch?.addEventListener('input', updateMultiBrands);
    multiChecks.forEach(input => input.addEventListener('change', updateMultiBrands));
    updateMultiBrands();

    document.querySelectorAll('[data-wmc-submit-once]').forEach(form => {
        form.addEventListener('submit', event => {
            if (form.dataset.wmcSubmitting === 'true') {
                event.preventDefault();
                return;
            }
            form.dataset.wmcSubmitting = 'true';
            form.setAttribute('aria-busy', 'true');
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(button => {
                button.disabled = true;
                button.dataset.wmcLoading = 'true';
                button.dataset.wmcOriginalLabel = button.textContent || button.value || '';
                const loadingLabel = button.getAttribute('data-wmc-loading') || (button.matches('[data-wmc-confirm-submit]') ? 'Importando...' : 'Processando...');
                if ('value' in button && button.tagName === 'INPUT') button.value = loadingLabel;
                else button.textContent = loadingLabel;
            });
        });
    });

    const diagnosticInput = document.getElementById('diagnosticOrderId');
    const diagnosticResult = document.querySelector('[data-wmc-order-eligibility]');
    if (diagnosticResult) diagnosticResult.setAttribute('tabindex', '-1');
    if (diagnosticResult && new URLSearchParams(window.location.search).has('diagnosticOrderId')) {
        diagnosticResult.focus({ preventScroll: true });
    } else if (diagnosticInput && new URLSearchParams(window.location.search).has('diagnosticOrderId')) {
        diagnosticInput.focus({ preventScroll: true });
    }
})();
