import { Injectable, OnDestroy } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationSignalRMessage } from '../models/notification.models';

@Injectable({
  providedIn: 'root'
})
export class NotificationSignalRService implements OnDestroy {
  private hubConnection?: signalR.HubConnection;
  private readonly notificationReceivedSubject = new Subject<NotificationSignalRMessage>();
  private readonly unreadCountSubject = new BehaviorSubject<number>(0);
  private reconnectTimeout?: any;
  private isExplicitlyStopped = false;

  readonly notificationReceived$ = this.notificationReceivedSubject.asObservable();
  readonly unreadCount$ = this.unreadCountSubject.asObservable();

  constructor(private readonly authService: AuthService) {}

  async startConnection(): Promise<void> {
    this.isExplicitlyStopped = false;

    if (!this.authService.isAuthenticated()) {
      return;
    }

    if (
      this.hubConnection?.state === signalR.HubConnectionState.Connected ||
      this.hubConnection?.state === signalR.HubConnectionState.Connecting ||
      this.hubConnection?.state === signalR.HubConnectionState.Reconnecting
    ) {
      return;
    }

    if (!this.hubConnection) {
      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${environment.apiUrl}/hubs/notifications`, {
          accessTokenFactory: () => this.authService.getAccessToken() ?? ''
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Default exponential backoff delay with a cap at 30 seconds
            const delay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
            return delay;
          }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();

      this.registerNotificationHandler();
    }

    this.clearReconnectTimeout();

    try {
      await this.hubConnection.start();
      console.log('SignalR connected successfully to notifications hub.');
    } catch (err) {
      console.error('Error starting SignalR connection:', err);
      if (!this.isExplicitlyStopped && this.authService.isAuthenticated()) {
        this.scheduleStartRetry();
      }
    }
  }

  async stopConnection(): Promise<void> {
    this.isExplicitlyStopped = true;
    this.clearReconnectTimeout();

    if (!this.hubConnection) {
      return;
    }

    if (this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      try {
        await this.hubConnection.stop();
        console.log('SignalR connection stopped.');
      } catch (err) {
        console.error('Error stopping SignalR connection:', err);
      }
    }
  }

  registerNotificationHandler(): void {
    if (!this.hubConnection) {
      return;
    }

    this.hubConnection.off('notificationReceived');
    this.hubConnection.off('unreadCountUpdated');

    this.hubConnection.on('notificationReceived', (notification: NotificationSignalRMessage) => {
      this.notificationReceivedSubject.next(notification);
    });

    this.hubConnection.on('unreadCountUpdated', (count: number) => {
      this.unreadCountSubject.next(count);
    });

    this.hubConnection.onreconnecting((error) => {
      console.warn('SignalR connection lost. Reconnecting automatically...', error);
    });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR connection re-established.');
      this.refreshConnectionState();
    });

    this.hubConnection.onclose((error) => {
      console.error('SignalR connection closed permanently.', error);
      if (!this.isExplicitlyStopped && this.authService.isAuthenticated()) {
        this.scheduleStartRetry();
      }
    });
  }

  setUnreadCount(count: number): void {
    this.unreadCountSubject.next(count);
  }

  ngOnDestroy(): void {
    void this.stopConnection();
  }

  private scheduleStartRetry(delayMs = 5000): void {
    this.clearReconnectTimeout();
    console.log(`Scheduling SignalR reconnect attempt in ${delayMs / 1000} seconds...`);
    this.reconnectTimeout = setTimeout(() => {
      if (!this.isExplicitlyStopped && this.authService.isAuthenticated()) {
        void this.startConnection();
      }
    }, delayMs);
  }

  private clearReconnectTimeout(): void {
    if (this.reconnectTimeout) {
      clearTimeout(this.reconnectTimeout);
      this.reconnectTimeout = undefined;
    }
  }

  private refreshConnectionState(): void {
    if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
      return;
    }
    // When SignalR reconnects, the backend will push unread updates on the next event.
  }
}
