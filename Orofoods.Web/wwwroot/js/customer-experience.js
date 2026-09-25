(() => {
    document.querySelectorAll('[data-registration-wizard]').forEach((form) => {
        const panels = [...form.querySelectorAll('[data-registration-step]')];
        const indicators = [...form.querySelectorAll('[data-registration-indicator]')];
        const status = form.querySelector('[data-registration-status]');
        if (panels.length !== 4) return;

        const panelNames = ['Empresa', 'Contato', 'Endereço', 'Acesso'];
        const invalidPanel = panels.findIndex((panel) => panel.querySelector('.input-validation-error, .field-validation-error'));
        let currentStep = invalidPanel >= 0 ? invalidPanel : 0;

        const showStep = (step, focus = false) => {
            currentStep = Math.max(0, Math.min(panels.length - 1, step));
            panels.forEach((panel, index) => { panel.hidden = index !== currentStep; });
            indicators.forEach((indicator, index) => {
                if (index === currentStep) indicator.setAttribute('aria-current', 'step');
                else indicator.removeAttribute('aria-current');
                indicator.classList.toggle('is-complete', index < currentStep);
            });
            if (status) status.textContent = `Etapa ${currentStep + 1} de ${panels.length}: ${panelNames[currentStep]}`;
            if (focus) {
                const heading = panels[currentStep].querySelector('h2');
                heading?.setAttribute('tabindex', '-1');
                heading?.focus({ preventScroll: true });
            }
        };

        form.querySelectorAll('[data-registration-next]').forEach((button) => {
            button.addEventListener('click', () => {
                const fields = [...panels[currentStep].querySelectorAll('input, select, textarea')]
                    .filter((field) => field.willValidate && !field.checkValidity());
                if (fields.length) {
                    fields[0].reportValidity();
                    fields[0].focus();
                    return;
                }
                showStep(currentStep + 1, true);
            });
        });

        form.querySelectorAll('[data-registration-previous]').forEach((button) => {
            button.addEventListener('click', () => showStep(currentStep - 1, true));
        });

        showStep(currentStep);
    });
})();
