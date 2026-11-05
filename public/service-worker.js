const CACHE = 'icas-public-v2';
// Deliberately cache only this public offline shell. Never store authenticated pages or API responses.
const PUBLIC = ['/offline.html', '/pwa/icon.svg', '/accessibility/preferences.js', '/accessibility/preferences.css'];
self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(PUBLIC)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith('icas-public-') && key !== CACHE).map(key => caches.delete(key)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== self.location.origin) return;
  if (event.request.mode === 'navigate') {
    event.respondWith(fetch(event.request).catch(() => caches.match('/offline.html')));
  } else if (PUBLIC.includes(url.pathname) && !url.search) {
    event.respondWith(caches.match(url.pathname).then(cached => cached || fetch(event.request)));
  }
});
self.addEventListener('notificationclick', event => {
  event.notification.close();
  event.waitUntil(self.clients.openWindow('/Queue/Notifications'));
});
