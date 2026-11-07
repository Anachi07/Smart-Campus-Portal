(() => {
  // iOS has no beforeinstallprompt API; install help must not depend on service workers.
  const registration = Promise.resolve().then(() => window.isSecureContext && 'serviceWorker' in navigator
    ? navigator.serviceWorker.register('/service-worker.js') : null).catch(() => null);
  const ios = /iPad|iPhone|iPod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  const inApp = /FBAN|FBAV|Instagram|Messenger|Line\//i.test(navigator.userAgent);
  const bar = document.createElement('div'); bar.className = 'campus-app-tools';
  const label = document.createElement('span'); label.setAttribute('role', 'status');
  const install = document.createElement('button'); install.type = 'button'; install.textContent = 'Install campus app'; install.hidden = true;
  const notify = document.createElement('button'); notify.type = 'button'; notify.textContent = 'Enable browser alerts';
  let installed = !!(window.matchMedia('(display-mode: standalone)').matches || navigator.standalone);
  const guide = document.createElement('section'); guide.className = 'campus-install-guide'; guide.id = 'campus-install-guide'; guide.hidden = true;
  const heading = document.createElement('h2'); heading.id = 'campus-install-title'; guide.setAttribute('aria-labelledby', heading.id); heading.textContent = ios ? 'Add ICAS SUCAT on iPhone / iPad' : 'Install ICAS SUCAT';
  const intro = document.createElement('p');
  intro.textContent = !window.isSecureContext ? 'Open the HTTPS address of this site before installing.' : ios
    ? (inApp ? 'Open this link in Safari first using the app menu, or copy the link into Safari.' : 'iPhone and iPad use the browser Share menu to add this app. No Settings app is needed.')
    : 'If the install prompt is unavailable, open your browser menu and look for Install app or Add to Home screen. In an in-app browser, open this link in Chrome first.';
  const steps = document.createElement('ol');
  for (const text of ios ? ['Open this site in Safari. Tap Share (the square with an upward arrow). In some Safari layouts, Share is inside the More (…) menu.', 'Choose Add to Home Screen. Scroll through the actions if you do not see it immediately.', 'Keep the name ICAS SUCAT, enable Open as Web App if shown, then tap Add. Open the new icon from your Home Screen.'] : ['Open the browser menu.', 'Choose Install app or Add to Home screen when available, then confirm.']) {
    const item = document.createElement('li'); item.textContent = text; steps.append(item);
  }
  const close = document.createElement('button'); close.type = 'button'; close.textContent = 'Close install instructions';
  guide.append(heading, intro, steps, close); install.setAttribute('aria-controls', guide.id); install.setAttribute('aria-expanded', 'false');
  function showGuide(open) {guide.hidden = !open; install.setAttribute('aria-expanded', String(open)); if (open) {heading.tabIndex = -1; heading.focus({preventScroll:true}); guide.scrollIntoView({block:'nearest'});} else install.focus({preventScroll:true});}
  close.addEventListener('click', () => showGuide(false));
  guide.addEventListener('keydown', event => {if (event.key === 'Escape') {event.stopPropagation(); showGuide(false);}});
  function updateInstall() {install.hidden = installed; install.textContent = ios ? 'Install on iPhone / iPad' : 'Install campus app'; label.textContent = installed ? 'ICAS SUCAT app' : 'ICAS SUCAT on your phone'; if (installed) {guide.hidden = true; install.setAttribute('aria-expanded', 'false');}}
  let prompt;
  updateInstall();
  window.addEventListener('beforeinstallprompt', event => {event.preventDefault(); if (!ios && !installed) prompt = event;});
  install.addEventListener('click', async () => {
    if (installed) return;
    if (ios || !prompt || !window.isSecureContext) {showGuide(guide.hidden); return;}
    const pending = prompt; prompt = null; install.disabled = true;
    try {await pending.prompt(); const choice = await pending.userChoice; if (choice.outcome === 'accepted') {installed = true; updateInstall();}}
    catch {showGuide(true);} finally {install.disabled = false;}
  });
  window.addEventListener('appinstalled', () => {installed = true; prompt = null; updateInstall();});
  const endpoint = document.body.dataset.notificationSummaryUrl;
  notify.hidden = (ios && !installed) || !endpoint || !('Notification' in window) || Notification.permission !== 'default';
  notify.addEventListener('click', async () => {
    try {
      const result = await Notification.requestPermission(); notify.hidden = true;
      label.textContent = result === 'granted' ? 'Browser alerts enabled while the portal is open. Check Notifications for full details.' : 'Browser alerts are off. You can still check Notifications in the portal.';
    } catch { label.textContent = 'Browser alerts are unavailable. Check Notifications in the portal.'; }
  });
  // Generic lock-screen text keeps student details out of OS notifications.
  window.addEventListener('campus:notification', async () => {
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    try { const worker = await registration; await worker?.showNotification('ICAS Campus update', {body:'You have a new campus service update. Open the portal to view it.', icon:'/pwa/icon.svg', tag:'icas-campus-update'}); } catch {}
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
  bar.append(label, install, notify, guide);
  document.body.prepend(bar);
})();
