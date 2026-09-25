(() => {
    const shell = document.querySelector('[data-portal-shell]');
    if (!shell) return;

    const toggle = shell.querySelector('[data-portal-menu-toggle]');
    const sidebarToggle = shell.querySelector('[data-portal-sidebar-toggle]');
    const closeButtons = shell.querySelectorAll('[data-portal-menu-close]');
    const storageKey = 'portal-sidebar-collapsed';
    const close = () => {
        shell.classList.remove('portal-menu-open');
        toggle.setAttribute('aria-expanded', 'false');
        toggle.focus();
    };
    const open = () => {
        shell.classList.add('portal-menu-open');
        toggle.setAttribute('aria-expanded', 'true');
        shell.querySelector('[data-portal-menu-close]').focus();
    };

    toggle.addEventListener('click', () => shell.classList.contains('portal-menu-open') ? close() : open());
    closeButtons.forEach((button) => button.addEventListener('click', close));

    const setCollapsed = (collapsed, persist = true) => {
        shell.classList.toggle('portal-sidebar-collapsed', collapsed);
        sidebarToggle?.setAttribute('aria-expanded', String(!collapsed));
        sidebarToggle?.setAttribute('aria-label', collapsed ? 'Expandir menu lateral' : 'Recolher menu lateral');
        if (sidebarToggle) sidebarToggle.title = collapsed ? 'Expandir menu lateral' : 'Recolher menu lateral';
        const icon = sidebarToggle?.querySelector('i');
        icon?.classList.toggle('fa-chevron-left', !collapsed);
        icon?.classList.toggle('fa-chevron-right', collapsed);
        if (persist) {
            try { localStorage.setItem(storageKey, String(collapsed)); } catch { }
        }
    };

    let initiallyCollapsed = false;
    try { initiallyCollapsed = localStorage.getItem(storageKey) === 'true'; } catch { }
    setCollapsed(initiallyCollapsed, false);
    sidebarToggle?.addEventListener('click', () => setCollapsed(!shell.classList.contains('portal-sidebar-collapsed')));

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && shell.classList.contains('portal-menu-open')) close();
    });
})();