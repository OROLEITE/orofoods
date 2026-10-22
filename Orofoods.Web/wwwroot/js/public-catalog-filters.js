(() => {
    const buttons = [...document.querySelectorAll("[data-catalog-filter]")];
    const products = [...document.querySelectorAll("[data-catalog-product]")];
    const emptyState = document.querySelector("[data-catalog-filter-empty]");

    if (!buttons.length || !products.length) return;

    const applyFilter = (selectedFilter) => {
        let visibleCount = 0;

        products.forEach((product) => {
            const tags = (product.dataset.catalogTags || "").split(/\s+/).filter(Boolean);
            const isVisible = selectedFilter === "all" || tags.includes(selectedFilter);
            product.hidden = !isVisible;
            if (isVisible) visibleCount++;
        });

        buttons.forEach((button) => {
            const isActive = button.dataset.catalogFilter === selectedFilter;
            button.classList.toggle("is-active", isActive);
            button.setAttribute("aria-pressed", String(isActive));
        });

        if (emptyState) emptyState.hidden = visibleCount > 0;
    };

    buttons.forEach((button) => {
        button.addEventListener("click", () => applyFilter(button.dataset.catalogFilter));
    });

    // Home shortcuts link to /produtos?filtro=<slug>; only filters that exist as buttons are honored.
    const requestedFilter = new URLSearchParams(window.location.search).get("filtro");
    if (requestedFilter && buttons.some((button) => button.dataset.catalogFilter === requestedFilter)) {
        applyFilter(requestedFilter);
    }
})();
