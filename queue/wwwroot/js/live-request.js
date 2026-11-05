(() => {
  const root = document.querySelector('[data-live-request]'); if (!root) return;
  root.querySelectorAll('[data-review-decision]').forEach(select => {
    const note=select.closest('form').querySelector('[data-review-note]');
    select.addEventListener('change',()=>{note.required=select.value==='return';note.minLength=select.value==='return'?5:0;});
  });
  root.querySelectorAll('.submission-upload').forEach(form=>form.addEventListener('submit',event=>{
    const input=form.querySelector('input[type="file"]');
    if(input.files[0]?.size>2*1024*1024){event.preventDefault();input.setCustomValidity('Choose a file of up to 2 MB.');input.reportValidity();const button=form.querySelector('button');button?.classList.remove('is-loading');}
  }));
  root.querySelectorAll('.submission-upload input[type="file"]').forEach(input=>input.addEventListener('change',()=>input.setCustomValidity('')));
  const status = root.querySelector('[data-request-live-status]');
  let timer, baseline, busy = false, ended = false;
  const next = {
    'For review':'Prepare the listed requirements and wait for Registrar review.',
    'Processing':'The Registrar is processing your request. Check progress notes for instructions.',
    'Ready for pickup':'Your document is ready. Read the collection instructions and bring your school ID and claim stub.',
    'Released':'Your document has been released. You can now leave private service feedback.'
  };
  async function poll() {
    clearTimeout(timer);
    if (ended || busy) return;
    if (document.hidden || !navigator.onLine) { status.textContent = 'Updates paused · showing the last received status.'; timer = setTimeout(poll, 5000); return; }
    busy = true;
    try {
      const response = await fetch('/Services/LiveRequest?id=' + encodeURIComponent(root.dataset.liveRequest), {cache:'no-store', signal:AbortSignal.timeout(10000)});
      if (response.status === 401 || response.status === 404) { ended = true; throw new Error(); }
      if (!response.ok) throw new Error();
      const data = await response.json();
      const signature = JSON.stringify(data);
      // Never replace editable forms: new actions/checklist changes require an explicit refresh.
      if (baseline !== undefined && baseline !== signature) root.querySelector('[data-request-changed]').hidden = false;
      baseline = signature;
      root.querySelector('[data-request-stage]').textContent = `Queue: ${data.status} · ${data.documentStatus || 'Office transaction'}`;
      root.querySelector('[data-request-next]').textContent = next[data.documentStatus] || 'Prepare your requirements and track your queue ticket for office service.';
      root.querySelector('[data-request-counts]').textContent = `${data.prepared} of ${data.total} prepared · ${data.verified} office-verified`;
      const meter = root.querySelector('[data-request-meter]'); meter.max = Math.max(1, data.total); meter.value = data.prepared;
      const stages = Object.keys(next);
      root.querySelectorAll('.ops-timeline li').forEach((node, i) => node.classList.toggle('reached', i <= stages.indexOf(data.documentStatus)));
      const history = root.querySelector('[data-request-history]');
      if (history) {
        history.replaceChildren();
        for (const item of data.history) {
          const article = document.createElement('article'); article.className = 'ops-history';
          for (const [tag, text] of [['strong', item.status], ['small', item.at + ' · Manila'], ['p', item.note]]) {
            const node = document.createElement(tag); node.textContent = text; article.append(node);
          }
          history.append(article);
        }
      }
      status.textContent = 'Live · updated ' + new Date().toLocaleTimeString();
    } catch { status.textContent = ended ? 'Request unavailable or session ended. Sign in again to continue.' : 'Connection interrupted · retrying. Showing the last received status.'; }
    finally { busy = false; if (!ended) timer = setTimeout(poll, 5000); }
  }
  document.addEventListener('visibilitychange', poll); window.addEventListener('online', poll); poll();
})();
