import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { ApiService } from '../../../core/services/api.service';
import {
  CreateNotificationRequest,
  NotificationLogRequest,
  NotificationLogResponse,
  NotificationRecipientResponse,
  NotificationRecipientsQueryRequest,
  NotificationQueryParams,
  NotificationResponse,
  NotificationStatisticsResponse,
  PagedNotificationResponse,
  RegisterWebPushSubscriptionRequest,
  UnreadCountResponse,
  WebPushSubscriptionResponse
} from '../models/notification.models';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private readonly endpoint = 'notifications';
  private readonly unreadCountSubject = new BehaviorSubject<number>(0);

  readonly unreadCount$ = this.unreadCountSubject.asObservable();

  constructor(private readonly apiService: ApiService) {}

  getNotifications(params: NotificationQueryParams = {}): Observable<PagedNotificationResponse> {
    return this.apiService.get<PagedNotificationResponse>(this.endpoint, params);
  }

  getUnreadNotifications(params: NotificationQueryParams = {}): Observable<PagedNotificationResponse> {
    return this.apiService.get<PagedNotificationResponse>(`${this.endpoint}/unread`, params);
  }

  getNotificationById(id: number): Observable<NotificationResponse> {
    return this.apiService.get<NotificationResponse>(`${this.endpoint}/${id}`);
  }

  getUnreadCount(): Observable<number> {
    return this.apiService
      .get<UnreadCountResponse>(`${this.endpoint}/unread-count`)
      .pipe(map(response => response.unreadCount));
  }

  refreshUnreadCount(): Observable<number> {
    return this.getUnreadCount().pipe(
      tap(unreadCount => this.unreadCountSubject.next(unreadCount)),
      catchError(() => {
        this.unreadCountSubject.next(0);
        return of(0);
      })
    );
  }

  setUnreadCount(count: number): void {
    this.unreadCountSubject.next(count);
  }

  getStatistics(): Observable<NotificationStatisticsResponse> {
    return this.apiService.get<NotificationStatisticsResponse>(`${this.endpoint}/statistics`);
  }

  markAsRead(id: number): Observable<NotificationResponse> {
    return this.apiService.patch<NotificationResponse>(`${this.endpoint}/${id}/mark-read`, {}).pipe(
      tap(() => {
        void this.refreshUnreadCount().subscribe();
      })
    );
  }

  markAllAsRead(): Observable<{ updatedCount: number }> {
    return this.apiService.patch<{ updatedCount: number }>(`${this.endpoint}/mark-all-read`, {}).pipe(
      tap(() => {
        void this.refreshUnreadCount().subscribe();
      })
    );
  }

  archiveNotification(id: number): Observable<NotificationResponse> {
    return this.apiService.patch<NotificationResponse>(`${this.endpoint}/${id}/archive`, {}).pipe(
      tap(() => {
        void this.refreshUnreadCount().subscribe();
      })
    );
  }

  deleteNotification(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`).pipe(
      tap(() => {
        void this.refreshUnreadCount().subscribe();
      })
    );
  }

  getRecipients(query: NotificationRecipientsQueryRequest): Observable<NotificationRecipientResponse[]> {
    return this.apiService.get<NotificationRecipientResponse[]>(`${this.endpoint}/recipients`, query);
  }

  createNotification(payload: CreateNotificationRequest): Observable<NotificationResponse[]> {
    return this.apiService.post<NotificationResponse[]>(this.endpoint, payload).pipe(
      tap(() => {
        void this.refreshUnreadCount().subscribe();
      })
    );
  }

  logNotification(payload: NotificationLogRequest): Observable<NotificationLogResponse> {
    return this.apiService.post<NotificationLogResponse>(`${this.endpoint}/log`, payload);
  }

  registerWebPushSubscription(payload: RegisterWebPushSubscriptionRequest): Observable<{ id: number; success: boolean }> {
    return this.apiService.post<{ id: number; success: boolean }>(`${this.endpoint}/web-push/subscribe`, payload);
  }

  unregisterWebPushSubscription(endpoint: string): Observable<{ removed: number }> {
    return this.apiService.post<{ removed: number }>(`${this.endpoint}/web-push/unsubscribe`, { endpoint });
  }

  getMyWebPushSubscriptions(): Observable<WebPushSubscriptionResponse[]> {
    return this.apiService.get<WebPushSubscriptionResponse[]>(`${this.endpoint}/web-push/my-subscriptions`);
  }
}
