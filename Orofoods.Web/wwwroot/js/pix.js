(() => {
    const copyButton = document.getElementById("pixCopyButton");
    const copyInput = document.getElementById("pixCopyPaste");
    const feedback = document.getElementById("pixCopyFeedback");

    if (!copyButton || !copyInput) return;

    copyButton.addEventListener("click", async () => {
        const code = copyInput.value;
        try {
            if (navigator.clipboard?.writeText) {
                await navigator.clipboard.writeText(code);
            } else {
                copyInput.select();
                document.execCommand("copy");
            }
            if (feedback) feedback.textContent = "Código PIX copiado!";
        } catch {
            if (feedback) feedback.textContent = "Não foi possível copiar o código. Selecione e copie manualmente.";
        }
    });
})();
