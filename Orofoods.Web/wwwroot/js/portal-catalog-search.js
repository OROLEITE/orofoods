const searchForm = document.querySelector('[data-portal-catalog-search]');
const searchGrid = document.querySelector('[data-portal-catalog-grid]');
const searchInput = searchForm?.querySelector('input[name="q"]');
const categorySelect = searchForm?.querySelector('select[name="category"]');
const brandSelect = searchForm?.querySelector('select[name="brand"]');
const resultsStatus = document.querySelector('[data-portal-catalog-results-status]');

if (searchForm && searchGrid && searchInput) {
    let debounceHandle;
    let activeSearch;
    let searchSequence = 0;

    const searchCatalog = async () => {
        const sequence = ++searchSequence;
        activeSearch?.abort();
        activeSearch = new AbortController();

        const target = new URL(searchForm.action || window.location.href, window.location.origin);
        for (const name of ['q', 'category', 'brand']) {
            target.searchParams.delete(name);
        }
        for (const [name, value] of new FormData(searchForm)) {
            if (['q', 'category', 'brand'].includes(name) && String(value).trim()) {
                target.searchParams.set(name, String(value));
            }
        }

        searchForm.setAttribute('aria-busy', 'true');
        if (resultsStatus) {
            resultsStatus.textContent = 'Atualizando produtos...';
        }

        try {
            const response = await fetch(target, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html'
                },
                signal: activeSearch.signal
            });
            if (!response.ok) {
                throw new Error(`Falha ao pesquisar produtos (${response.status}).`);
            }

            const html = await response.text();
            const parsed = new DOMParser().parseFromString(html, 'text/html');
            const updatedGrid = parsed.querySelector('[data-portal-catalog-grid]');
            if (!updatedGrid) {
                window.location.assign(response.url || target.href);
                return;
            }
            if (sequence !== searchSequence) {
                return;
            }

            searchGrid.replaceChildren(...updatedGrid.childNodes);
            window.initializeCatalogProductInteractions(searchGrid);
            window.history.replaceState(null, '', target);
            if (resultsStatus) {
                const count = searchGrid.querySelectorAll(':scope > .product-card').length;
                resultsStatus.textContent = `${count} produtos encontrados.`;
            }
        } catch (error) {
            if (error.name !== 'AbortError' && sequence === searchSequence) {
                window.location.assign(target.href);
            }
        } finally {
            if (sequence === searchSequence) {
                searchForm.removeAttribute('aria-busy');
            }
        }
    };

    const scheduleCatalogSearch = (delay = 200) => {
        window.clearTimeout(debounceHandle);
        debounceHandle = window.setTimeout(searchCatalog, delay);
    };

    searchInput.addEventListener('input', () => scheduleCatalogSearch(200));
    categorySelect?.addEventListener('change', () => scheduleCatalogSearch(0));
    brandSelect?.addEventListener('change', () => scheduleCatalogSearch(0));
    searchForm.addEventListener('submit', (event) => {
        event.preventDefault();
        window.clearTimeout(debounceHandle);
        searchCatalog();
    });
}
