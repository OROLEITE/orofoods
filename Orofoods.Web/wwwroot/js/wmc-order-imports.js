document.addEventListener('submit', event => {
    const form = event.target.closest('form[data-wmc-import-confirm]');
    if (!form) return;

    if (!window.confirm(form.dataset.wmcImportConfirm || 'Confirmar a importação deste pedido ao WMC?')) {
        event.preventDefault();
    }
});
