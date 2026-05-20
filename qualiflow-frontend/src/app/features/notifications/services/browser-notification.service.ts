import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { NotificationSignalRMessage } from '../models/notification.models';
import { NotificationService } from './notification.service';

@Injectable({
  providedIn: 'root'
})
export class BrowserNotificationService {
  private readonly soundUrl = this.resolveSoundUrl();
  private audio: HTMLAudioElement | null = null;
  private audioContext: AudioContext | null = null;
  private audioUnlockInitialized = false;
  private lastSoundAt = 0;

  constructor(private readonly notificationService: NotificationService) {
    this.setupAudioUnlockListeners();
  }

  async registerServiceWorker(): Promise<void> {
    if (!('serviceWorker' in navigator)) {
      return;
    }

    const registered = await navigator.serviceWorker.getRegistration('/notification-sw.js');
    if (registered) {
      return;
    }

    await navigator.serviceWorker.register('/notification-sw.js', { scope: '/' });
  }

  async requestBrowserPermission(): Promise<NotificationPermission> {
    if (!('Notification' in window)) {
      return 'denied';
    }

    if (Notification.permission === 'granted' || Notification.permission === 'denied') {
      return Notification.permission;
    }

    return Notification.requestPermission();
  }

  async syncWebPushSubscription(): Promise<void> {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
      return;
    }

    if (!environment.webPushPublicKey) {
      return;
    }

    const registration = await navigator.serviceWorker.ready;
    let subscription = await registration.pushManager.getSubscription();

    if (!subscription) {
      subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: this.urlBase64ToUint8Array(environment.webPushPublicKey)
      });
    }

    const json = subscription.toJSON();
    const p256dh = json.keys?.['p256dh'];
    const auth = json.keys?.['auth'];

    if (!json.endpoint || !p256dh || !auth) {
      return;
    }

    await firstValueFrom(this.notificationService.registerWebPushSubscription({
      endpoint: json.endpoint,
      p256dh,
      auth
    }));
  }

  async playNotificationSound(): Promise<void> {
    const now = Date.now();
    if (now - this.lastSoundAt < 600) {
      return;
    }

    this.lastSoundAt = now;

    try {
      this.ensureAudioInstance();
      const audio = this.audio;
      if (!audio) {
        return;
      }

      audio.currentTime = 0;
      await audio.play();
      return;
    } catch {
      await this.unlockAudioPlayback();

      try {
        this.ensureAudioInstance();
        const audio = this.audio;
        if (!audio) {
          return;
        }

        audio.currentTime = 0;
        await audio.play();
        return;
      } catch {
        this.playFallbackBeep();
      }
    }
  }

  async showSystemNotification(payload: NotificationSignalRMessage): Promise<void> {
    if (!('Notification' in window) || Notification.permission !== 'granted') {
      return;
    }

    const targetUrl = this.normalizeRoute(payload.actionUrl ?? payload.redirectUrl ?? '/notifications');
    const data = {
      notificationId: payload.id,
      url: targetUrl
    };

    if ('serviceWorker' in navigator) {
      const registration = await navigator.serviceWorker.ready;
      await registration.showNotification(payload.title, {
        body: payload.message,
        icon: '/assets/logo.png',
        badge: '/assets/logo.png',
        data,
        tag: `qualityflow-${payload.id}`
      });
      return;
    }

    const browserNotification = new Notification(payload.title, {
      body: payload.message,
      icon: '/assets/logo.png',
      data
    });

    browserNotification.onclick = () => {
      window.focus();
      window.location.href = targetUrl;
      browserNotification.close();
    };
  }

  isDocumentHidden(): boolean {
    return document.hidden;
  }

  private normalizeRoute(route: string): string {
    if (!route) {
      return '/notifications';
    }

    if (route.startsWith('http://') || route.startsWith('https://')) {
      return route;
    }

    return route.startsWith('/') ? route : `/${route}`;
  }

  private ensureAudioInstance(): void {
    if (!this.audio) {
      this.audio = new Audio(this.soundUrl);
      this.audio.preload = 'auto';
    }
  }

  private setupAudioUnlockListeners(): void {
    if (this.audioUnlockInitialized || typeof window === 'undefined') {
      return;
    }

    this.audioUnlockInitialized = true;

    const unlock = () => {
      void this.unlockAudioPlayback();
    };

    window.addEventListener('pointerdown', unlock, { once: true, passive: true });
    window.addEventListener('touchstart', unlock, { once: true, passive: true });
    window.addEventListener('keydown', unlock, { once: true });
  }

  private async unlockAudioPlayback(): Promise<void> {
    this.ensureAudioInstance();
    const audio = this.audio;
    if (!audio) {
      return;
    }

    const audioContext = this.getAudioContext();

    try {
      if (audioContext?.state === 'suspended') {
        await audioContext.resume();
      }
    } catch {
      // Ignore context resume failures; playback fallback is handled separately.
    }

    try {
      audio.muted = true;
      const playPromise = audio.play();
      if (playPromise) {
        await playPromise;
      }
      audio.pause();
      audio.currentTime = 0;
    } catch {
      // Browser may still block; we keep fallback beep.
    } finally {
      audio.muted = false;
    }
  }

  private resolveSoundUrl(): string {
    if (typeof document === 'undefined') {
      return 'assets/sounds/notification.mp3';
    }

    return new URL('assets/sounds/notification.mp3', document.baseURI).toString();
  }

  private getAudioContext(): AudioContext | null {
    if (typeof window === 'undefined') {
      return null;
    }

    if (this.audioContext) {
      return this.audioContext;
    }

    const audioContextCtor = (window.AudioContext || (window as any).webkitAudioContext) as typeof AudioContext | undefined;
    if (!audioContextCtor) {
      return null;
    }

    this.audioContext = new audioContextCtor();
    return this.audioContext;
  }

  private playFallbackBeep(): void {
    const audioContext = this.getAudioContext();
    if (!audioContext) {
      return;
    }
    if (audioContext.state === 'suspended') {
      void audioContext.resume();
    }
    const oscillator = audioContext.createOscillator();
    const gainNode = audioContext.createGain();

    oscillator.type = 'sine';
    oscillator.frequency.setValueAtTime(880, audioContext.currentTime);
    gainNode.gain.setValueAtTime(0.0001, audioContext.currentTime);
    gainNode.gain.exponentialRampToValueAtTime(0.08, audioContext.currentTime + 0.03);
    gainNode.gain.exponentialRampToValueAtTime(0.0001, audioContext.currentTime + 0.2);

    oscillator.connect(gainNode);
    gainNode.connect(audioContext.destination);
    oscillator.start();
    oscillator.stop(audioContext.currentTime + 0.22);
  }

  private urlBase64ToUint8Array(base64String: string): Uint8Array {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding).replace(/\-/g, '+').replace(/_/g, '/');
    const rawData = atob(base64);
    const outputArray = new Uint8Array(rawData.length);
    for (let index = 0; index < rawData.length; index += 1) {
      outputArray[index] = rawData.charCodeAt(index);
    }
    return outputArray;
  }
}
