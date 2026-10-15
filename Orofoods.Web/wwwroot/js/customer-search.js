(() => {
    const normalize = value => value.trim().toLocaleLowerCase();

    document.querySelectorAll("[data-customer-search]").forEach(root => {
        const input = root.querySelector("[data-customer-search-input]");
        const results = root.querySelector("[data-customer-search-results]");
        const status = root.querySelector("[data-customer-search-status]");
        const selectedId = root.querySelector("[data-customer-search-selected-id]");
        const submit = root.querySelector("[data-customer-search-submit]");
        const mode = root.dataset.searchMode;
        if (!input || !results || !status || !mode) return;

        let timer = 0;
        let requestNumber = 0;
        let requestController = null;
        let scheduledQuery = null;
        let pendingQuery = null;
        let renderedQuery = null;
        let currentQuery = normalize(input.value);

        const setStatus = message => { status.textContent = message; };
        const clearSelection = () => {
            if (selectedId) selectedId.value = "";
            if (submit) submit.disabled = true;
        };
        const clearPendingSearch = () => {
            window.clearTimeout(timer);
            requestController?.abort();
            requestController = null;
            scheduledQuery = null;
            pendingQuery = null;
            requestNumber++;
        };

        const appendSuggestionText = (item, target) => {
            const name = document.createElement("strong");
            name.textContent = item.tradeName || item.legalName || "Cliente";
            target.append(name);

            if (item.legalName && item.legalName !== item.tradeName) {
                const legalName = document.createElement("span");
                legalName.className = "customer-search__muted";
                legalName.textContent = item.legalName;
                target.append(legalName);
            }

            const details = [item.cnpj, item.wmcCode, item.city].filter(Boolean).join(" · ");
            if (details) {
                const small = document.createElement("small");
                small.textContent = details;
                target.append(small);
            }
        };

        const createSuggestion = item => {
            const element = mode === "select" ? document.createElement("button") : document.createElement("a");
            if (mode === "select") {
                element.type = "button";
                element.className = "customer-search__option";
                element.setAttribute("role", "option");
                element.setAttribute("aria-selected", "false");
            } else {
                element.className = mode === "cards" ? "customer-search__card" : "customer-search__option";
                element.setAttribute("role", "option");
                const template = root.dataset.linkUrlTemplate || "#";
                element.href = template.replace("__id__", encodeURIComponent(item.id));
            }

            element.dataset.customerSearchItem = "";
            element.dataset.customerId = String(item.id);
            appendSuggestionText(item, element);
            return element;
        };

        const renderResults = items => {
            results.replaceChildren();
            items.slice(0, 20).forEach(item => results.append(createSuggestion(item)));
            return items.length;
        };

        const runSearch = async query => {
            if (query.length < 1 || query === renderedQuery || query === pendingQuery) return;
            clearPendingSearch();
            const thisRequest = requestNumber;
            const controller = new AbortController();
            requestController = controller;
            pendingQuery = query;
            setStatus("Pesquisando clientes autorizados…");

            const url = new URL(root.dataset.searchUrl, window.location.origin);
            url.searchParams.set("q", input.value.trim());
            try {
                const response = await fetch(url, {
                    signal: controller.signal,
                    credentials: "same-origin",
                    headers: { Accept: "application/json" }
                });
                if (!response.ok) throw new Error("Customer search failed");
                const suggestions = await response.json();
                if (thisRequest !== requestNumber || query !== currentQuery || controller.signal.aborted) return;

                const count = renderResults(Array.isArray(suggestions) ? suggestions : []);
                renderedQuery = query;
                pendingQuery = null;
                requestController = null;
                setStatus(count ? `${count} cliente(s) encontrado(s).` : "Nenhum cliente autorizado encontrado.");
            } catch (error) {
                if (error.name === "AbortError" || thisRequest !== requestNumber) return;
                pendingQuery = null;
                requestController = null;
                setStatus("Não foi possível pesquisar agora. Tente novamente.");
            }
        };

        const scheduleSearch = immediate => {
            const query = normalize(input.value);
            if (query.length < 1) {
                clearPendingSearch();
                currentQuery = "";
                renderedQuery = null;
                results.replaceChildren();
                clearSelection();
                setStatus("Digite pelo menos um caractere para pesquisar.");
                return;
            }

            if (query === currentQuery && (query === renderedQuery || query === pendingQuery || query === scheduledQuery)) return;
            currentQuery = query;
            clearSelection();
            clearPendingSearch();
            renderedQuery = null;
            results.replaceChildren();
            if (immediate) {
                void runSearch(query);
            } else {
                scheduledQuery = query;
                setStatus("Aguardando para pesquisar…");
                timer = window.setTimeout(() => {
                    scheduledQuery = null;
                    void runSearch(query);
                }, 300);
            }
        };

        const serverRenderedQuery = normalize(root.dataset.serverRenderedQuery || "");
        if (serverRenderedQuery && serverRenderedQuery === currentQuery) {
            renderedQuery = currentQuery;
        } else if (!currentQuery) {
            setStatus("Digite pelo menos um caractere para pesquisar.");
        } else {
            currentQuery = "";
            scheduleSearch(false);
        }

        input.addEventListener("input", () => scheduleSearch(false));
        input.addEventListener("keydown", event => {
            if (event.key === "ArrowDown") {
                const first = results.querySelector("[data-customer-search-item]");
                if (first) {
                    event.preventDefault();
                    first.focus();
                } else if (currentQuery) {
                    event.preventDefault();
                    void runSearch(currentQuery);
                }
            } else if (event.key === "Escape") {
                clearPendingSearch();
                renderedQuery = null;
                results.replaceChildren();
                clearSelection();
                setStatus("Sugestões fechadas.");
                input.focus();
            }
        });

        results.addEventListener("click", event => {
            if (mode !== "select") return;
            const option = event.target.closest("[data-customer-search-item]");
            if (!option || !results.contains(option)) return;
            selectedId.value = option.dataset.customerId;
            submit.disabled = false;
            results.querySelectorAll("[data-customer-search-item]").forEach(item =>
                item.setAttribute("aria-selected", String(item === option)));
            setStatus(`${option.querySelector("strong")?.textContent || "Cliente"} selecionado. Você pode abrir o portal.`);
        });

        results.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                clearPendingSearch();
                renderedQuery = null;
                results.replaceChildren();
                clearSelection();
                setStatus("Sugestões fechadas.");
                input.focus();
                return;
            }
            if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
            const items = Array.from(results.querySelectorAll("[data-customer-search-item]"));
            const index = items.indexOf(event.target.closest("[data-customer-search-item]"));
            const next = event.key === "ArrowDown" ? Math.min(index + 1, items.length - 1) : Math.max(index - 1, 0);
            if (items[next]) {
                event.preventDefault();
                items[next].focus();
            }
        });

        const form = root.matches("form") ? root : root.querySelector("form");
        if (form && mode !== "select") {
            form.addEventListener("submit", event => {
                event.preventDefault();
                window.clearTimeout(timer);
                scheduledQuery = null;
                void runSearch(normalize(input.value));
            });
        }
    });
})();
