(() => {
    const root = document.querySelector("[data-assisted-customer-search]");
    if (!root) return;

    const input = root.querySelector("[data-assisted-customer-query]");
    const results = root.querySelector("[data-assisted-customer-results]");
    const status = root.querySelector("[data-assisted-customer-status]");
    const moreButton = root.querySelector("[data-assisted-customer-more]");
    if (!input || !results || !status || !moreButton) return;

    const minimumQueryLength = 2;
    let timer = 0;
    let requestNumber = 0;
    let requestController = null;
    let page = 1;
    let hasMore = false;
    let items = [];
    let activeIndex = -1;
    let selectFirstAfterSearch = false;

    const setExpanded = value => {
        input.setAttribute("aria-expanded", String(value));
    };

    const clearResults = () => {
        results.replaceChildren();
        items = [];
        activeIndex = -1;
        page = 1;
        hasMore = false;
        input.removeAttribute("aria-activedescendant");
        moreButton.hidden = true;
        setExpanded(false);
    };

    const createOption = (customer, index) => {
        const option = document.createElement("button");
        option.type = "button";
        option.className = "assisted-customer-option";
        option.id = `assisted-customer-option-${customer.id}`;
        option.dataset.customerId = String(customer.id);
        option.setAttribute("role", "option");
        option.setAttribute("aria-selected", String(index === activeIndex));
        const identity = [customer.tradeName, customer.legalName, customer.cnpj, customer.wmcCode]
            .filter(Boolean)
            .join(" · ");
        option.textContent = identity;
        return option;
    };

    const render = (pageResult, append) => {
        if (!append) clearResults();
        const incoming = Array.isArray(pageResult.items) ? pageResult.items : [];
        const startIndex = items.length;
        items = items.concat(incoming);
        incoming.forEach((customer, index) => results.append(createOption(customer, startIndex + index)));
        page = Number(pageResult.page) || page;
        hasMore = Boolean(pageResult.hasMore);
        moreButton.hidden = !hasMore;
        setExpanded(items.length > 0);
        status.textContent = items.length
            ? `${items.length} cliente(s) carregado(s).${hasMore ? " Use Mostrar mais clientes para continuar." : ""}`
            : "Nenhum cliente autorizado encontrado.";

        if (selectFirstAfterSearch) {
            selectFirstAfterSearch = false;
            if (items.length) selectCustomer(items[0].id);
        }
    };

    const selectCustomer = customerId => {
        const selectedId = Number(root.dataset.selectedCustomerId) || 0;
        if (selectedId && selectedId === Number(customerId)) {
            input.value = root.dataset.selectedCustomerName || input.value;
            clearResults();
            status.textContent = "Este cliente já está selecionado.";
            return;
        }
        if (selectedId && selectedId !== Number(customerId)) {
            const hasEnteredLines = document.querySelectorAll("#assisted-order-lines .assisted-order-line").length > 0;
            if (hasEnteredLines && !window.confirm("Trocar o cliente descartará os itens digitados. Deseja continuar?")) {
                input.value = root.dataset.selectedCustomerName || input.value;
                clearResults();
                return;
            }
        }

        const destination = root.dataset.orderUrlTemplate.replace("__id__", encodeURIComponent(customerId));
        window.location.assign(destination);
    };

    const searchPage = async (requestedPage, append = false) => {
        const query = input.value.trim();
        if (query.length < minimumQueryLength) {
            clearResults();
            status.textContent = "Digite pelo menos dois caracteres para pesquisar.";
            return;
        }

        if (!append) {
            window.clearTimeout(timer);
            requestController?.abort();
            requestController = null;
            clearResults();
            status.textContent = "Pesquisando clientes autorizados…";
        }

        const thisRequest = ++requestNumber;
        const controller = new AbortController();
        requestController = controller;
        const url = new URL(root.dataset.searchUrl, window.location.origin);
        url.searchParams.set("q", query);
        url.searchParams.set("page", String(requestedPage));

        try {
            const response = await fetch(url, {
                signal: controller.signal,
                credentials: "same-origin",
                headers: { Accept: "application/json" }
            });
            if (!response.ok) throw new Error("Customer search failed");
            const pageResult = await response.json();
            if (thisRequest !== requestNumber || controller.signal.aborted || query !== input.value.trim()) return;
            requestController = null;
            render(pageResult, append);
        } catch (error) {
            if (error.name === "AbortError" || thisRequest !== requestNumber) return;
            requestController = null;
            status.textContent = "Não foi possível pesquisar agora. Tente novamente.";
        }
    };

    const setActiveOption = index => {
        if (!items.length) return;
        activeIndex = Math.max(0, Math.min(index, items.length - 1));
        results.querySelectorAll("[data-customer-id]").forEach((option, optionIndex) =>
            option.setAttribute("aria-selected", String(optionIndex === activeIndex)));
        input.setAttribute("aria-activedescendant", `assisted-customer-option-${items[activeIndex].id}`);
    };

    input.setAttribute("aria-expanded", "false");
    input.addEventListener("input", () => {
        selectFirstAfterSearch = false;
        window.clearTimeout(timer);
        requestController?.abort();
        requestController = null;
        requestNumber++;
        clearResults();
        if (input.value.trim().length < minimumQueryLength) {
            status.textContent = "Digite pelo menos dois caracteres para pesquisar.";
            return;
        }
        status.textContent = "Aguardando para pesquisar…";
        timer = window.setTimeout(() => void searchPage(1), 250);
    });

    input.addEventListener("keydown", event => {
        if (event.key === "ArrowDown" && items.length) {
            event.preventDefault();
            setActiveOption(activeIndex < 0 ? 0 : activeIndex + 1);
        } else if (event.key === "ArrowUp" && items.length) {
            event.preventDefault();
            setActiveOption(activeIndex < 0 ? 0 : activeIndex - 1);
        } else if (event.key === "Enter") {
            if (items.length) {
                event.preventDefault();
                selectCustomer(items[activeIndex < 0 ? 0 : activeIndex].id);
            } else if (input.value.trim().length >= minimumQueryLength) {
                event.preventDefault();
                selectFirstAfterSearch = true;
                void searchPage(1);
            }
        } else if (event.key === "Escape") {
            window.clearTimeout(timer);
            requestController?.abort();
            requestController = null;
            selectFirstAfterSearch = false;
            clearResults();
            status.textContent = "Sugestões fechadas.";
        }
    });

    results.addEventListener("click", event => {
        const option = event.target.closest("[data-customer-id]");
        if (!option || !results.contains(option)) return;
        selectCustomer(option.dataset.customerId);
    });

    moreButton.addEventListener("click", () => {
        if (hasMore) void searchPage(page + 1, true);
    });

    if (root.dataset.focusProductSearch === "true") {
        const productSearch = document.querySelector("#assisted-product-search");
        if (productSearch) productSearch.focus();
    } else if (!root.dataset.selectedCustomerId || root.dataset.selectedCustomerId === "0") {
        input.focus();
    }
})();
