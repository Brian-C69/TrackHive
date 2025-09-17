(function () {
  'use strict';

  const APPROVED_STATUSES = new Set(['Approved', 'ApprovedAwaitingCertificate']);
  const STORAGE_KEY = 'trackhive:approved-leave-requests';
  let audioContext;

  function getAudioContext() {
    if (audioContext) {
      return audioContext;
    }

    const Context = window.AudioContext || window.webkitAudioContext;
    if (!Context) {
      return null;
    }

    audioContext = new Context();
    return audioContext;
  }

  function playSubmitSound() {
    const context = getAudioContext();
    if (!context) {
      return;
    }

    if (context.state === 'suspended') {
      context.resume().catch(() => { /* Ignore resume errors */ });
    }

    const now = context.currentTime;
    const oscillator = context.createOscillator();
    const gainNode = context.createGain();

    oscillator.type = 'triangle';
    oscillator.frequency.setValueAtTime(880, now);

    gainNode.gain.setValueAtTime(0.0001, now);
    gainNode.gain.exponentialRampToValueAtTime(0.09, now + 0.02);
    gainNode.gain.exponentialRampToValueAtTime(0.0001, now + 0.35);

    oscillator.connect(gainNode);
    gainNode.connect(context.destination);

    oscillator.start(now);
    oscillator.stop(now + 0.4);
  }

  function bindSubmitSound() {
    const form = document.querySelector('[data-leave-application-form]');
    if (!form) {
      return;
    }

    form.addEventListener('submit', () => {
      playSubmitSound();
    });
  }

  function loadStoredApprovals() {
    try {
      const raw = window.localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return new Set();
      }

      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) {
        return new Set();
      }

      return new Set(parsed.map((value) => String(value)));
    } catch (error) {
      console.warn('TrackHive leave approval cache read failed', error);
      return new Set();
    }
  }

  function saveStoredApprovals(ids) {
    try {
      const values = Array.from(ids);
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(values));
    } catch (error) {
      console.warn('TrackHive leave approval cache write failed', error);
    }
  }

  function collectLeaveRows() {
    return Array.from(document.querySelectorAll('[data-leave-request-id]'));
  }

  function extractRowDetails(row) {
    return {
      id: row.getAttribute('data-leave-request-id'),
      status: row.getAttribute('data-leave-request-status'),
      range: row.getAttribute('data-leave-request-range'),
      type: row.getAttribute('data-leave-request-type'),
      days: row.getAttribute('data-leave-request-days')
    };
  }

  function detectApprovedLeave() {
    const rows = collectLeaveRows();
    if (rows.length === 0) {
      return;
    }

    const stored = loadStoredApprovals();
    const currentApproved = new Set();
    const newlyApproved = [];

    rows.map(extractRowDetails).forEach((detail) => {
      if (!detail.id) {
        return;
      }

      if (APPROVED_STATUSES.has(detail.status)) {
        currentApproved.add(detail.id);
        if (!stored.has(detail.id)) {
          newlyApproved.push(detail);
        }
      }
    });

    saveStoredApprovals(currentApproved);

    if (newlyApproved.length === 0) {
      return;
    }

    if (window.TrackHivePWA && typeof window.TrackHivePWA.showLeaveApprovedNotification === 'function') {
      window.TrackHivePWA.showLeaveApprovedNotification({ details: newlyApproved });
    }
  }

  function initialise() {
    bindSubmitSound();
    detectApprovedLeave();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialise);
  } else {
    initialise();
  }
})();
