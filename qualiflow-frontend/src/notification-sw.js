self.addEventListener('push', event => {
  const payload = event.data ? event.data.json() : {};

  const title = payload.title || 'QualityFlow';
  const body = payload.message || payload.body || 'Nouvelle notification';
  const redirectUrl = payload.redirectUrl || payload.url || payload.actionUrl || '/notifications';
  const icon = payload.icon || '/assets/logo.png';
  const badge = payload.badge || '/assets/logo.png';
  const notificationId = payload.notificationId || payload.id || null;

  event.waitUntil(
    self.registration.showNotification(title, {
      body,
      icon,
      badge,
      data: {
        url: redirectUrl,
        notificationId
      },
      tag: notificationId ? `qualityflow-${notificationId}` : `qualityflow-${Date.now()}`
    })
  );
});

self.addEventListener('notificationclick', event => {
  event.notification.close();

  const targetUrl = (event.notification.data && event.notification.data.url) || '/notifications';
  const absoluteTargetUrl = new URL(targetUrl, self.location.origin).href;

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windowClients => {
      for (const client of windowClients) {
        const isSameOrigin = client.url.startsWith(self.location.origin);
        if (!isSameOrigin) {
          continue;
        }

        if ('focus' in client) {
          client.focus();
        }

        if ('navigate' in client) {
          return client.navigate(absoluteTargetUrl);
        }

        return client;
      }

      if (clients.openWindow) {
        return clients.openWindow(absoluteTargetUrl);
      }

      return null;
    })
  );
});
