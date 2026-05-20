import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  ORGANIZATION_STATUS_OPTIONS,
  ORGANIZATION_TYPE_OPTIONS,
  OrganizationListItemResponse,
  OrganizationListQueryParams,
  OrganizationStatus,
  OrganizationType
} from '../models/organization.models';
import { OrganizationService } from '../services/organization.service';

@Component({
  selector: 'app-super-admin-organizations-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatTooltipModule
  ],
  templateUrl: './organizations-list.component.html',
  styleUrls: ['./organizations-list.component.scss']
})
export class OrganizationsListComponent implements OnInit {
  readonly displayedColumns: string[] = [
    'name',
    'type',
    'status',
    'subscriptionDaysRemaining',
    'usersCount',
    'adminsCount',
    'actions'
  ];

  readonly statusOptions = ORGANIZATION_STATUS_OPTIONS;
  readonly typeOptions = ORGANIZATION_TYPE_OPTIONS;

  readonly filterForm = this.fb.group({
    search: [''],
    type: [''],
    status: ['']
  });

  loading = false;
  items: OrganizationListItemResponse[] = [];
  total = 0;
  pageNumber = 1;
  pageSize = 10;

  constructor(
    private readonly fb: FormBuilder,
    private readonly organizationService: OrganizationService,
    private readonly notificationService: NotificationService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly dialog: MatDialog
  ) { }

  ngOnInit(): void {
    const statusFromQuery = this.route.snapshot.queryParamMap.get('status');
    if (statusFromQuery) {
      this.filterForm.patchValue({ status: statusFromQuery });
    }

    this.loadOrganizations();
  }

  loadOrganizations(): void {
    this.loading = true;
    this.organizationService.getOrganizations(this.buildQuery()).subscribe({
      next: (response) => {
        this.items = response.items;
        this.total = response.total;
        this.pageNumber = response.pageNumber;
        this.pageSize = response.pageSize;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les organisations.');
      }
    });
  }

  applyFilters(): void {
    this.pageNumber = 1;
    this.loadOrganizations();
  }

  clearFilters(): void {
    this.filterForm.reset({ search: '', type: '', status: '' });
    this.pageNumber = 1;
    this.loadOrganizations();
  }

  onPageChange(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadOrganizations();
  }

  createOrganization(): void {
    this.router.navigate(['/super-admin/organizations/create']);
  }

  viewOrganization(item: OrganizationListItemResponse): void {
    this.router.navigate(['/super-admin/organizations', item.id]);
  }

  editOrganization(item: OrganizationListItemResponse): void {
    this.router.navigate(['/super-admin/organizations', item.id, 'edit']);
  }

  toggleStatus(item: OrganizationListItemResponse): void {
    const nextStatus: OrganizationStatus = item.status === 'ACTIF' ? 'SUSPENDUE' : 'ACTIF';

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Changer le statut',
        message: `Voulez-vous passer ${item.name} au statut ${nextStatus} ?`,
        confirmText: 'Confirmer',
        cancelText: 'Annuler',
        type: 'info'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.organizationService.toggleOrganizationStatus(item.id, { status: nextStatus }).subscribe({
        next: () => {
          this.notificationService.showSuccess('Statut de l organisation mis a jour.');
          this.loadOrganizations();
        },
        error: () => {
          this.notificationService.showError('Impossible de changer le statut.');
        }
      });
    });
  }

  deleteOrganization(item: OrganizationListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer l\'organisation',
        message: `Êtes-vous sûr de vouloir supprimer définitivement ${item.name} ? Cette action est irréversible.`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.organizationService.deleteOrganization(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Organisation supprimée avec succès.');
          this.loadOrganizations();
        },
        error: () => {
          this.notificationService.showError('Erreur lors de la suppression de l\'organisation.');
        }
      });
    });
  }

  getTypeLabel(type: OrganizationType | string | null | undefined): string {
    if (!type) {
      return 'N/A';
    }

    return this.typeOptions.find(option => option.value === type)?.label ?? type;
  }

  getStatusLabel(status: string): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? status;
  }

  private buildQuery(): OrganizationListQueryParams {
    const raw = this.filterForm.getRawValue();

    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: raw.search?.trim() || undefined,
      type: (raw.type as OrganizationType | '') || undefined,
      status: (raw.status as OrganizationStatus | '') || undefined,
      sortBy: 'CreatedAt',
      sortDirection: 'DESC'
    };
  }
}
