(() => {
    const paymentTermSelect = document.getElementById("paymentTermSelect");
    const cardForm = document.getElementById("creditCardForm");
    const checkoutForm = document.getElementById("checkoutForm");
    const cardNumber = document.getElementById("cardNumberInput");
    const cardHolder = document.getElementById("cardHolderInput");
    const cardExpiry = document.getElementById("cardExpiryInput");
    const cardCvv = document.getElementById("cardCvvInput");
    const numberPreview = document.getElementById("cardNumberPreview");
    const holderPreview = document.getElementById("cardHolderPreview");
    const expiryPreview = document.getElementById("cardExpiryPreview");
    const tokenError = document.getElementById("cardTokenError");
    const cardUnavailableNotice = document.getElementById("cardUnavailableNotice");
    const installmentsSelect = document.getElementById("cardInstallmentsSelect");
    const installmentsHint = document.getElementById("cardInstallmentsHint");
    const cardTokenField = document.getElementById("CardToken");
    const cardPaymentMethodField = document.getElementById("CardPaymentMethodId");
    const cardInstallmentsField = document.getElementById("CardInstallments");
    const submitButton = checkoutForm?.querySelector("button[type=submit]");
    const cartTotal = Number.parseFloat(checkoutForm?.dataset.cartTotal ?? "0") || 0;

    if (!checkoutForm) return;

    const isCreditCardSelected = () => {
        if (!paymentTermSelect || !cardForm) return false;
        const selected = paymentTermSelect.options[paymentTermSelect.selectedIndex];
        return selected?.dataset.code === "CREDIT_CARD";
    };

    // Card tokenization uses the official Mercado Pago SDK V2 in the browser (Checkout Transparente).
    // Only the resulting token and payment-method id are sent to the server; the raw card
    // number/CVV never leave the browser and are never submitted with the form.
    const mercadoPago = window.MercadoPago && window.__mercadoPagoPublicKey
        ? new window.MercadoPago(window.__mercadoPagoPublicKey, { locale: "pt-BR" })
        : null;

    const updateVisibility = () => {
        cardForm.hidden = !isCreditCardSelected();
        if (cardUnavailableNotice) cardUnavailableNotice.hidden = !(isCreditCardSelected() && !mercadoPago);
    };

    // Installment options come exclusively from Mercado Pago's official getInstallments API;
    // the number of installments and any interest are never hardcoded or guessed by Orofoods.
    const resetInstallments = () => {
        if (!installmentsSelect) return;
        installmentsSelect.innerHTML = '<option value="1">1x sem juros</option>';
        installmentsSelect.disabled = true;
    };
    let installmentsRequestId = 0;
    const updateInstallments = async (digits) => {
        if (!installmentsSelect) return;
        if (!mercadoPago || digits.length < 6 || cartTotal <= 0) {
            resetInstallments();
            if (installmentsHint) installmentsHint.textContent = "Informe o número do cartão para ver as opções de parcelamento.";
            return;
        }

        const requestId = ++installmentsRequestId;
        try {
            const options = await mercadoPago.getInstallments({ amount: String(cartTotal), bin: digits.slice(0, 6), paymentTypeId: "credit_card" });
            if (requestId !== installmentsRequestId) return; // a newer request superseded this one.

            const payerCosts = options?.[0]?.payer_costs ?? [];
            if (!payerCosts.length) {
                resetInstallments();
                if (installmentsHint) installmentsHint.textContent = "Parcelamento indisponível para este cartão; será cobrado em 1x.";
                return;
            }

            installmentsSelect.innerHTML = payerCosts
                .map((cost) => `<option value="${cost.installments}">${cost.recommended_message ?? `${cost.installments}x`}</option>`)
                .join("");
            installmentsSelect.disabled = false;
            if (installmentsHint) installmentsHint.textContent = "";
        } catch {
            if (requestId !== installmentsRequestId) return;
            resetInstallments();
            if (installmentsHint) installmentsHint.textContent = "Não foi possível consultar as parcelas; será cobrado em 1x.";
        }
    };

    const updateNumber = () => {
        const digits = (cardNumber?.value ?? "").replace(/\D/g, "").slice(0, 19);
        if (cardNumber) cardNumber.value = digits.replace(/(.{4})/g, "$1 ").trim();
        if (numberPreview) numberPreview.textContent = digits ? digits.padEnd(16, "•").replace(/(.{4})/g, "$1 ").trim() : "•••• •••• •••• ••••";
        updateInstallments(digits);
    };
    const updateExpiry = () => {
        const digits = (cardExpiry?.value ?? "").replace(/\D/g, "").slice(0, 4);
        if (cardExpiry) cardExpiry.value = digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits;
        if (expiryPreview) expiryPreview.textContent = cardExpiry?.value || "MM/AA";
    };

    if (paymentTermSelect && cardForm) {
        paymentTermSelect.addEventListener("change", updateVisibility);
        cardNumber?.addEventListener("input", updateNumber);
        cardHolder?.addEventListener("input", () => { if (holderPreview) holderPreview.textContent = cardHolder.value.trim().toUpperCase() || "SEU NOME"; });
        cardExpiry?.addEventListener("input", updateExpiry);
        updateVisibility();
        updateNumber();
        updateExpiry();
    }

    let submissionInProgress = false;
    const originalButtonLabel = submitButton?.textContent ?? "";
    const setSubmittingState = () => {
        submissionInProgress = true;
        if (!submitButton) return;
        submitButton.disabled = true;
        submitButton.textContent = submitButton.dataset.processingLabel || "Enviando pedido…";
        submitButton.setAttribute("aria-busy", "true");
    };
    const clearSubmittingState = () => {
        submissionInProgress = false;
        if (!submitButton) return;
        submitButton.disabled = false;
        submitButton.textContent = originalButtonLabel;
        submitButton.removeAttribute("aria-busy");
    };

    checkoutForm.addEventListener("submit", async (event) => {
        if (submissionInProgress) {
            event.preventDefault();
            return;
        }

        if (!isCreditCardSelected()) {
            setSubmittingState();
            return;
        }

        event.preventDefault();
        if (tokenError) tokenError.textContent = "";

        if (!mercadoPago) {
            if (tokenError) tokenError.textContent = "O pagamento por cartão está indisponível no momento.";
            return;
        }

        const digits = (cardNumber?.value ?? "").replace(/\D/g, "");
        const expiryDigits = (cardExpiry?.value ?? "").replace(/\D/g, "");
        if (digits.length < 13 || expiryDigits.length !== 4 || !cardHolder?.value || !cardCvv?.value) {
            if (tokenError) tokenError.textContent = "Preencha todos os dados do cartão.";
            return;
        }

        setSubmittingState();

        try {
            const paymentMethods = await mercadoPago.getPaymentMethods({ bin: digits.slice(0, 6) });
            const paymentMethodId = paymentMethods?.results?.[0]?.id;
            if (!paymentMethodId) {
                if (tokenError) tokenError.textContent = "Não foi possível identificar a bandeira do cartão.";
                clearSubmittingState();
                return;
            }

            const token = await mercadoPago.createCardToken({
                cardNumber: digits,
                cardholderName: cardHolder.value.trim(),
                cardExpirationMonth: expiryDigits.slice(0, 2),
                cardExpirationYear: `20${expiryDigits.slice(2)}`,
                securityCode: cardCvv.value.trim()
            });

            if (!token?.id) {
                if (tokenError) tokenError.textContent = "Não foi possível validar o cartão. Verifique os dados.";
                clearSubmittingState();
                return;
            }

            if (cardTokenField) cardTokenField.value = token.id;
            if (cardPaymentMethodField) cardPaymentMethodField.value = paymentMethodId;
            if (cardInstallmentsField) cardInstallmentsField.value = installmentsSelect?.value || "1";
            checkoutForm.submit();
        } catch {
            if (tokenError) tokenError.textContent = "Não foi possível validar o cartão. Verifique os dados.";
            clearSubmittingState();
        }
    });
})();
