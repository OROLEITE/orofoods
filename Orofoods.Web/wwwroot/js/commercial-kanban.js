(() => {
    const board = document.querySelector("[data-commercial-kanban]");
    const statusForm = document.querySelector("#commercial-kanban-status-form");
    const feedback = board?.querySelector("[data-kanban-feedback]");
    const supportsMouseDrag = window.matchMedia("(hover: hover) and (pointer: fine)").matches;

    if (!board || !statusForm || !feedback || !supportsMouseDrag) return;

    const allowedStatuses = new Set(["0", "1", "2"]);
    const pendingCardIds = new Set();
    let draggedCard = null;

    board.querySelectorAll(".commercial-activity-card[data-activity-id]").forEach(card => {
        card.draggable = true;
        card.style.cursor = "grab";
    });

    function showFeedback(message, isError) {
        feedback.textContent = message;
        feedback.hidden = false;
        feedback.classList.toggle("is-error", isError);
    }

    function updateColumn(column) {
        if (!column) return;

        const cardsContainer = column.querySelector("[data-kanban-cards]");
        const cards = cardsContainer.querySelectorAll(":scope > .commercial-activity-card");
        const count = column.querySelector("[data-status-count]");
        let emptyState = cardsContainer.querySelector("[data-kanban-empty]");

        count.textContent = String(cards.length);
        if (cards.length === 0 && !emptyState) {
            emptyState = document.createElement("p");
            emptyState.className = "commercial-empty-column";
            emptyState.dataset.kanbanEmpty = "";
            emptyState.textContent = "Nenhum atendimento";
            cardsContainer.appendChild(emptyState);
        } else if (cards.length > 0 && emptyState) {
            emptyState.remove();
        }
    }

    function restoreCardPosition(card, originContainer, originNextSibling, originStatus) {
        if (originNextSibling && originNextSibling.parentElement === originContainer) {
            originContainer.insertBefore(card, originNextSibling);
        } else {
            originContainer.appendChild(card);
        }

        card.dataset.currentStatus = originStatus;
        updateColumn(originContainer.closest("[data-kanban-column]"));
    }

    async function moveCard(card, targetColumn) {
        const targetStatus = targetColumn.dataset.status;
        const originStatus = card.dataset.currentStatus;
        const originContainer = card.closest("[data-kanban-cards]");
        const targetContainer = targetColumn.querySelector("[data-kanban-cards]");
        const originNextSibling = card.nextElementSibling;
        const cardId = card.dataset.activityId;

        if (!allowedStatuses.has(targetStatus) || targetStatus === originStatus || pendingCardIds.has(cardId)) return;

        const originColumn = originContainer.closest("[data-kanban-column]");
        pendingCardIds.add(cardId);
        card.dataset.statusUpdatePending = "true";
        card.draggable = false;
        card.classList.add("is-status-updating");
        targetContainer.appendChild(card);
        updateColumn(originColumn);
        updateColumn(targetColumn);

        const body = new URLSearchParams();
        new FormData(statusForm).forEach((value, key) => {
            if (typeof value === "string") body.append(key, value);
        });
        body.set("id", cardId);
        body.set("customerId", card.dataset.customerId);
        body.set("status", targetStatus);
        body.set("asJson", "true");

        try {
            const response = await fetch(statusForm.action, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body: body.toString()
            });
            const result = await response.json().catch(() => null);
            if (!response.ok || result?.success !== true) throw new Error("Status update rejected");

            card.dataset.currentStatus = targetStatus;
            showFeedback(`Atendimento movido para ${targetColumn.dataset.statusLabel}.`, false);
        } catch {
            restoreCardPosition(card, originContainer, originNextSibling, originStatus);
            updateColumn(targetColumn);
            showFeedback("Não foi possível atualizar o atendimento. O card voltou à coluna original.", true);
        } finally {
            pendingCardIds.delete(cardId);
            delete card.dataset.statusUpdatePending;
            card.classList.remove("is-status-updating");
            card.draggable = true;
            card.style.cursor = "grab";
            updateColumn(card.closest("[data-kanban-column]"));
        }
    }

    board.addEventListener("dragstart", event => {
        const card = event.target instanceof Element ? event.target.closest(".commercial-activity-card[data-activity-id]") : null;
        if (!card || pendingCardIds.has(card.dataset.activityId)) {
            event.preventDefault();
            return;
        }

        draggedCard = card;
        card.classList.add("is-dragging");
        card.style.cursor = "grabbing";
        if (event.dataTransfer) {
            event.dataTransfer.effectAllowed = "move";
            event.dataTransfer.setData("text/plain", card.dataset.activityId);
        }
    });

    board.addEventListener("dragover", event => {
        const column = event.target instanceof Element ? event.target.closest("[data-kanban-column]") : null;
        if (!draggedCard || !column || !allowedStatuses.has(column.dataset.status)) return;

        event.preventDefault();
        if (event.dataTransfer) event.dataTransfer.dropEffect = "move";
        column.classList.add("is-drop-target");
    });

    board.addEventListener("dragleave", event => {
        const column = event.target instanceof Element ? event.target.closest("[data-kanban-column]") : null;
        if (column && !column.contains(event.relatedTarget)) column.classList.remove("is-drop-target");
    });

    board.addEventListener("drop", event => {
        const column = event.target instanceof Element ? event.target.closest("[data-kanban-column]") : null;
        board.querySelectorAll(".commercial-kanban-column.is-drop-target").forEach(item => item.classList.remove("is-drop-target"));
        if (!draggedCard || !column || !allowedStatuses.has(column.dataset.status)) return;

        event.preventDefault();
        if (event.dataTransfer?.getData("text/plain") !== draggedCard.dataset.activityId) return;

        const card = draggedCard;
        draggedCard = null;
        return moveCard(card, column);
    });

    board.addEventListener("dragend", event => {
        const card = event.target instanceof Element ? event.target.closest(".commercial-activity-card[data-activity-id]") : null;
        card?.classList.remove("is-dragging");
        if (card && !pendingCardIds.has(card.dataset.activityId)) card.style.cursor = "grab";
        board.querySelectorAll(".commercial-kanban-column.is-drop-target").forEach(column => column.classList.remove("is-drop-target"));
        if (draggedCard === card) draggedCard = null;
    });
})();
