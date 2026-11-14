(() => {
  document.querySelectorAll('[data-confirm]').forEach(form => form.addEventListener('submit', event => {
    if (!window.confirm(form.dataset.confirm)) event.preventDefault();
  }));
  document.querySelectorAll('[data-use-image]').forEach(button => button.addEventListener('click', () => {
    button.closest('form').querySelector('[data-main-image]').value = button.dataset.useImage;
  }));
  const rub = value => new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value) + ' ₽';
  const cents = value => Math.round(value * 100);
  const ratio = (num, den) => Math.floor((num + den / 2) / den);
  function calculate(c, p, surcharge = 0) {
    const lengthCm = Math.round(c.length * 100) + (c.shape !== 'straight' ? Math.round(c.width * 100) - 60 : 0) + (c.shape === 'u' ? Math.round(c.returnLength * 100) - 60 : 0);
    const lines = [];
    Object.entries(p.categories).forEach(([key, category]) => {
      const option = category.options[c.materials[key]];
      if (!option) return;
      const price = cents(option.price);
      let amount = ratio(price, 100);
      if (category.unit === 'cabinet') amount = ratio(lengthCm * price * c.height * (c.upper ? 100 : 65), 240000000);
      else if (category.unit === 'countertop') amount = ratio(lengthCm * price * c.depth, 600000);
      else if (category.unit === 'meter') amount = ratio(lengthCm * price, 10000);
      lines.push({label: category.name + ' · ' + option.label, amount});
    });
    Object.entries(p.extras).forEach(([key, option]) => {
      if (!c.extras[key]) return;
      const amount = option.unit === 'meter' ? ratio(cents(option.price) * lengthCm, 10000) : ratio(cents(option.price), 100);
      lines.push({label: option.label, amount});
    });
    if (surcharge > 0) lines.push({ label: 'Особенности модели', amount: ratio(cents(surcharge), 100) });
    const subtotal = lines.reduce((sum, line) => sum + line.amount, 0);
    if (c.installation) lines.push({ label: 'Монтаж · ' + p.installation + '%', amount: ratio(subtotal * cents(p.installation), 10000) });
    if (c.delivery) lines.push({ label: 'Доставка', amount: ratio(cents(p.delivery), 100) });
    return { length: lengthCm / 100, lines, total: lines.reduce((sum, line) => sum + line.amount, 0) };
  }
  window.KitchenQuote = { calculate };

  document.querySelectorAll('[data-calculator]').forEach(root => {
    const {pricing, configuration, surcharge} = JSON.parse(root.querySelector('[data-calculator-data]').textContent);
    function update() {
      const c = {...configuration, materials: {}, extras: {}};
      root.querySelectorAll('[data-field]').forEach(input => {
        c[input.dataset.field] = input.type === 'checkbox' ? Number(input.checked) : input.type === 'number' ? Number(input.value) : input.value;
      });
      root.querySelectorAll('[data-material]').forEach(input => {c.materials[input.dataset.material] = input.value;});
      root.querySelectorAll('[data-extra]').forEach(input => {c.extras[input.dataset.extra] = input.type === 'checkbox' ? Number(input.checked) : Number(input.value);});
      root.querySelectorAll('[data-dimension]').forEach(label => {
        const visible = label.dataset.dimension === 'width' ? c.shape !== 'straight' : c.shape === 'u';
        label.hidden = !visible; label.querySelector('input').disabled = !visible;
      });
      const valid = [...root.querySelectorAll('[data-field],[data-material],[data-extra]')].every(input => input.disabled || input.checkValidity());
      if (!valid) {root.querySelector('[data-quote-total]').textContent = 'Проверьте размеры'; return;}
      const quote = calculate(c, pricing, surcharge);
      root.querySelector('[data-quote-total]').textContent = rub(quote.total);
      root.querySelector('[data-quote-dimensions]').textContent = ({straight:'Прямая',l:'Угловая',u:'П-образная'}[c.shape])+' · '+quote.length.toLocaleString('ru-RU')+' пог. м';
      root.querySelector('[data-quote-lines]').replaceChildren(...quote.lines.map(line => {
        const row=document.createElement('div'), label=document.createElement('span'), price=document.createElement('strong');
        label.textContent=line.label;price.textContent=rub(line.amount);row.append(label,price);return row;
      }));
    }
    root.querySelectorAll('[data-field],[data-material],[data-extra]').forEach(input => input.addEventListener('input', update));
    update();
  });
})();
