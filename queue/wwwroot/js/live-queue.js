(() => {
  const display = document.querySelector('[data-live-display]');
  const tickets = document.querySelector('[data-live-tickets]');
  if (!display && !tickets) return;
  const status = document.querySelector('[data-live-status]');
  const endpoint = display ? '/Home/LiveQueue' : '/Queue/LiveTickets' + location.search;
  let timer, busy = false, stopped = false;
  const element = (tag, text, className) => {
    const node = document.createElement(tag); node.textContent = text || '';
    if (className) node.className = className;
    return node;
  };
  function renderDisplay(data) {
    const serving = document.querySelector('[data-live-serving]');
    const waiting = document.querySelector('[data-live-waiting]');
    serving.replaceChildren(); waiting.replaceChildren();
    for (const ticket of data.serving) {
      const card = element('article', '', 'serving-box');
      card.append(element('span', `${ticket.office} · ${ticket.counterName}`),
        element('strong', ticket.ticketNumber), element('em', ticket.serviceType), element('p', ticket.requestType));
      serving.append(card);
    }
    if (!data.serving.length) {
      const card = element('div', '', 'serving-box');
      card.append(element('span', 'Now serving'), element('strong', '—'), element('em', 'Waiting for the next transaction'));
      serving.append(card);
    }
    for (const ticket of data.waiting) {
      const row = element('div', '', 'next-ticket'), detail = element('div');
      detail.append(element('strong', ticket.ticketNumber), element('span', `${ticket.office} · ${ticket.serviceType}`));
      row.append(detail, element('em', ticket.predictedCounterName)); waiting.append(row);
    }
    if (!data.waiting.length) waiting.append(element('p', 'No waiting transactions.', 'muted-light'));
  }
  function renderTickets(data) {
    for (const card of tickets.querySelectorAll('[data-live-ticket]')) {
      const ticket = data.find(t => t.ticketNumber === card.dataset.liveTicket);
      if (!ticket) { card.hidden = true; continue; }
      card.hidden = false;
      const pill = card.querySelector('[data-ticket-status]');
      pill.textContent = `${ticket.status} • ${ticket.estimatedWaitText}`;
      // Only fixed status values may become CSS classes.
      const css = {Waiting:'waiting', Serving:'serving', Completed:'completed', Skipped:'skipped', Cancelled:'cancelled', Expired:'expired'};
      pill.className = 'pill status-' + (css[ticket.status] || 'waiting');
      card.querySelector('[data-ticket-counter]').textContent = ticket.counter || 'Waiting';
      card.querySelector('[data-ticket-call]').textContent = ticket.estimatedCall;
    }
  }
  async function poll() {
    clearTimeout(timer);
    if (busy || stopped) return;
    if (document.hidden || !navigator.onLine) {
      status.textContent = navigator.onLine ? 'Updates paused while this tab is hidden.' : 'Offline · showing the last received update.';
      timer = setTimeout(poll, 5000); return;
    }
    busy = true;
    try {
      const response = await fetch(endpoint, {cache:'no-store', signal:AbortSignal.timeout(10000)});
      if (response.status === 401) { stopped = true; throw new Error('session'); }
      if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw new Error('response');
      const data = await response.json();
      if (display) renderDisplay(data); else renderTickets(data);
      status.textContent = 'Live · updated ' + new Date().toLocaleTimeString([], {hour:'2-digit', minute:'2-digit', second:'2-digit'});
    } catch {
      status.textContent = stopped ? 'Session ended. Sign in again for live updates.' : 'Connection interrupted · retrying. Showing the last received update.';
    } finally { busy = false; if (!stopped) timer = setTimeout(poll, 5000); }
  }
  document.addEventListener('visibilitychange', poll);
  window.addEventListener('online', poll);
  window.addEventListener('offline', poll);
  poll();
})();
