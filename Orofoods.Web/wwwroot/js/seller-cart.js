document.querySelectorAll('[data-stepper]').forEach((button) => {
    button.addEventListener('click', () => {
        const input = button.parentElement?.querySelector('input[type="number"]');
        if (!input) return;
        const minimum = Number(input.min || 1);
        const current = Number(input.value || minimum);
        input.value = String(Math.max(minimum, current + (button.dataset.stepper === 'increase' ? 1 : -1)));
    });
});
