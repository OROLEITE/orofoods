(() => {
    const shell = document.querySelector('[data-portal-shell]');
    if (!shell) return;

    const toggle = shell.querySelector('[data-portal-menu-toggle]');
    const closeButtons = shell.querySelectorAll('[data-portal-menu-close]');
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
    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && shell.classList.contains('portal-menu-open')) close();
    });
})();