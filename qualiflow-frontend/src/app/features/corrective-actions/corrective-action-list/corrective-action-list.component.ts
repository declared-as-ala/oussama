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
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, forkJoin, of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService as CoreUserService } from '../../../core/services/user.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { NonConformityListItemResponse } from '../../non-conformities/models/nonconformity.models';
import { NonConformityService } from '../../non-conformities/services/nonconformity.service';
import {
  CORRECTIVE_ACTION_STATUS_OPTIONS,
  CORRECTIVE_ACTION_TYPE_OPTIONS,
  CorrectiveActionListItemResponse,
  CorrectiveActionStatus,
  CorrectiveActionType,
  CorrectiveActionStatisticsResponse,
  GetCorrectiveActionsQueryRequest,
  PagedCorrectiveActionResponse
} from '../models/corrective-action.models';
import { CorrectiveActionService } from '../services/corrective-action.service';

@Component({
  selector: 'app-corrective-action-list',
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
    MatMenuModule,
    MatTooltipModule
  ],
  templateUrl: './corrective-action-list.component.html',
  styleUrls: ['./corrective-action-list.component.scss']
})
export class CorrectiveActionListComponent implements OnInit {
  readonly statusOptions = CORRECTIVE_ACTION_STATUS_OPTIONS;
  readonly typeOptions = CORRECTIVE_ACTION_TYPE_OPTIONS;
  readonly displayedColumns = ['title', 'nonConformity', 'type', 'responsible', 'dueDate', 'status', 'overdue', 'createdAt', 'actions'];

  readonly filtersForm = this.fb.group({
    search: [''],
    status: ['' as CorrectiveActionStatus | ''],
    type: ['' as CorrectiveActionType | ''],
    responsibleUserId: [null as number | null],
    nonConformityId: [null as number | null],
    isOverdue: ['' as '' | 'true' | 'false'],
    fromDate: [''],
    toDate: ['']
  });

  loading = false;
  showFilters = true;
  items: CorrectiveActionListItemResponse[] = [];
  statistics: CorrectiveActionStatisticsResponse | null = null;
  users: UserResponse[] = [];
  nonConformities: NonConformityListItemResponse[] = [];
  selectedAction: CorrectiveActionListItemResponse | null = null;

  total = 0;
  pageNumber = 1;
  pageSize = 10;

  constructor(
    private readonly fb: FormBuilder,
    private readonly router: Router,
    private readonly dialog: MatDialog,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly userService: CoreUserService,
    private readonly nonConformityService: NonConformityService,
    private readonly correctiveActionService: CorrectiveActionService
  ) { }

  ngOnInit(): void {
    this.loadReferences();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get canLoadUsers(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  onSearch(): void {
    this.pageNumber = 1;
    this.refresh();
  }

  clearFilters(): void {
    this.filtersForm.reset({
      search: '',
      status: '',
      type: '',
      responsibleUserId: null,
      nonConformityId: null,
      isOverdue: '',
      fromDate: '',
      toDate: ''
    });

    this.pageNumber = 1;
    this.refresh();
  }

  onPageChanged(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.refresh();
  }

  create(): void {
    this.router.navigate(['/corrective-actions/new']);
  }

  view(item: CorrectiveActionListItemResponse): void {
    this.router.navigate(['/corrective-actions', item.id]);
  }

  edit(item: CorrectiveActionListItemResponse): void {
    this.router.navigate(['/corrective-actions', item.id, 'edit']);
  }

  delete(item: CorrectiveActionListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer action corrective',
        message: `Confirmer la suppression de ${item.title} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.correctiveActionService.deleteCorrectiveAction(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Action corrective supprimee.');
          this.refresh();
        },
        error: () => this.notificationService.showError('Suppression impossible.')
      });
    });
  }

  selectForStatus(item: CorrectiveActionListItemResponse): void {
    this.selectedAction = item;
  }

  applyStatus(status: CorrectiveActionStatus): void {
    if (!this.selectedAction || this.selectedAction.status === status) {
      return;
    }

    this.correctiveActionService.updateCorrectiveActionStatus(this.selectedAction.id, {
      status,
      comment: 'Mise a jour depuis la liste.'
    }).subscribe({
      next: () => {
        this.notificationService.showSuccess('Statut mis a jour.');
        this.refresh();
      },
      error: () => this.notificationService.showError('Impossible de changer le statut.')
    });
  }

  verify(item: CorrectiveActionListItemResponse): void {
    if (item.status !== 'REALISEE' && item.status !== 'VERIFIEE') {
      this.notificationService.showInfo('Verification possible uniquement apres realisation.');
      return;
    }

    this.correctiveActionService.verifyEffectiveness(item.id, {
      effectivenessVerified: true,
      effectivenessComment: 'Verification validee depuis la liste.'
    }).subscribe({
      next: () => {
        this.notificationService.showSuccess('Verification d efficacite enregistree.');
        this.refresh();
      },
      error: () => this.notificationService.showError('Verification impossible pour cette action.')
    });
  }

  getStatusLabel(status: CorrectiveActionStatus): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? status;
  }

  getTypeLabel(type: CorrectiveActionType): string {
    return this.typeOptions.find(option => option.value === type)?.label ?? type;
  }

  trackById(_index: number, item: CorrectiveActionListItemResponse): number {
    return item.id;
  }

  private loadReferences(): void {
    this.loading = true;

    const emptyUsersResponse = {
      total: 0,
      page: 1,
      pageSize: 300,
      items: [] as UserResponse[]
    };

    const emptyNonConformityResponse = {
      total: 0,
      pageNumber: 1,
      pageSize: 300,
      items: [] as NonConformityListItemResponse[]
    };

    const usersRequest$ = this.canLoadUsers
      ? this.userService.getAll(1, 300).pipe(catchError(() => of(emptyUsersResponse)))
      : of(emptyUsersResponse);

    forkJoin({
      users: usersRequest$,
      nonConformities: this.nonConformityService
        .getNonConformities({ pageNumber: 1, pageSize: 300 })
        .pipe(catchError(() => of(emptyNonConformityResponse)))
    }).subscribe({
      next: ({ users, nonConformities }) => {
        this.users = users.items.filter(user => user.isActive);
        this.nonConformities = nonConformities.items;
        this.refresh();
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les references du module.');
      }
    });
  }

  private refresh(): void {
    this.loading = true;

    forkJoin({
      page: this.correctiveActionService.getCorrectiveActions(this.buildQuery()),
      stats: this.correctiveActionService.getCorrectiveActionStatistics()
    }).subscribe({
      next: ({ page, stats }) => {
        this.applyPage(page);
        this.statistics = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Erreur lors du chargement des actions correctives.');
      }
    });
  }

  private applyPage(page: PagedCorrectiveActionResponse): void {
    this.items = page.items;
    this.total = page.total;
    this.pageNumber = page.pageNumber;
    this.pageSize = page.pageSize;
  }

  private buildQuery(): GetCorrectiveActionsQueryRequest {
    const raw = this.filtersForm.getRawValue();

    const overdueFilter = raw.isOverdue === 'true'
      ? true
      : raw.isOverdue === 'false'
        ? false
        : null;

    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: raw.search?.trim() || undefined,
      status: raw.status || undefined,
      type: raw.type || undefined,
      responsibleUserId: raw.responsibleUserId ?? undefined,
      nonConformityId: raw.nonConformityId ?? undefined,
      isOverdue: overdueFilter,
      fromDate: raw.fromDate || undefined,
      toDate: raw.toDate || undefined
    };
  }
}
