document.querySelectorAll('[data-catalog-quantity-stepper]').forEach((stepper) => {
    const input = stepper.querySelector('input[name="quantity"]');
    const minimum = Math.max(1, Number.parseInt(input.min, 10) || 1);
    const decrease = stepper.querySelector('[data-catalog-quantity-decrease]');
    const increase = stepper.querySelector('[data-catalog-quantity-increase]');

    const normalizeQuantity = () => {
        const value = Number.parseInt(input.value, 10);
        input.value = Math.max(minimum, Number.isFinite(value) ? value : minimum);
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
});

document.querySelectorAll('[data-catalog-add-form]').forEach((form) => {
    form.addEventListener('submit', async (event) => {
        event.preventDefault();

        const button = form.querySelector('button[type="submit"]');
        const quantityInput = form.querySelector('input[name="quantity"]');
        const stepperButtons = form.querySelectorAll('.catalog-quantity-button');
        const status = form.querySelector('.catalog-add-status');
        const originalLabel = button.innerHTML;
        let cartCount = document.querySelector('.catalog-cart-count');

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
                throw new Error(payload.message || 'Não foi possível adicionar o produto ao carrinho.');
            }

            quantityInput.value = payload.quantity;
            quantityInput.disabled = true;
            stepperButtons.forEach((stepperButton) => { stepperButton.disabled = true; });
            button.innerHTML = '<i class="fa-solid fa-check" aria-hidden="true"></i> <span>No carrinho</span>';
            button.setAttribute('aria-label', 'Produto no carrinho');
            status.textContent = 'Produto adicionado ao carrinho.';

            if (payload.cartQuantity > 0) {
                const cartLink = document.querySelector('.catalog-cart-link');
                if (cartCount) {
                    cartCount.textContent = payload.cartQuantity;
                } else {
                    cartCount = document.createElement('span');
                    cartCount.className = 'catalog-cart-count';
                    cartCount.setAttribute('aria-hidden', 'true');
                    cartCount.textContent = payload.cartQuantity;
                    cartLink.append(cartCount);
                }
                cartLink.setAttribute('aria-label', `Ver carrinho com ${payload.cartQuantity} caixas`);
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
