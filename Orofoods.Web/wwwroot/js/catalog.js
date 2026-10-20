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
        const quantityInput = form.querySelector('input[name="quantity"]');
        const stepperButtons = form.querySelectorAll('.catalog-quantity-button');
        const status = form.querySelector('.catalog-add-status');
        const canUpdateQuantity = form.dataset.catalogAllowQuantityUpdate === 'true';
        const originalLabel = button.innerHTML;
        const cartLink = document.querySelector('.catalog-header-cart');
        let cartCount = cartLink?.querySelector('.catalog-cart-count');

        button.disabled = true;
        button.setAttribute('aria-busy', 'true');
        button.textContent = 'Adicionando...';
        status.textContent = '';

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const payload = await response.json();
            if (!response.ok) {
                throw new Error(payload.message || 'Não foi possível atualizar o produto no carrinho.');
            }

            quantityInput.value = payload.quantity;
            if (canUpdateQuantity) {
                button.disabled = false;
                button.innerHTML = '<i class="fa-solid fa-cart-arrow-up" aria-hidden="true"></i> <span>Atualizar</span>';
                button.setAttribute('aria-label', 'Atualizar quantidade no carrinho');
                status.textContent = 'Quantidade do carrinho atualizada.';
            } else {
                quantityInput.disabled = true;
                stepperButtons.forEach((stepperButton) => { stepperButton.disabled = true; });
                button.innerHTML = '<i class="fa-solid fa-check" aria-hidden="true"></i> <span>No carrinho</span>';
                button.setAttribute('aria-label', 'Produto no carrinho');
                status.textContent = 'Produto adicionado ao carrinho.';
            }

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
            button.innerHTML = originalLabel;
            status.textContent = error.message;
        } finally {
            button.removeAttribute('aria-busy');
        }
    });
});
};

window.initializeCatalogProductInteractions = initializeCatalogProductInteractions;
initializeCatalogProductInteractions();
