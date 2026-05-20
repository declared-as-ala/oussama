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
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { catchError, forkJoin, of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService as CoreUserService } from '../../../core/services/user.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ProcessListItemResponse } from '../../processes/models/process.models';
import { ProcessService } from '../../processes/services/process.service';
import {
  GetIndicatorsQueryRequest,
  IndicatorListItemResponse,
  IndicatorStatisticsResponse,
  IndicatorStatus,
  MeasurementFrequency,
  PagedIndicatorResponse,
  INDICATOR_FREQUENCY_OPTIONS,
  INDICATOR_STATUS_OPTIONS
} from '../models/indicator.models';
import { IndicatorService } from '../services/indicator.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-indicator-list',
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
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe
  ],
  templateUrl: './indicator-list.component.html',
  styleUrls: ['./indicator-list.component.scss']
})
export class IndicatorListComponent implements OnInit {
  readonly statusOptions = INDICATOR_STATUS_OPTIONS;
  readonly frequencyOptions = INDICATOR_FREQUENCY_OPTIONS;
  readonly displayedColumns = [
    'code',
    'name',
    'process',
    'unit',
    'target',
    'threshold',
    'latestValue',
    'alert',
    'responsible',
    'status',
    'actions'
  ];

  readonly filtersForm = this.fb.group({
    search: [''],
    status: ['' as IndicatorStatus | ''],
    measurementFrequency: ['' as MeasurementFrequency | ''],
    processId: [null as number | null],
    responsibleUserId: [null as number | null],
    isInAlert: ['' as '' | 'true' | 'false']
  });

  viewMode: 'cards' | 'table' = 'cards';
  loading = false;
  items: IndicatorListItemResponse[] = [];
  statistics: IndicatorStatisticsResponse | null = null;
  users: UserResponse[] = [];
  processes: ProcessListItemResponse[] = [];

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
    private readonly processService: ProcessService,
    private readonly indicatorService: IndicatorService
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

  create(): void {
    this.router.navigate(['/indicators/new']);
  }

  openDashboard(): void {
    this.router.navigate(['/indicators/dashboard']);
  }

  view(item: IndicatorListItemResponse): void {
    this.router.navigate(['/indicators', item.id]);
  }

  edit(item: IndicatorListItemResponse): void {
    this.router.navigate(['/indicators', item.id, 'edit']);
  }

  openValues(item: IndicatorListItemResponse): void {
    this.router.navigate(['/indicators', item.id], { queryParams: { tab: 'values' } });
  }

  openChart(item: IndicatorListItemResponse): void {
    this.router.navigate(['/indicators', item.id], { queryParams: { tab: 'chart' } });
  }

  delete(item: IndicatorListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer indicateur',
        message: `Confirmer la suppression de ${item.code} - ${item.name} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.indicatorService.deleteIndicator(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Indicateur supprime.');
          this.refresh();
        },
        error: () => this.notificationService.showError('Suppression impossible.')
      });
    });
  }

  toggleStatus(item: IndicatorListItemResponse): void {
    this.indicatorService.toggleIndicatorStatus(item.id).subscribe({
      next: () => {
        this.notificationService.showSuccess('Statut indicateur mis a jour.');
        this.refresh();
      },
      error: () => this.notificationService.showError('Changement de statut impossible.')
    });
  }

  onSearch(): void {
    this.pageNumber = 1;
    this.refresh();
  }

  clearFilters(): void {
    this.filtersForm.reset({
      search: '',
      status: '',
      measurementFrequency: '',
      processId: null,
      responsibleUserId: null,
      isInAlert: ''
    });

    this.pageNumber = 1;
    this.refresh();
  }

  onPageChanged(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.refresh();
  }

  getPerformanceScore(item: IndicatorListItemResponse): number {
    if (!item.latestValue || !item.targetValue || item.targetValue === 0) return 0;
    const score = (item.latestValue / item.targetValue) * 100;
    return Math.min(100, Math.max(0, Math.round(score)));
  }

  getScoreColor(score: number): string {
    if (score >= 90) return '#00875a'; // Vert XL
    if (score >= 70) return '#eab308'; // Jaune
    return '#ef4444'; // Rouge
  }

  getStatusIcon(item: IndicatorListItemResponse): string {
    return item.status === 'ACTIF' ? '✓' : '○';
  }

  getStatusLabel(status: IndicatorStatus): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? status;
  }

  getFrequencyLabel(frequency: MeasurementFrequency): string {
    return this.frequencyOptions.find(option => option.value === frequency)?.label ?? frequency;
  }

  trackById(_index: number, item: IndicatorListItemResponse): number {
    return item.id;
  }

  private loadReferences(): void {
    this.loading = true;

    const emptyUsers = { total: 0, page: 1, pageSize: 300, items: [] as UserResponse[] };
    const emptyProcesses = { total: 0, pageNumber: 1, pageSize: 300, items: [] as ProcessListItemResponse[] };

    const usersRequest$ = this.canLoadUsers
      ? this.userService.getAll(1, 300).pipe(catchError(() => of(emptyUsers)))
      : of(emptyUsers);

    forkJoin({
      users: usersRequest$,
      processes: this.processService.getProcesses({ pageNumber: 1, pageSize: 300 }).pipe(catchError(() => of(emptyProcesses)))
    }).subscribe({
      next: ({ users, processes }) => {
        this.users = users.items.filter(user => user.isActive);
        this.processes = processes.items;
        this.refresh();
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les references du module KPI.');
      }
    });
  }

  private refresh(): void {
    this.loading = true;

    forkJoin({
      page: this.indicatorService.getIndicators(this.buildQuery()),
      stats: this.indicatorService.getIndicatorStatistics()
    }).subscribe({
      next: ({ page, stats }) => {
        this.applyPage(page);
        this.statistics = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Chargement des indicateurs impossible.');
      }
    });
  }

  private applyPage(page: PagedIndicatorResponse): void {
    this.items = page.items;
    this.total = page.total;
    this.pageNumber = page.pageNumber;
    this.pageSize = page.pageSize;
  }

  private buildQuery(): GetIndicatorsQueryRequest {
    const raw = this.filtersForm.getRawValue();
    const alertFilter = raw.isInAlert === 'true'
      ? true
      : raw.isInAlert === 'false'
        ? false
        : null;

    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: raw.search?.trim() || undefined,
      status: raw.status || undefined,
      measurementFrequency: raw.measurementFrequency || undefined,
      processId: raw.processId ?? undefined,
      responsibleUserId: raw.responsibleUserId ?? undefined,
      isInAlert: alertFilter
    };
  }
}
