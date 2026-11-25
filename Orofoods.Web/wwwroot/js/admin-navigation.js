(() => {
    const shell = document.querySelector('[data-admin-shell]');
    if (!shell) return;

    const navigationStorageKey = 'orofoods.admin.navigation.mode';
    const navigationLinksSelector = '[data-admin-navigation-menu] a.admin-nav-link[href]';
    const navigationModeSelector = '[data-admin-navigation-mode]';
    const originalLinkAttributes = new WeakMap();

    const normalizeNavigationMode = (mode) => mode === 'same' ? 'same' : 'new';

    const readNavigationMode = () => {
        try {
            return normalizeNavigationMode(localStorage.getItem(navigationStorageKey));
        } catch {
            return 'new';
        }
    };

    const applyNavigationMode = (mode) => {
        const openInNewTab = mode === 'new';

        document.querySelectorAll(navigationLinksSelector).forEach((navigationLink) => {
            if (!originalLinkAttributes.has(navigationLink)) {
                originalLinkAttributes.set(navigationLink, {
                    target: navigationLink.getAttribute('target'),
                    rel: navigationLink.getAttribute('rel')
                });
            }

            const original = originalLinkAttributes.get(navigationLink);
            if (openInNewTab) {
                navigationLink.setAttribute('target', '_blank');
                const relValues = new Set((original.rel || '').split(/\s+/).filter(Boolean));
                relValues.add('noopener');
                navigationLink.setAttribute('rel', Array.from(relValues).join(' '));
                return;
            }

            if (original.target) navigationLink.setAttribute('target', original.target);
            else navigationLink.removeAttribute('target');

            if (original.rel) navigationLink.setAttribute('rel', original.rel);
            else navigationLink.removeAttribute('rel');
        });
    };

    const setNavigationMode = (mode, persist = true) => {
        const normalizedMode = normalizeNavigationMode(mode);
        document.querySelectorAll(navigationModeSelector).forEach((control) => {
            control.value = normalizedMode;
        });
        applyNavigationMode(normalizedMode);

        if (persist) {
            try {
                localStorage.setItem(navigationStorageKey, normalizedMode);
            } catch {
                // Navigation continues with the current-page preference.
            }
        }
    };

    document.querySelectorAll(navigationModeSelector).forEach((control) => {
        control.addEventListener('change', (event) => setNavigationMode(event.target.value));
    });
    setNavigationMode(readNavigationMode(), false);

    const sidebar = shell.querySelector('.admin-sidebar');
    const toggle = shell.querySelector('[data-admin-sidebar-toggle]');
    if (sidebar && toggle) {
        const storageKey = 'admin-sidebar-collapsed';
        const submenuSelector = '.admin-sidebar .admin-nav-submenu';
        const getCollapse = (element) => window.bootstrap?.Collapse?.getOrCreateInstance(element, { toggle: false });

        const closeDesktopSubmenus = () => {
            shell.querySelectorAll(submenuSelector).forEach((submenu) => {
                submenu.classList.remove('show');
                const trigger = sidebar.querySelector(`[aria-controls="${submenu.id}"]`);
                trigger?.setAttribute('aria-expanded', 'false');
            });
        };

        const restoreActiveSubmenu = () => {
            const activeSubmenu = sidebar.querySelector('[data-admin-active-group="true"]');
            if (activeSubmenu) getCollapse(activeSubmenu)?.show();
        };

        const setCollapsed = (collapsed, persist = true) => {
            shell.classList.toggle('admin-sidebar-collapsed', collapsed);
            toggle.setAttribute('aria-expanded', String(!collapsed));
            toggle.setAttribute('aria-label', collapsed ? 'Expandir menu lateral' : 'Recolher menu lateral');
            toggle.title = collapsed ? 'Expandir menu lateral' : 'Recolher menu lateral';
            const icon = toggle.querySelector('i');
            icon?.classList.toggle('fa-chevron-left', !collapsed);
            icon?.classList.toggle('fa-chevron-right', collapsed);

            if (collapsed) closeDesktopSubmenus();
            else restoreActiveSubmenu();

            if (persist) {
                try {
                    localStorage.setItem(storageKey, String(collapsed));
                } catch {
                    // The navigation still works when browser storage is unavailable.
                }
            }
        };

        let initiallyCollapsed = false;
        try {
            initiallyCollapsed = localStorage.getItem(storageKey) === 'true';
        } catch {
            initiallyCollapsed = false;
        }
        setCollapsed(initiallyCollapsed, false);

        toggle.addEventListener('click', () => {
            setCollapsed(!shell.classList.contains('admin-sidebar-collapsed'));
        });

        document.addEventListener('click', (event) => {
            if (shell.classList.contains('admin-sidebar-collapsed') && !sidebar.contains(event.target)) {
                closeDesktopSubmenus();
            }
        });

        sidebar.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && shell.classList.contains('admin-sidebar-collapsed')) {
                closeDesktopSubmenus();
                toggle.focus();
            }
        });
    }

    const ignoredFieldTypes = new Set(['hidden', 'password', 'file', 'submit', 'button', 'reset', 'image']);
    const isTrackedField = (field) => {
        if (field.disabled) return false;
        const type = (field.type || '').toLowerCase();
        return !ignoredFieldTypes.has(type);
    };

    const formState = (form) => Array.from(form.elements)
        .map((field, index) => ({ field, index }))
        .filter(({ field }) => isTrackedField(field))
        .map(({ field, index }) => {
            const fieldKey = field.name || field.id || `field-${index}`;
            const type = (field.type || '').toLowerCase();
            if (type === 'checkbox' || type === 'radio') return `${fieldKey}:${field.checked}`;
            if (field.tagName === 'SELECT' && field.multiple) {
                return `${fieldKey}:${Array.from(field.selectedOptions).map((option) => option.value).join(',')}`;
            }
            return `${fieldKey}:${field.value}`;
        })
        .join('\u001f');

    const trackedForms = Array.from(document.querySelectorAll('form[method="post" i]'))
        .filter((form) => Array.from(form.elements).some(isTrackedField));
    const initialFormStates = new WeakMap(trackedForms.map((form) => [form, formState(form)]));
    const intentionalSubmits = new WeakSet();

    trackedForms.forEach((form) => {
        form.addEventListener('submit', (event) => {
            if (event.defaultPrevented) return;
            intentionalSubmits.add(form);

            // A later validator may cancel this submit after the capture listener.
            setTimeout(() => intentionalSubmits.delete(form), 0);
        }, { capture: true });
    });

    window.addEventListener('beforeunload', (event) => {
        const hasUnsavedChanges = trackedForms.some((form) =>
            !intentionalSubmits.has(form) && formState(form) !== initialFormStates.get(form));
        if (!hasUnsavedChanges) return;

        event.preventDefault();
        event.returnValue = '';
    });
})();
