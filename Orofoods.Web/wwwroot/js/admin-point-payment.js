(() => {
  const form = document.querySelector('[data-point-payment-poll]');
  if (!form) return;

  const message = form.querySelector('[data-point-poll-message]');
  const statusElement = document.querySelector('[data-point-status-label]');
  const statusContainer = document.querySelector('[data-point-payment-status]');
  const startedAt = Date.now();
  const deadline = startedAt + 60_000;
  let stopped = false;

  const finish = (text) => {
    stopped = true;
    if (message) {
      message.classList.remove('visually-hidden');
      message.textContent = text;
    }
  };

  const poll = async () => {
    if (stopped) return;
    if (Date.now() >= deadline) {
      finish('O terminal ainda não confirmou um novo status. Consulte novamente antes de tentar outra cobrança.');
      return;
    }

    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), 8_000);
    try {
      const response = await fetch(form.action, {
        method: 'POST',
        body: new URLSearchParams(new FormData(form)),
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
        credentials: 'same-origin',
        cache: 'no-store',
        signal: controller.signal
      });
      if (!response.ok) throw new Error('poll-failed');
      const result = await response.json();
      if (result.label && statusElement) statusElement.textContent = result.label;
      if (result.status && statusContainer) statusContainer.dataset.pointPaymentStatus = result.status.toLowerCase();
      if (result.message && message) {
        message.classList.remove('visually-hidden');
        message.textContent = result.message;
      }
      if (result.terminal) {
        finish(result.label || 'Status final recebido.');
        window.setTimeout(() => window.location.reload(), 500);
        return;
      }
    } catch {
      if (message) {
        message.classList.remove('visually-hidden');
        message.textContent = 'Não foi possível atualizar agora. A tentativa foi preservada; a consulta automática continuará.';
      }
    } finally {
      window.clearTimeout(timeout);
    }

    window.setTimeout(poll, 5_000);
  };

  window.setTimeout(poll, 5_000);
})();
