(function () {
  'use strict';

  const SERVICE_WORKER_URL = '/service-worker.js';
  const notificationNamespace = window.TrackHivePWA || {};

  function registerServiceWorker() {
    if (!('serviceWorker' in navigator)) {
      return;
    }

    const register = () => {
      navigator.serviceWorker
        .register(SERVICE_WORKER_URL)
        .catch((error) => console.warn('TrackHive service worker registration failed', error));
    };

    if (document.readyState === 'complete') {
      register();
    } else {
      window.addEventListener('load', register);
    }
  }

  async function requestNotificationPermission() {
    if (!('Notification' in window)) {
      return 'denied';
    }

    if (Notification.permission === 'granted' || Notification.permission === 'denied') {
      return Notification.permission;
    }

    try {
      return await Notification.requestPermission();
    } catch (error) {
      console.warn('TrackHive notification permission request failed', error);
      return Notification.permission;
    }
  }

  async function sendMessageToServiceWorker(message) {
    if (!('serviceWorker' in navigator)) {
      return false;
    }

    try {
      const registration = await navigator.serviceWorker.ready;
      if (registration.active) {
        registration.active.postMessage(message);
        return true;
      }
    } catch (error) {
      console.warn('TrackHive service worker messaging failed', error);
    }

    return false;
  }

  async function showLeaveApprovedNotification(payload) {
    const details = payload && payload.details ? payload.details : [];
    const count = Array.isArray(details) ? details.length : 0;
    if (count === 0) {
      return false;
    }

    const permission = await requestNotificationPermission();
    if (permission !== 'granted') {
      return false;
    }

    const latest = details[0];
    const url = '/EmployeeDashboard';
    const title = count > 1 ? 'Leave requests approved' : 'Leave approved';
    const range = latest && latest.range ? latest.range : 'your upcoming leave';
    const type = latest && latest.type ? latest.type : 'leave';
    const body = count > 1
      ? `${count} of your leave requests were approved. Latest: ${range} (${type}).`
      : `Your leave for ${range} (${type}) was approved.`;

    const options = {
      body,
      icon: '/img/pwa-icon.svg',
      badge: '/img/pwa-icon.svg',
      tag: 'trackhive-leave-approvals',
      renotify: true,
      data: { url }
    };

    if (latest && latest.days) {
      options.body += ` Duration: ${latest.days} day(s).`;
    }

    const messageSent = await sendMessageToServiceWorker({
      type: 'LEAVE_APPROVED_NOTIFICATION',
      payload: { title, options }
    });

    if (messageSent) {
      return true;
    }

    if ('serviceWorker' in navigator) {
      try {
        const registration = await navigator.serviceWorker.getRegistration();
        if (registration && registration.showNotification) {
          await registration.showNotification(title, options);
          return true;
        }
      } catch (error) {
        console.warn('TrackHive direct notification failed', error);
      }
    }

    if ('Notification' in window) {
      new Notification(title, options);
      return true;
    }

    return false;
  }

  notificationNamespace.ensureNotificationPermission = requestNotificationPermission;
  notificationNamespace.showLeaveApprovedNotification = showLeaveApprovedNotification;
  window.TrackHivePWA = notificationNamespace;

  registerServiceWorker();
})();
