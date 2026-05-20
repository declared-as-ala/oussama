import { Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NotificationToastComponent } from '../../features/notifications/notification-toast/notification-toast.component';

type NotificationCategory = 'INFO' | 'SUCCESS' | 'WARNING' | 'ERROR';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  constructor(private snackBar: MatSnackBar) { }

  showSuccess(message: string): void {
    this.snackBar.open(message, 'OK', {
      duration: 3000,
      panelClass: ['success-snackbar'],
      horizontalPosition: 'center',
      verticalPosition: 'top'
    });
  }

  showError(message: string): void {
    let finalMessage = message;
    if (
      (message.toLowerCase().includes('chargement') || message.toLowerCase().includes('charger')) &&
      !message.toLowerCase().includes('connexion internet')
    ) {
      finalMessage = `${message} Veuillez vérifier votre connexion internet.`;
    }

    this.snackBar.open(finalMessage, 'OK', {
      duration: 5000,
      panelClass: ['error-snackbar'],
      horizontalPosition: 'center',
      verticalPosition: 'top'
    });
  }

  showInfo(message: string): void {
    this.snackBar.open(message, 'OK', {
      duration: 3000,
      panelClass: ['info-snackbar'],
      horizontalPosition: 'center',
      verticalPosition: 'top'
    });
  }

  showWarning(message: string): void {
    this.snackBar.open(message, 'OK', {
      duration: 4000,
      panelClass: ['warning-snackbar'],
      horizontalPosition: 'center',
      verticalPosition: 'top'
    });
  }

  showRealtimeNotification(title: string, message: string, category: NotificationCategory = 'INFO'): void {
    const categoryClass = `realtime-${category.toLowerCase()}`;

    this.snackBar.openFromComponent(NotificationToastComponent, {
      duration: 4000,
      horizontalPosition: 'center',
      verticalPosition: 'top',
      panelClass: ['notification-toast-panel', 'realtime-snackbar', categoryClass],
      data: {
        title,
        message,
        category
      }
    });
  }
}
