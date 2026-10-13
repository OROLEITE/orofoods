(() => {
    const form = document.getElementById('seller-order-draft-form');
    if (!form) return;

    const searchInput = document.getElementById('product-search');
    const searchButton = document.getElementById('product-search-button');
    const resultsHost = document.getElementById('product-results');
    const searchStatus = document.getElementById('product-search-status');
    const paginationHost = document.getElementById('product-pagination');
    const itemsHost = document.getElementById('draft-items');
    const totalHost = document.getElementById('draft-total');
    const itemCountHost = document.getElementById('draft-item-count');
    const summaryCountHost = document.getElementById('summary-item-count');
    const removalStatus = document.getElementById('draft-removal-status');
    const emptyItems = document.getElementById('draft-items-empty');
    const saveButton = document.getElementById('save-draft-button');
    const persisted = form.dataset.persisted === 'true';
    const maximumPageSize = 20;
    let searchTimer;
    let activeRequest;
    let currentPage = 1;
    let totalPages = 0;
    let isDirty = false;

    const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
    const itemRows = () => Array.from(itemsHost.querySelectorAll('[data-draft-item]'));
    const activeRows = () => itemRows().filter(row => !row.classList.contains('is-removal-pending'));

    function productIdsMarkedForRemoval() {
        return new Set(Array.from(form.querySelectorAll('[data-removed-product-id]'))
            .map(input => Number(input.dataset.removedProductId)));
    }

    function renumberItemInputs() {
        activeRows().forEach((row, index) => {
            const productId = row.dataset.productId;
            const idInput = row.querySelector('[data-item-product-id]');
            const quantityInput = row.querySelector('[data-item-quantity]');
            idInput.name = `Items[${index}].ProductId`;
            idInput.value = productId;
            quantityInput.name = `Items[${index}].Quantity`;
        });
    }

    function updateSummary() {
        const rows = activeRows();
        let total = 0;
        rows.forEach(row => {
            const quantityInput = row.querySelector('[data-item-quantity]');
            const quantity = Math.max(0, Number.parseInt(quantityInput?.value ?? '0', 10) || 0);
            const price = Number.parseFloat(row.dataset.unitPrice ?? '0') || 0;
            total += price * quantity;
            const subtotal = row.querySelector('[data-item-subtotal]');
            if (subtotal) subtotal.textContent = money.format(price * quantity);
        });
        totalHost.textContent = money.format(total);
        itemCountHost.textContent = String(rows.length);
        summaryCountHost.textContent = String(rows.length);
        emptyItems.hidden = rows.length > 0;
    }

    function markDirty() {
        isDirty = true;
    }

    function makeNode(tagName, className, text) {
        const element = document.createElement(tagName);
        if (className) element.className = className;
        if (text !== undefined) element.textContent = text;
        return element;
    }

    function addProduct(product) {
        const existing = itemRows().find(row => Number(row.dataset.productId) === product.id);
        if (existing) {
            if (existing.classList.contains('is-removal-pending')) undoRemoval(product.id);
            const quantity = existing.querySelector('[data-item-quantity]');
            const minimum = Math.max(1, product.minimumCases || Number(existing.dataset.minimum) || 1);
            quantity.value = String((Number.parseInt(quantity.value || '0', 10) || minimum) + minimum);
            quantity.dispatchEvent(new Event('input', { bubbles: true }));
            markDirty();
            searchStatus.textContent = `${product.name} teve a quantidade atualizada.`;
            updateSummary();
            return;
        }

        const row = makeNode('article', 'seller-order-drafts__item');
        row.dataset.draftItem = '';
        row.dataset.productId = String(product.id);
        row.dataset.unitPrice = String(product.unitPrice);
        row.dataset.minimum = String(product.minimumCases);

        const hiddenId = document.createElement('input');
        hiddenId.type = 'hidden';
        hiddenId.dataset.itemProductId = '';
        hiddenId.value = String(product.id);
        row.append(hiddenId);

        const main = makeNode('div', 'seller-order-drafts__item-main');
        main.append(makeNode('span', 'seller-order-drafts__sku', product.sku));
        main.append(makeNode('h3', '', product.name));
        main.append(makeNode('span', 'seller-order-drafts__muted', product.unitDescription));
        main.append(makeNode('span', 'seller-order-drafts__unit-price', `${money.format(product.unitPrice)} por unidade comercial`));
        row.append(main);

        const controls = makeNode('div', 'seller-order-drafts__item-controls');
        const label = document.createElement('label');
        label.htmlFor = `quantity-${product.id}`;
        label.append(document.createTextNode('Quantidade '));
        label.append(makeNode('small', '', `(mín. ${product.minimumCases})`));
        controls.append(label);
        const quantity = document.createElement('input');
        quantity.id = `quantity-${product.id}`;
        quantity.className = 'form-control seller-order-drafts__quantity';
        quantity.type = 'number';
        quantity.inputMode = 'numeric';
        quantity.min = String(product.minimumCases);
        quantity.step = '1';
        quantity.required = true;
        quantity.value = String(product.minimumCases);
        quantity.dataset.itemQuantity = '';
        controls.append(quantity);
        const discount = makeNode('span', 'seller-order-drafts__discount');
        discount.append(document.createTextNode('Desconto '));
        discount.append(makeNode('strong', '', '0%'));
        discount.append(makeNode('i', 'fa-solid fa-lock'));
        discount.lastChild.setAttribute('aria-label', 'Bloqueado');
        controls.append(discount);
        const remove = makeNode('button', 'seller-order-drafts__remove');
        remove.type = 'button';
        remove.dataset.removeItem = String(product.id);
        remove.append(makeNode('i', 'fa-regular fa-trash-can'));
        remove.lastChild.setAttribute('aria-hidden', 'true');
        remove.append(document.createTextNode(' Remover'));
        controls.append(remove);
        row.append(controls);

        const subtotal = makeNode('div', 'seller-order-drafts__item-subtotal');
        subtotal.append(makeNode('span', '', 'Subtotal'));
        subtotal.append(makeNode('strong', '', money.format(product.unitPrice * product.minimumCases)));
        subtotal.lastChild.dataset.itemSubtotal = '';
        row.append(subtotal);

        itemsHost.append(row);
        renumberItemInputs();
        markDirty();
        searchStatus.textContent = `${product.name} foi adicionado ao pedido.`;
        updateSummary();
    }

    function removeProduct(productId) {
        const row = itemRows().find(item => Number(item.dataset.productId) === productId);
        if (!row) return;

        if (!persisted) {
            row.remove();
            markDirty();
            removalStatus.textContent = 'Produto removido do rascunho ainda não salvo.';
            renumberItemInputs();
            updateSummary();
            return;
        }

        if (!productIdsMarkedForRemoval().has(productId)) {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'RemovedProductIds';
            input.value = String(productId);
            input.dataset.removedProductId = String(productId);
            form.append(input);
        }
        row.classList.add('is-removal-pending');
        const productInput = row.querySelector('[data-item-product-id]');
        const quantityInput = row.querySelector('[data-item-quantity]');
        productInput.removeAttribute('name');
        quantityInput.removeAttribute('name');
        productInput.disabled = true;
        quantityInput.disabled = true;
        row.querySelector('[data-remove-item]')?.remove();

        const controls = row.querySelector('.seller-order-drafts__item-controls');
        controls.replaceChildren();
        controls.append(makeNode('span', 'seller-order-drafts__pending-removal', 'Marcado para remoção'));
        const undo = makeNode('button', 'seller-order-drafts__undo-removal', 'Desfazer');
        undo.type = 'button';
        undo.dataset.undoRemoval = String(productId);
        controls.append(undo);
        markDirty();
        removalStatus.textContent = 'Produto marcado para remoção. Salve o rascunho para confirmar a alteração.';
        renumberItemInputs();
        updateSummary();
    }

    function undoRemoval(productId) {
        const row = itemRows().find(item => Number(item.dataset.productId) === productId);
        if (!row) return;
        form.querySelectorAll(`[data-removed-product-id="${productId}"]`).forEach(input => input.remove());
        row.classList.remove('is-removal-pending');
        const productInput = row.querySelector('[data-item-product-id]');
        const quantityInput = row.querySelector('[data-item-quantity]');
        productInput.disabled = false;
        quantityInput.disabled = false;
        quantityInput.classList.remove('visually-hidden');
        quantityInput.classList.add('form-control', 'seller-order-drafts__quantity');
        const controls = row.querySelector('.seller-order-drafts__item-controls');
        controls.replaceChildren();
        const label = document.createElement('label');
        label.htmlFor = quantityInput.id;
        label.append(document.createTextNode('Quantidade '));
        label.append(makeNode('small', '', `(mín. ${row.dataset.minimum})`));
        controls.append(label, quantityInput);
        const discount = makeNode('span', 'seller-order-drafts__discount');
        discount.append(document.createTextNode('Desconto '), makeNode('strong', '', '0%'));
        const lock = makeNode('i', 'fa-solid fa-lock');
        lock.setAttribute('aria-label', 'Bloqueado');
        discount.append(lock);
        const remove = makeNode('button', 'seller-order-drafts__remove');
        remove.type = 'button';
        remove.dataset.removeItem = String(productId);
        remove.append(makeNode('i', 'fa-regular fa-trash-can'), document.createTextNode(' Remover'));
        controls.append(discount, remove);
        markDirty();
        removalStatus.textContent = 'A remoção foi desfeita.';
        renumberItemInputs();
        updateSummary();
    }

    function renderProducts(payload) {
        paginationHost.replaceChildren();
        paginationHost.hidden = true;
        totalPages = 0;
        currentPage = payload.page ?? 1;
        resultsHost.replaceChildren();
        const products = payload.products ?? [];
        if (products.length === 0) {
            searchStatus.textContent = 'Nenhum produto disponível encontrado.';
            return;
        }

        const selectedIds = new Set(itemRows().map(row => Number(row.dataset.productId)));
        products.forEach(product => {
            const card = makeNode('article', 'seller-order-drafts__product-card');
            card.append(makeNode('span', 'seller-order-drafts__product-code', `${product.sku}${product.ean ? ` · EAN ${product.ean}` : ''}`));
            card.append(makeNode('h3', '', product.name));
            card.append(makeNode('span', 'seller-order-drafts__product-unit', product.unitDescription));
            card.append(makeNode('strong', 'seller-order-drafts__product-price', money.format(product.unitPrice)));
            card.append(makeNode('span', 'seller-order-drafts__product-minimum', `Mínimo: ${product.minimumCases} caixa(s)`));
            const button = makeNode('button', 'btn seller-order-drafts__outline', selectedIds.has(product.id) ? 'Adicionar mais' : 'Adicionar');
            button.type = 'button';
            button.dataset.addProduct = '';
            button.addEventListener('click', () => addProduct(product));
            card.append(button);
            resultsHost.append(card);
        });

        totalPages = payload.totalPages ?? 0;
        currentPage = payload.page ?? 1;
        paginationHost.hidden = totalPages <= 1;
        if (totalPages > 1) {
            if (currentPage > 1) {
                const previous = makeNode('button', 'seller-order-drafts__pagination-button', 'Anterior');
                previous.type = 'button';
                previous.addEventListener('click', () => searchProducts(currentPage - 1));
                paginationHost.append(previous);
            }
            paginationHost.append(makeNode('span', '', `Página ${currentPage} de ${totalPages}`));
            if (currentPage < totalPages) {
                const next = makeNode('button', 'seller-order-drafts__pagination-button', 'Próxima');
                next.type = 'button';
                next.addEventListener('click', () => searchProducts(currentPage + 1));
                paginationHost.append(next);
            }
        }
        searchStatus.textContent = `${payload.totalCount} produto(s) encontrado(s). Exibindo até ${maximumPageSize} por página.`;
    }

    async function searchProducts(page = 1) {
        const query = searchInput.value.trim();
        if (!query) {
            resultsHost.replaceChildren();
            paginationHost.replaceChildren();
            paginationHost.hidden = true;
            totalPages = 0;
            currentPage = 1;
            searchStatus.textContent = 'Digite para pesquisar produtos disponíveis para este cliente.';
            return;
        }

        activeRequest?.abort();
        activeRequest = new AbortController();
        searchStatus.textContent = 'Pesquisando…';
        const url = new URL(form.dataset.productSearchUrl, window.location.origin);
        url.searchParams.set('q', query);
        url.searchParams.set('page', String(page));

        try {
            const response = await fetch(url, {
                credentials: 'same-origin',
                headers: { Accept: 'application/json' },
                signal: activeRequest.signal
            });
            if (!response.ok) throw new Error('Não foi possível pesquisar o catálogo.');
            renderProducts(await response.json());
        } catch (error) {
            if (error.name === 'AbortError') return;
            resultsHost.replaceChildren();
            paginationHost.replaceChildren();
            paginationHost.hidden = true;
            totalPages = 0;
            currentPage = 1;
            searchStatus.textContent = error.message || 'Ocorreu um erro na busca. Tente novamente.';
        }
    }

    searchInput.addEventListener('input', () => {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => searchProducts(1), 250);
    });
    searchButton.addEventListener('click', () => searchProducts(1));
    searchInput.addEventListener('keydown', event => {
        if (event.key === 'Enter') {
            event.preventDefault();
            searchProducts(1);
        }
    });

    itemsHost.addEventListener('input', event => {
        if (event.target.matches('[data-item-quantity]')) {
            markDirty();
            updateSummary();
        }
    });
    itemsHost.addEventListener('change', event => {
        if (event.target.matches('[data-item-quantity]')) {
            const minimum = Number(event.target.closest('[data-draft-item]').dataset.minimum) || 1;
            const quantity = Number.parseInt(event.target.value || '0', 10) || minimum;
            event.target.value = String(Math.max(minimum, quantity));
            updateSummary();
        }
    });
    itemsHost.addEventListener('click', event => {
        const removeButton = event.target.closest('[data-remove-item]');
        if (removeButton) removeProduct(Number(removeButton.dataset.removeItem));
        const undoButton = event.target.closest('[data-undo-removal]');
        if (undoButton) undoRemoval(Number(undoButton.dataset.undoRemoval));
    });

    form.addEventListener('input', event => {
        if (event.target !== searchInput) markDirty();
    });
    form.addEventListener('change', event => {
        if (event.target !== searchInput) markDirty();
    });
    form.addEventListener('submit', () => {
        isDirty = false;
        renumberItemInputs();
        saveButton.disabled = true;
        saveButton.setAttribute('aria-busy', 'true');
        saveButton.textContent = 'Salvando rascunho…';
    });

    window.addEventListener('beforeunload', event => {
        if (!isDirty) return;
        event.preventDefault();
        event.returnValue = '';
    });

    function updateKeyboardVisibility() {
        const active = document.activeElement;
        const isEditing = active && active.matches('input, textarea, select');
        const keyboardLikelyOpen = window.matchMedia('(max-width: 767.98px)').matches && isEditing;
        document.documentElement.classList.toggle('is-keyboard-open', keyboardLikelyOpen);
    }

    document.addEventListener('focusin', updateKeyboardVisibility);
    document.addEventListener('focusout', () => window.setTimeout(updateKeyboardVisibility, 50));
    window.addEventListener('resize', updateKeyboardVisibility);
    window.visualViewport?.addEventListener('resize', updateKeyboardVisibility);
    updateSummary();
})();
