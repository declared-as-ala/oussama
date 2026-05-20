import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { Clipboard } from '@angular/cdk/clipboard';
import { NotificationService } from '../../../core/services/notification.service';

interface RequestDetailData {
  request: {
    id: number;
    fullName: string;
    jobTitle: string;
    organizationName: string;
    organizationType: string;
    country: string;
    email: string;
    phone: string;
    createdAt: Date;
  };
}

@Component({
  selector: 'app-request-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule
  ],
  templateUrl: './request-detail-dialog.component.html',
  styleUrls: ['./request-detail-dialog.component.scss']
})
export class RequestDetailDialogComponent {
  constructor(
    private readonly dialogRef: MatDialogRef<RequestDetailDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: RequestDetailData,
    private readonly clipboard: Clipboard,
    private readonly notificationService: NotificationService
  ) {}

  copyToClipboard(text: string, label: string): void {
    if (!text || text === 'N/A') return;
    this.clipboard.copy(text);
    this.notificationService.showSuccess(`${label} copié avec succès !`);
  }

  onAction(action: 'approve' | 'reject' | 'close'): void {
    this.dialogRef.close(action);
  }
}
