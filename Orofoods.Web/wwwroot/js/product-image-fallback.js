document.addEventListener("error", (event) => {
    const image = event.target;
    if (!(image instanceof HTMLImageElement) || image.dataset.productImage !== "true") {
        return;
    }

    if (image.dataset.fallbackStage === "manual" && image.dataset.catalogFallback) {
        image.dataset.fallbackStage = "catalog";
        image.src = image.dataset.catalogFallback;
        return;
    }

    if (image.dataset.fallbackStage !== "placeholder") {
        image.dataset.fallbackStage = "placeholder";
        image.src = image.dataset.placeholderSrc;
        return;
    }

    image.onerror = null;
    image.hidden = true;
}, true);
