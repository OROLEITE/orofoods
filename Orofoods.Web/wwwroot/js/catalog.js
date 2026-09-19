document.querySelectorAll('[data-catalog-add-form]').forEach((form) => {
    form.addEventListener('submit', async (event) => {
        event.preventDefault();

        const button = form.querySelector('button[type="submit"]');
        const quantityInput = form.querySelector('input[name="quantity"]');
        const status = form.querySelector('.catalog-add-status');
        const cartCount = document.querySelector('.catalog-cart-count');
        const originalLabel = button.innerHTML;

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
            button.innerHTML = '<i class="fa-solid fa-check" aria-hidden="true"></i> <span>No carrinho</span>';
            button.setAttribute('aria-label', 'Produto no carrinho');
            status.textContent = 'Produto adicionado ao carrinho.';

            if (payload.cartQuantity > 0) {
                const cartLink = document.querySelector('.catalog-cart-link');
                if (cartCount) {
                    cartCount.textContent = payload.cartQuantity;
                } else {
                    const count = document.createElement('span');
                    count.className = 'catalog-cart-count';
                    count.setAttribute('aria-hidden', 'true');
                    count.textContent = payload.cartQuantity;
                    cartLink.append(count);
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