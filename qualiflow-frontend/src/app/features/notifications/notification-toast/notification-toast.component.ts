import { CommonModule } from '@angular/common';
import { Component, Inject, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { MAT_SNACK_BAR_DATA, MatSnackBarRef, MatSnackBarModule } from '@angular/material/snack-bar';
import { NotificationCategory } from '../models/notification.models';

export interface NotificationToastData {
  title: string;
  message: string;
  category: NotificationCategory;
  duration?: number;
}

@Component({
  selector: 'app-notification-toast',
  standalone: true,
  imports: [CommonModule, MatSnackBarModule],
  templateUrl: './notification-toast.component.html',
  styleUrls: ['./notification-toast.component.scss']
})
export class NotificationToastComponent implements OnInit, OnDestroy {
  progressWidth = 100;
  private progressInterval?: ReturnType<typeof setInterval>;
  private readonly totalDuration: number;
  private elapsed = 0;
  private readonly tickMs = 30;

  constructor(
    @Inject(MAT_SNACK_BAR_DATA) public readonly data: NotificationToastData,
    private readonly snackBarRef: MatSnackBarRef<NotificationToastComponent>,
    private readonly cdr: ChangeDetectorRef
  ) {
    this.totalDuration = data.duration ?? 6000;
  }

  ngOnInit(): void {
    this.progressInterval = setInterval(() => {
      this.elapsed += this.tickMs;
      this.progressWidth = Math.max(0, 100 - (this.elapsed / this.totalDuration) * 100);
      this.cdr.markForCheck();
      if (this.elapsed >= this.totalDuration) {
        this.clearProgress();
      }
    }, this.tickMs);
  }

  ngOnDestroy(): void {
    this.clearProgress();
  }

  dismiss(): void {
    this.snackBarRef.dismiss();
  }

  get icon(): string {
    switch (this.data.category) {
      case 'SUCCESS': return '✓';
      case 'WARNING': return '⚠';
      case 'ERROR':   return '✕';
      default:        return 'ℹ';
    }
  }

  get cssClass(): string {
    switch (this.data.category) {
      case 'SUCCESS': return 'toast-success';
      case 'WARNING': return 'toast-warning';
      case 'ERROR':   return 'toast-error';
      default:        return 'toast-info';
    }
  }

  private clearProgress(): void {
    if (this.progressInterval) {
      clearInterval(this.progressInterval);
      this.progressInterval = undefined;
    }
  }
}
