(() => {
  if (!('serviceWorker' in navigator) || !window.isSecureContext) return;
  const registration = navigator.serviceWorker.register('/service-worker.js');
  registration.catch(() => {});
  const bar = document.createElement('div'); bar.className = 'campus-app-tools';
  const label = document.createElement('span'); label.setAttribute('role', 'status');
  const install = document.createElement('button'); install.type = 'button'; install.textContent = 'Install campus app'; install.hidden = true;
  const notify = document.createElement('button'); notify.type = 'button'; notify.textContent = 'Enable browser alerts';
  const installed = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone;
  label.textContent = installed ? 'Campus app' : 'Campus on your phone';
  let prompt;
  window.addEventListener('beforeinstallprompt', event => { event.preventDefault(); prompt = event; install.hidden = false; });
  install.addEventListener('click', async () => {
    if (!prompt) return;
    await prompt.prompt(); await prompt.userChoice; prompt = null; install.hidden = true;
  });
  window.addEventListener('appinstalled', () => { install.hidden = true; label.textContent = 'Campus app installed'; });
  const endpoint = document.body.dataset.notificationSummaryUrl;
  notify.hidden = !endpoint || !('Notification' in window) || Notification.permission !== 'default';
  notify.addEventListener('click', async () => {
    try {
      const result = await Notification.requestPermission(); notify.hidden = true;
      label.textContent = result === 'granted' ? 'Browser alerts enabled while the portal is open. Check Notifications for full details.' : 'Browser alerts are off. You can still check Notifications in the portal.';
    } catch { label.textContent = 'Browser alerts are unavailable. Check Notifications in the portal.'; }
  });
  // Generic lock-screen text keeps student details out of OS notifications.
  window.addEventListener('campus:notification', async () => {
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    try { const worker = await registration; await worker.showNotification('ICAS Campus update', {body:'You have a new campus service update. Open the portal to view it.', icon:'/pwa/icon.svg', tag:'icas-campus-update'}); } catch {}
  });
  if (endpoint && document.body.dataset.campusNotificationPoll) {
    let latest = null, polling = false, ended = false;
    const poll = async () => {
      if (polling || ended || document.hidden || !navigator.onLine) return;
      polling = true;
      try {
        const response = await fetch(endpoint, {cache:'no-store', signal:AbortSignal.timeout(10000)});
        if (response.status === 401) { ended = true; return; }
        if (!response.ok) return;
        const data = await response.json();
        if (latest !== null && data.latestId > latest) window.dispatchEvent(new Event('campus:notification'));
        latest = data.latestId;
      } catch {} finally { polling = false; }
    };
    poll(); setInterval(poll, 10000);
  }
  bar.append(label, install, notify);
  if (!installed && !('onbeforeinstallprompt' in window)) label.textContent = 'Add to your home screen from your browser menu.';
  document.body.prepend(bar);
})();
