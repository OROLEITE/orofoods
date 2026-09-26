(() => {
    const shell = document.querySelector('[data-admin-shell]');
    const sidebar = shell?.querySelector('.admin-sidebar');
    const toggle = shell?.querySelector('[data-admin-sidebar-toggle]');
    if (!shell || !sidebar || !toggle) return;

    const storageKey = 'admin-sidebar-collapsed';
    const submenuSelector = '.admin-sidebar .admin-nav-submenu';
    const getCollapse = (element) => bootstrap.Collapse.getOrCreateInstance(element, { toggle: false });

    const closeDesktopSubmenus = () => {
        shell.querySelectorAll(submenuSelector).forEach((submenu) => {
            submenu.classList.remove('show');
            const trigger = sidebar.querySelector(`[aria-controls="${submenu.id}"]`);
            trigger?.setAttribute('aria-expanded', 'false');
        });
    };

    const restoreActiveSubmenu = () => {
        const activeSubmenu = sidebar.querySelector('[data-admin-active-group="true"]');
        if (activeSubmenu) getCollapse(activeSubmenu).show();
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
})();