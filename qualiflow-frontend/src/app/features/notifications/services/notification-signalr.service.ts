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

  readonly notificationReceived$ = this.notificationReceivedSubject.asObservable();
  readonly unreadCount$ = this.unreadCountSubject.asObservable();

  constructor(private readonly authService: AuthService) {}

  async startConnection(): Promise<void> {
    if (!this.authService.isAuthenticated()) {
      return;
    }

    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (!this.hubConnection) {
      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${environment.apiUrl}/hubs/notifications`, {
          accessTokenFactory: () => this.authService.getAccessToken() ?? ''
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

      this.registerNotificationHandler();
    }

    await this.hubConnection.start();
  }

  async stopConnection(): Promise<void> {
    if (!this.hubConnection) {
      return;
    }

    if (this.hubConnection.state !== signalR.HubConnectionState.Disconnected) {
      await this.hubConnection.stop();
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

    this.hubConnection.onreconnected(() => {
      this.refreshConnectionState();
    });
  }

  setUnreadCount(count: number): void {
    this.unreadCountSubject.next(count);
  }

  ngOnDestroy(): void {
    void this.stopConnection();
  }

  private refreshConnectionState(): void {
    if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    // When SignalR reconnects, the backend will push unread updates on the next event.
  }
}
