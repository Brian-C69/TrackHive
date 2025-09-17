const CACHE_NAME = 'trackhive-static-v1';
const OFFLINE_URLS = [
  '/css/site.css',
  '/js/pwa.js',
  '/js/leave-dashboard.js',
  '/manifest.webmanifest',
  '/img/pwa-icon.svg'
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => cache.addAll(OFFLINE_URLS))
      .catch((error) => {
        console.warn('TrackHive service worker install issue', error);
      })
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))
    )
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const { request } = event;

  if (request.method !== 'GET') {
    return;
  }

  event.respondWith(
    (async () => {
      const acceptHeader = request.headers.get('Accept') || '';
      if (request.mode === 'navigate' || acceptHeader.includes('text/html')) {
        return fetch(request);
      }

      const cachedResponse = await caches.match(request);
      if (cachedResponse) {
        return cachedResponse;
      }

      try {
        const networkResponse = await fetch(request);

        if (
          networkResponse &&
          networkResponse.status === 200 &&
          networkResponse.type === 'basic'
        ) {
          const contentType = networkResponse.headers.get('Content-Type') || '';

          if (!contentType.includes('text/html')) {
            const cache = await caches.open(CACHE_NAME);
            await cache.put(request, networkResponse.clone());
          }
        }

        return networkResponse;
      } catch (error) {
        if (cachedResponse) {
          return cachedResponse;
        }

        throw error;
      }
    })()
  );
});

self.addEventListener('message', (event) => {
  if (!event.data || event.data.type !== 'LEAVE_APPROVED_NOTIFICATION') {
    return;
  }

  const { title, options } = event.data.payload || {};
  if (!title || !options) {
    return;
  }

  self.registration.showNotification(title, options);
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const targetUrl = (event.notification.data && event.notification.data.url) || '/';

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
      for (const client of clientList) {
        if (client.url.includes(targetUrl) && 'focus' in client) {
          return client.focus();
        }
      }

      if (self.clients.openWindow) {
        return self.clients.openWindow(targetUrl);
      }

      return undefined;
    })
  );
});
