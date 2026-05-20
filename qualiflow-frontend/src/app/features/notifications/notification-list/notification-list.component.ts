import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NotificationListItemResponse } from '../models/notification.models';

@Component({
  selector: 'app-notification-list',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule
  ],
  templateUrl: './notification-list.component.html',
  styleUrls: ['./notification-list.component.scss']
})
export class NotificationListComponent {
  @Input() notifications: NotificationListItemResponse[] = [];
  @Input() loading = false;

  @Output() readonly openNotification = new EventEmitter<NotificationListItemResponse>();
  @Output() readonly markRead = new EventEmitter<number>();
  @Output() readonly archive = new EventEmitter<number>();

  getIcon(item: NotificationListItemResponse): string {
    if (item.type.includes('DOCUMENT')) {
      return 'description';
    }

    if (item.type.includes('NONCONFORMITY')) {
      return 'report_problem';
    }

    if (item.type.includes('CORRECTIVE_ACTION')) {
      return 'task_alt';
    }

    if (item.type.includes('PROCESS')) {
      return 'account_tree';
    }

    if (item.type.includes('PROCEDURE')) {
      return 'rule';
    }

    if (item.category === 'ERROR') {
      return 'error';
    }

    if (item.category === 'WARNING') {
      return 'warning';
    }

    if (item.category === 'SUCCESS') {
      return 'check_circle';
    }

    return 'notifications';
  }

  getPriorityLabel(priority: string): string {
    switch (priority) {
      case 'CRITICAL':
        return 'Critique';
      case 'HIGH':
        return 'Haute';
      case 'LOW':
        return 'Basse';
      default:
        return 'Moyenne';
    }
  }

  onOpen(item: NotificationListItemResponse): void {
    this.openNotification.emit(item);
  }

  onMarkRead(item: NotificationListItemResponse, event: MouseEvent): void {
    event.stopPropagation();
    this.markRead.emit(item.id);
  }

  onArchive(item: NotificationListItemResponse, event: MouseEvent): void {
    event.stopPropagation();
    this.archive.emit(item.id);
  }

  trackById(_index: number, item: NotificationListItemResponse): number {
    return item.id;
  }
}
