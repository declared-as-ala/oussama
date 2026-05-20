import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { NotificationService } from '../../../core/services/notification.service';
import { OrganizationService } from '../services/organization.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { RequestDetailDialogComponent } from './request-detail-dialog.component';

@Component({
  selector: 'app-requests-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule,
    RequestDetailDialogComponent
  ],
  templateUrl: './requests-list.component.html',
  styleUrls: ['./requests-list.component.scss']
})
export class RequestsListComponent implements OnInit {
  displayedColumns: string[] = ['fullName', 'organizationName', 'organizationType', 'email', 'phone', 'createdAt', 'actions'];
  loading = false;
  requests: any[] = [];

  constructor(
    private readonly organizationService: OrganizationService,
    private readonly notificationService: NotificationService,
    private readonly router: Router,
    private readonly dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.loadRequests();
  }

  loadRequests(): void {
    this.loading = true;
    this.organizationService.getOrganizationRequests().subscribe({
      next: (data) => {
        this.requests = data.map(req => {
          const parsed = this.parseMessage(req.message);
          return {
            id: req.id,
            fullName: parsed['client'] || parsed['fullName'] || 'N/A',
            jobTitle: parsed['poste'] || parsed['jobTitle'] || 'N/A',
            organizationName: parsed['organisation'] || parsed['organizationName'] || 'N/A',
            organizationType: parsed['type'] || parsed['organizationType'] || 'N/A',
            country: parsed['pays'] || parsed['country'] || 'N/A',
            email: parsed['email'] || 'N/A',
            phone: parsed['tel'] || parsed['phone'] || 'N/A',
            createdAt: req.createdAt
          };
        });
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les demandes d\'organisation.');
      }
    });
  }

  viewDetails(req: any): void {
    const dialogRef = this.dialog.open(RequestDetailDialogComponent, {
      width: '600px',
      data: { request: req }
    });

    dialogRef.afterClosed().subscribe(action => {
      if (action === 'approve') {
        this.approveRequest(req);
      } else if (action === 'reject') {
        this.rejectRequest(req);
      }
    });
  }

  approveRequest(req: any): void {
    // Redirect to organization creation with query parameters pre-filled!
    this.router.navigate(['/super-admin/organizations/create'], {
      queryParams: {
        name: req.organizationName,
        type: req.organizationType,
        email: req.email,
        phone: req.phone,
        fullName: req.fullName
      }
    });
  }

  rejectRequest(req: any): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Rejeter la demande',
        message: `Êtes-vous sûr de vouloir rejeter et supprimer la demande de "${req.fullName}" pour "${req.organizationName}" ?`,
        confirmText: 'Rejeter',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.loading = true;
      this.organizationService.deleteOrganizationRequest(req.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Demande rejetée et supprimée.');
          this.loadRequests();
        },
        error: () => {
          this.loading = false;
          this.notificationService.showError('Impossible de rejeter la demande.');
        }
      });
    });
  }

  private parseMessage(message: string): any {
    if (!message) return {};
    const lines = message.split('\n');
    const result: any = {};
    for (const line of lines) {
      const idx = line.indexOf(':');
      if (idx !== -1) {
        const key = line.substring(0, idx).trim().toLowerCase();
        const value = line.substring(idx + 1).trim();
        result[key] = value;
      }
    }
    return result;
  }
}
