import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NotificationListItemResponse, NotificationSignalRMessage } from '../models/notification.models';
import { NotificationSignalRService } from '../services/notification-signalr.service';
import { NotificationService as UserNotificationService } from '../services/notification.service';

@Component({
  selector: 'app-notification-panel',
  standalone: true,
  imports: [
    CommonModule,
    MatBadgeModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatTooltipModule
  ],
  templateUrl: './notification-panel.component.html',
  styleUrls: ['./notification-panel.component.scss']
})
export class NotificationPanelComponent implements OnInit, OnDestroy {
  private readonly subscriptions = new Subscription();

  unreadCount = 0;
  loading = false;
  recentNotifications: NotificationListItemResponse[] = [];

  constructor(
    private readonly notificationService: UserNotificationService,
    private readonly signalRService: NotificationSignalRService,
    private readonly router: Router
  ) { }

  ngOnInit(): void {
    this.subscriptions.add(
      this.notificationService.unreadCount$.subscribe(count => {
        this.unreadCount = count;
      })
    );

    this.subscriptions.add(
      this.signalRService.notificationReceived$.subscribe(notification => {
        this.handleIncomingNotification(notification);
      })
    );

    this.refreshUnreadCount();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  onMenuOpened(): void {
    this.refreshUnreadCount();
    this.loadRecentNotifications();
  }

  markAsRead(item: NotificationListItemResponse, event: MouseEvent): void {
    event.stopPropagation();
    this.notificationService.markAsRead(item.id).subscribe({
      next: () => {
        item.isRead = true;
        this.refreshUnreadCount();
      }
    });
  }

  markAllAsRead(event: MouseEvent): void {
    event.stopPropagation();
    this.notificationService.markAllAsRead().subscribe({
      next: () => {
        this.recentNotifications = this.recentNotifications.map(item => ({ ...item, isRead: true }));
        this.refreshUnreadCount();
      }
    });
  }

  openNotification(item: NotificationListItemResponse): void {
    const navigate = () => {
      if (!item.actionUrl) {
        this.router.navigate(['/notifications']);
        return;
      }

      const normalized = item.actionUrl.startsWith('/') ? item.actionUrl : `/${item.actionUrl}`;
      this.router.navigateByUrl(normalized);
    };

    if (item.isRead) {
      navigate();
      return;
    }

    this.notificationService.markAsRead(item.id).subscribe({
      next: () => {
        item.isRead = true;
        this.refreshUnreadCount();
        navigate();
      },
      error: () => navigate()
    });
  }

  openCenter(): void {
    this.router.navigate(['/notifications']);
  }

  getTimeAgo(date: string | Date): string {
    const now = new Date();
    const past = new Date(date);
    const msPerMinute = 60 * 1000;
    const msPerHour = msPerMinute * 60;
    const msPerDay = msPerHour * 24;
    const msPerMonth = msPerDay * 30;
    const msPerYear = msPerDay * 365;

    const elapsed = now.getTime() - past.getTime();

    if (elapsed < msPerMinute) {
      return 'JUST NOW';
    } else if (elapsed < msPerHour) {
      return Math.round(elapsed / msPerMinute) + 'M AGO';
    } else if (elapsed < msPerDay) {
      return Math.round(elapsed / msPerHour) + 'H AGO';
    } else if (elapsed < msPerMonth) {
      return Math.round(elapsed / msPerDay) + 'D AGO';
    } else if (elapsed < msPerYear) {
      return Math.round(elapsed / msPerMonth) + ' MONTHS AGO';
    } else {
      return Math.round(elapsed / msPerYear) + ' YEARS AGO';
    }
  }

  getIcon(item: NotificationListItemResponse): string {
    if (item.priority === 'CRITICAL' || item.category === 'ERROR') {
      return 'report_problem';
    }

    if (item.category === 'SUCCESS' || item.title.toLowerCase().includes('complete') || item.title.toLowerCase().includes('finalized')) {
      return item.title.toLowerCase().includes('update') ? 'info' : 'check_circle_outline';
    }

    if (item.category === 'WARNING') {
      return 'warning';
    }

    return 'mail_outline';
  }

  getCategoryClass(item: NotificationListItemResponse): string {
    if (item.priority === 'CRITICAL' || item.category === 'ERROR') {
      return 'cat-critical';
    }

    if (item.title.toLowerCase().includes('update') || item.category === 'SUCCESS') {
      return 'cat-system';
    }

    if (item.title.toLowerCase().includes('task') || item.title.toLowerCase().includes('finalized')) {
      return 'cat-task';
    }

    return 'cat-report';
  }

  getPriorityLabel(item: NotificationListItemResponse): string {
    if (item.priority === 'CRITICAL') {
      return 'Critique';
    }

    if (item.priority === 'HIGH') {
      return 'Elevee';
    }

    if (item.priority === 'MEDIUM') {
      return 'Moyenne';
    }

    return 'Basse';
  }

  getPriorityClass(item: NotificationListItemResponse): string {
    if (item.priority === 'CRITICAL') {
      return 'priority-critical';
    }

    if (item.priority === 'HIGH') {
      return 'priority-high';
    }

    if (item.priority === 'MEDIUM') {
      return 'priority-medium';
    }

    return 'priority-low';
  }

  trackById(_index: number, item: NotificationListItemResponse): number {
    return item.id;
  }

  private refreshUnreadCount(): void {
    this.notificationService.refreshUnreadCount().subscribe();
  }

  private loadRecentNotifications(): void {
    this.loading = true;
    this.notificationService.getUnreadNotifications({ pageNumber: 1, pageSize: 6 }).subscribe({
      next: unreadResponse => {
        if (unreadResponse.items.length > 0) {
          this.recentNotifications = unreadResponse.items;
          this.loading = false;
          return;
        }

        this.notificationService.getNotifications({ pageNumber: 1, pageSize: 6 }).subscribe({
          next: response => {
            this.recentNotifications = response.items;
            this.loading = false;
          },
          error: () => {
            this.recentNotifications = [];
            this.loading = false;
          }
        });
      },
      error: () => {
        this.notificationService.getNotifications({ pageNumber: 1, pageSize: 6 }).subscribe({
          next: response => {
            this.recentNotifications = response.items;
            this.loading = false;
          },
          error: () => {
            this.recentNotifications = [];
            this.loading = false;
          }
        });
      }
    });
  }

  private handleIncomingNotification(notification: NotificationSignalRMessage): void {
    const listItem = this.mapToListItem(notification);
    this.recentNotifications = [listItem, ...this.recentNotifications.filter(item => item.id !== listItem.id)].slice(0, 6);
  }

  private mapToListItem(notification: NotificationSignalRMessage): NotificationListItemResponse {
    return {
      id: notification.id,
      type: notification.type,
      category: notification.category,
      title: notification.title,
      message: notification.message,
      priority: notification.priority,
      isRead: notification.isRead,
      isArchived: notification.isArchived,
      actionUrl: notification.actionUrl ?? null,
      redirectUrl: notification.redirectUrl ?? null,
      referenceType: notification.referenceType ?? null,
      referenceId: notification.referenceId ?? null,
      sourceModule: notification.sourceModule ?? null,
      createdAt: notification.createdAt
    };
  }
}
