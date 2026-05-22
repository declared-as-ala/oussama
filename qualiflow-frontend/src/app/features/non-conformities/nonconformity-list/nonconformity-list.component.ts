import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { catchError, forkJoin, of } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserResponse, UserService as CoreUserService } from '../../../core/services/user.service';
import { ProcessListItemResponse } from '../../processes/models/process.models';
import { ProcessService } from '../../processes/services/process.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  NON_CONFORMITY_SEVERITY_OPTIONS,
  NON_CONFORMITY_STATUS_OPTIONS,
  NonConformityListItemResponse,
  NonConformityQueryParams,
  NonConformitySeverity,
  NonConformityStatisticsResponse,
  NonConformityStatus,
  PagedNonConformityResponse
} from '../models/nonconformity.models';
import { NonConformityService } from '../services/nonconformity.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-nonconformity-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatTooltipModule,
    MatMenuModule,
    TranslatePipe
  ],
  templateUrl: './nonconformity-list.component.html',
  styleUrls: ['./nonconformity-list.component.scss']
})
export class NonconformityListComponent implements OnInit {
  readonly statusOptions = NON_CONFORMITY_STATUS_OPTIONS;
  readonly severityOptions = NON_CONFORMITY_SEVERITY_OPTIONS;
  readonly displayedColumns = ['code', 'title', 'severity', 'status', 'linkedTo', 'responsible', 'detectedDate', 'actions'];

  readonly filtersForm = this.fb.group({
    search: [''],
    status: ['' as NonConformityStatus | ''],
    severity: ['' as NonConformitySeverity | ''],
    processId: [null as number | null],
    responsibleUserId: [null as number | null]
  });

  loading = false;
  showFilters = true;
  awaitingValidationMode = false;
  items: NonConformityListItemResponse[] = [];
  processes: ProcessListItemResponse[] = [];
  users: UserResponse[] = [];
  statistics: NonConformityStatisticsResponse | null = null;

  total = 0;
  pageNumber = 1;
  pageSize = 10;

  constructor(
    private readonly fb: FormBuilder,
    private readonly nonConformityService: NonConformityService,
    private readonly processService: ProcessService,
    private readonly userService: CoreUserService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog,
    private readonly router: Router
  ) { }

  ngOnInit(): void {
    this.loading = true;
    const emptyUsersResponse = {
      total: 0,
      page: 1,
      pageSize: 300,
      items: [] as UserResponse[]
    };
    const usersRequest$ = this.canLoadUsers
      ? this.userService.getAll(1, 300).pipe(catchError(() => of(emptyUsersResponse)))
      : of(emptyUsersResponse);

    forkJoin({
      processes: this.processService.getProcesses({ pageNumber: 1, pageSize: 300 }),
      users: usersRequest$
    }).subscribe({
      next: ({ processes, users }) => {
        this.processes = processes.items;
        this.users = users.items.filter(user => user.isActive);
        this.refresh();
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les filtres non-conformites.');
      }
    });
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get canLoadUsers(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get canCreate(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'UTILISATEUR']);
  }

  get canManageValidation(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  refresh(): void {
    this.loading = true;
    const pageRequest$ = this.awaitingValidationMode
      ? this.nonConformityService.getAwaitingValidation(this.buildQuery())
      : this.nonConformityService.getNonConformities(this.buildQuery());

    forkJoin({
      page: pageRequest$,
      stats: this.nonConformityService.getStatistics()
    }).subscribe({
      next: ({ page, stats }) => {
        this.applyPage(page);
        this.statistics = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Erreur lors du chargement des non-conformites.');
      }
    });
  }

  focusAwaitingValidation(): void {
    if (!this.canManageValidation) {
      return;
    }

    this.awaitingValidationMode = true;
    this.pageNumber = 1;
    this.refresh();
  }

  showAllNonConformities(): void {
    this.awaitingValidationMode = false;
    this.pageNumber = 1;
    this.refresh();
  }

  onSearch(): void {
    this.pageNumber = 1;
    this.refresh();
  }

  clearFilters(): void {
    this.filtersForm.reset({
      search: '',
      status: '',
      severity: '',
      processId: null,
      responsibleUserId: null
    });

    this.pageNumber = 1;
    this.refresh();
  }

  onPageChanged(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.refresh();
  }

  createNonConformity(): void {
    this.router.navigate(['/non-conformities/new']);
  }

  view(item: NonConformityListItemResponse): void {
    this.router.navigate(['/non-conformities', item.id]);
  }

  edit(item: NonConformityListItemResponse): void {
    this.router.navigate(['/non-conformities', item.id, 'edit']);
  }

  updateStatus(item: NonConformityListItemResponse, nextStatus: NonConformityStatus): void {
    if (item.status === nextStatus) {
      return;
    }

    this.nonConformityService.updateNonConformityStatus(item.id, { status: nextStatus }).subscribe({
      next: () => {
        this.notificationService.showSuccess('Statut de la non-conformite mis a jour.');
        this.refresh();
      },
      error: () => {
        this.notificationService.showError('Impossible de changer le statut.');
      }
    });
  }

  delete(item: NonConformityListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer la non-conformite',
        message: `Confirmer la suppression de ${item.code} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.nonConformityService.deleteNonConformity(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Non-conformite supprimee.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Suppression impossible.');
        }
      });
    });
  }

  getStatusLabel(status: NonConformityStatus): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? status;
  }

  getSeverityLabel(severity: NonConformitySeverity): string {
    return this.severityOptions.find(option => option.value === severity)?.label ?? severity;
  }

  trackById(_index: number, item: NonConformityListItemResponse): number {
    return item.id;
  }

  private applyPage(page: PagedNonConformityResponse): void {
    this.items = page.items;
    this.total = page.total;
    this.pageNumber = page.pageNumber;
    this.pageSize = page.pageSize;
  }

  private buildQuery(): NonConformityQueryParams {
    const raw = this.filtersForm.getRawValue();

    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: raw.search?.trim() || undefined,
      status: raw.status || undefined,
      severity: raw.severity || undefined,
      processId: raw.processId ?? undefined,
      responsibleUserId: raw.responsibleUserId ?? undefined
    };
  }
}
