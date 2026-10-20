const initializeCatalogProductInteractions = (root = document) => {
    root.querySelectorAll('[data-catalog-quantity-stepper]').forEach((stepper) => {
    const input = stepper.querySelector('input[name="quantity"]');
    const minimum = Math.max(1, Number.parseInt(input.min, 10) || 1);
    const maximum = Number.parseInt(input.max, 10) || Number.POSITIVE_INFINITY;
    const decrease = stepper.querySelector('[data-catalog-quantity-decrease]');
    const increase = stepper.querySelector('[data-catalog-quantity-increase]');

    const normalizeQuantity = () => {
        if (input.disabled) {
            decrease.disabled = true;
            increase.disabled = true;
            return;
        }
        const value = Number.parseInt(input.value, 10);
        input.value = Math.min(maximum, Math.max(minimum, Number.isFinite(value) ? value : minimum));
        decrease.disabled = Number(input.value) <= minimum;
        increase.disabled = Number(input.value) >= maximum;
    };

    decrease.addEventListener('click', () => {
        normalizeQuantity();
        input.value = Math.max(minimum, Number(input.value) - 1);
        input.dispatchEvent(new Event('input', { bubbles: true }));
    });

    increase.addEventListener('click', () => {
        normalizeQuantity();
        input.value = Number(input.value) + 1;
        input.dispatchEvent(new Event('input', { bubbles: true }));
    });

    input.addEventListener('change', normalizeQuantity);
    normalizeQuantity();
});

    root.querySelectorAll('[data-catalog-add-form]').forEach((form) => {
    form.addEventListener('submit', async (event) => {
        event.preventDefault();

        const button = form.querySelector('button[type="submit"]');
        const status = form.querySelector('.catalog-add-status');
        const cartLink = document.querySelector('.catalog-header-cart');
        let cartCount = cartLink?.querySelector('.catalog-cart-count');

        button.disabled = true;
        button.setAttribute('aria-busy', 'true');
        status.textContent = '';

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const payload = await response.json();
            if (!response.ok) {
                throw new Error(payload.message || 'Falha ao adicionar ao carrinho.');
            }

            button.disabled = false;
            status.textContent = payload.message || 'Produto adicionado ao carrinho.';

            if (cartLink && payload.cartQuantity >= 0) {
                if (cartCount) {
                    cartCount.textContent = payload.cartQuantity;
                } else {
                    cartCount = document.createElement('span');
                    cartCount.className = 'catalog-cart-count';
                    cartCount.setAttribute('aria-live', 'polite');
                    cartCount.textContent = payload.cartQuantity;
                    cartLink.append(cartCount);
                }
                cartLink.setAttribute('aria-label', `Carrinho com ${payload.cartQuantity} unidades`);
            }
        } catch (error) {
            button.disabled = false;
            status.textContent = error.message;
        } finally {
            button.removeAttribute('aria-busy');
        }
    });
});
};

window.initializeCatalogProductInteractions = initializeCatalogProductInteractions;
initializeCatalogProductInteractions();
