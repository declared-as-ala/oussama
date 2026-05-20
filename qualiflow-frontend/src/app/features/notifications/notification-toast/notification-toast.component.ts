import { CommonModule } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { MAT_SNACK_BAR_DATA, MatSnackBarModule } from '@angular/material/snack-bar';
import { NotificationCategory } from '../models/notification.models';

export interface NotificationToastData {
  title: string;
  message: string;
  category: NotificationCategory;
}

@Component({
  selector: 'app-notification-toast',
  standalone: true,
  imports: [CommonModule, MatSnackBarModule],
  templateUrl: './notification-toast.component.html',
  styleUrls: ['./notification-toast.component.scss']
})
export class NotificationToastComponent {
  constructor(@Inject(MAT_SNACK_BAR_DATA) public readonly data: NotificationToastData) {}

  get cssClass(): string {
    switch (this.data.category) {
      case 'SUCCESS':
        return 'toast-success';
      case 'WARNING':
        return 'toast-warning';
      case 'ERROR':
        return 'toast-error';
      default:
        return 'toast-info';
    }
  }
}
