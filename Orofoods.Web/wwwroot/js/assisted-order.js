(() => {
  const lines = new Map();
  const list = document.querySelector('#assisted-order-lines');
  const summary = document.querySelector('#assisted-summary-items');
  const total = document.querySelector('#assisted-total');
  const minimumHost = document.querySelector('#assisted-minimum-order');
  const minimumGap = document.querySelector('#assisted-minimum-gap');
  const minimumOrder = Number(minimumHost?.dataset.minimumOrder) || 0;
  const form = document.querySelector('.assisted-order-form');
  const clientError = document.querySelector('#assisted-client-error');
  const money = value => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  function render() {
    list.innerHTML = '';
    summary.innerHTML = '';
    let sum = 0;
    if (!lines.size) list.innerHTML = '<p class="text-muted">Nenhum produto adicionado.</p>';
    for (const [id, item] of lines) {
      const subtotal = item.quantity * item.price; sum += subtotal;
      const row = document.createElement('div'); row.className = 'assisted-order-line';
      row.innerHTML = `<span>${item.name}</span><input type="number" min="1" value="${item.quantity}" data-line-id="${id}" /><strong>${money(subtotal)}</strong><button type="button" data-remove-id="${id}" aria-label="Remover ${item.name}">&times;</button>`;
      list.append(row);
      const summaryRow = document.createElement('div'); summaryRow.innerHTML = `<span>${item.quantity} x ${item.name}</span><strong>${money(subtotal)}</strong>`; summary.append(summaryRow);
    }
    total.textContent = money(sum);
    if (minimumGap) minimumGap.textContent = sum < minimumOrder
      ? `Faltam ${money(minimumOrder - sum)} para atingir o mínimo.`
      : 'Pedido mínimo atingido.';
    form.querySelectorAll('input[name="ProductIds"],input[name="Quantities"]').forEach(x => x.remove());
    for (const item of lines.values()) { const id = document.createElement('input'); id.type = 'hidden'; id.name = 'ProductIds'; id.value = item.id; const quantity = document.createElement('input'); quantity.type = 'hidden'; quantity.name = 'Quantities'; quantity.value = item.quantity; form.append(id, quantity); }
  }
  document.querySelectorAll('.assisted-add-product').forEach(button => button.addEventListener('click', () => { const card = button.closest('[data-product-id]'); const id = Number(card.dataset.productId); const quantity = Number(card.querySelector('input').value); lines.set(id, { id, name: card.dataset.productName, price: Number(card.dataset.productPrice), quantity }); render(); }));
  list.addEventListener('input', event => { const id = Number(event.target.dataset.lineId); if (lines.has(id)) { lines.get(id).quantity = Math.max(1, Number(event.target.value) || 1); render(); } });
  list.addEventListener('click', event => { const id = Number(event.target.dataset.removeId); if (id) { lines.delete(id); render(); } });
  const postedProductIds = [...form.querySelectorAll('input[name="ProductIds"]')];
  const postedQuantities = [...form.querySelectorAll('input[name="Quantities"]')];
  postedProductIds.forEach((input, index) => {
    const id = Number(input.value);
    const quantity = Number(postedQuantities[index]?.value);
    const card = document.querySelector(`[data-product-id="${id}"]`);
    if (id > 0 && quantity > 0 && card) lines.set(id, { id, name: card.dataset.productName, price: Number(card.dataset.productPrice), quantity });
  });
  form.addEventListener('submit', event => { if (!lines.size) { event.preventDefault(); if (clientError) { clientError.hidden = false; clientError.textContent = 'Adicione ao menos um produto antes de confirmar o pedido.'; clientError.focus(); } return; } const button = document.querySelector('#assisted-submit'); button.disabled = true; button.textContent = 'Criando pedido...'; });
  render();
})();
