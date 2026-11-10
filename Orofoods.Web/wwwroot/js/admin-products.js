(() => {
    const form = document.querySelector("[data-admin-product-search]");
    const searchInput = form?.querySelector('input[name="q"]');

    if (!form || !searchInput) return;

    let debounceTimer;
    searchInput.addEventListener("input", () => {
        window.clearTimeout(debounceTimer);
        debounceTimer = window.setTimeout(() => form.requestSubmit(), 300);
    });

    form.addEventListener("submit", () => window.clearTimeout(debounceTimer));
})();
