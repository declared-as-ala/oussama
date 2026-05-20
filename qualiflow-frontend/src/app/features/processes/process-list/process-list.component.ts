import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTableModule } from '@angular/material/table';
import { catchError, forkJoin, of } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserResponse, UserService } from '../../../core/services/user.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  PagedProcessResponse,
  PROCESS_STATUS_OPTIONS,
  PROCESS_TYPE_OPTIONS,
  ProcessListItemResponse,
  ProcessQueryParams,
  ProcessStatus,
  ProcessType
} from '../models/process.models';
import { ProcessService } from '../services/process.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

type ProcessTone = 'good' | 'warning' | 'danger';

@Component({
  selector: 'app-process-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule,
    MatDialogModule,
    MatTooltipModule,
    MatTableModule,
    TranslatePipe
  ],
  templateUrl: './process-list.component.html',
  styleUrls: ['./process-list.component.scss']
})
export class ProcessListComponent implements OnInit {
  readonly typeOptions = PROCESS_TYPE_OPTIONS;
  readonly statusOptions = PROCESS_STATUS_OPTIONS;

  readonly filtersForm = this.fb.group({
    search: [''],
    type: [''],
    status: [''],
    pilotUserId: [null as number | null]
  });

  loading = false;
  showFilters = false;
  items: ProcessListItemResponse[] = [];
  total = 0;
  pageNumber = 1;
  pageSize = 10;
  pilots: UserResponse[] = [];
  viewMode: 'cards' | 'table' = 'table';

  constructor(
    private readonly fb: FormBuilder,
    private readonly processService: ProcessService,
    private readonly userService: UserService,
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

    const canLoadPilots = this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
    const usersRequest = canLoadPilots
      ? this.userService.getAll(1, 300).pipe(catchError(() => of(emptyUsersResponse)))
      : of(emptyUsersResponse);

    forkJoin({
      users: usersRequest,
      processes: this.processService.getProcesses(this.buildQuery())
    }).subscribe({
      next: ({ users, processes }) => {
        this.pilots = users.items.filter(user => user.isActive);
        this.applyProcessPage(processes);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les processus.');
      }
    });
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get activeCount(): number {
    return this.items.filter(item => item.status === 'ACTIF').length;
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  refresh(): void {
    this.loading = true;
    this.processService.getProcesses(this.buildQuery()).subscribe({
      next: (response) => {
        this.applyProcessPage(response);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Erreur lors du chargement des processus.');
      }
    });
  }

  onSearch(): void {
    this.pageNumber = 1;
    this.refresh();
  }

  clearFilters(): void {
    this.filtersForm.reset({
      search: '',
      type: '',
      status: '',
      pilotUserId: null
    });
    this.pageNumber = 1;
    this.refresh();
  }

  onPageChanged(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.refresh();
  }

  createProcess(): void {
    this.router.navigate(['/processes/new']);
  }

  openMap(): void {
    this.router.navigate(['/processes/map']);
  }

  viewProcess(process: ProcessListItemResponse): void {
    this.router.navigate(['/processes', process.id]);
  }

  editProcess(process: ProcessListItemResponse): void {
    this.router.navigate(['/processes', process.id, 'edit']);
  }

  toggleStatus(process: ProcessListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Changer le statut',
        message: `Basculer le statut du processus ${process.code} ?`,
        confirmText: 'Confirmer',
        cancelText: 'Annuler',
        type: 'info'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.processService.toggleProcessStatus(process.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Statut du processus mis a jour.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Impossible de changer le statut.');
        }
      });
    });
  }

  deleteProcess(process: ProcessListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer le processus',
        message: `Confirmer la suppression de ${process.code} - ${process.name} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.processService.deleteProcess(process.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Processus supprime avec succes.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Suppression impossible.');
        }
      });
    });
  }

  getTypeLabel(type: ProcessType): string {
    return this.typeOptions.find(option => option.value === type)?.label ?? type;
  }

  getStatusLabel(status: ProcessStatus): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? status;
  }

  getTypeDescription(type: ProcessType): string {
    switch (type) {
      case 'PILOTAGE':
        return 'Processus de pilotage';
      case 'REALISATION':
        return 'Processus operationnel';
      case 'SUPPORT':
        return 'Processus support';
      default:
        return 'Processus';
    }
  }

  getScore(item: ProcessListItemResponse): number {
    let score = item.status === 'ACTIF' ? 78 : 55;

    switch (item.type) {
      case 'PILOTAGE':
        score += 10;
        break;
      case 'REALISATION':
        score += 4;
        break;
      case 'SUPPORT':
        score -= 6;
        break;
    }

    score += item.pilotUserId ? 6 : -5;
    score += (this.getSeed(item.code) % 9) - 4;

    return Math.min(97, Math.max(48, score));
  }

  getTone(item: ProcessListItemResponse): ProcessTone {
    const score = this.getScore(item);
    if (score >= 85) {
      return 'good';
    }

    if (score >= 70) {
      return 'warning';
    }

    return 'danger';
  }

  getToneLabel(item: ProcessListItemResponse): string {
    const tone = this.getTone(item);
    if (tone === 'good') {
      return 'Conforme';
    }

    if (tone === 'warning') {
      return 'A surveiller';
    }

    return 'Non conforme';
  }

  getToneIcon(item: ProcessListItemResponse): string {
    const tone = this.getTone(item);
    if (tone === 'good') {
      return '.';
    }

    if (tone === 'warning') {
      return '!';
    }

    return 'x';
  }

  getMetaText(item: ProcessListItemResponse): string {
    const seed = this.getSeed(item.code);
    const objectifs = 2 + (seed % 7);
    const indicateurs = 3 + ((seed * 3) % 9);
    return `${objectifs} objectifs · ${indicateurs} indicateurs`;
  }

  trackByProcessId(_index: number, item: ProcessListItemResponse): number {
    return item.id;
  }

  private applyProcessPage(page: PagedProcessResponse): void {
    this.items = page.items;
    this.total = page.total;
    this.pageNumber = page.pageNumber;
    this.pageSize = page.pageSize;
  }

  private buildQuery(): ProcessQueryParams {
    const raw = this.filtersForm.getRawValue();

    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: raw.search?.trim() || undefined,
      type: (raw.type as ProcessType | '') || undefined,
      status: (raw.status as ProcessStatus | '') || undefined,
      pilotUserId: raw.pilotUserId ?? undefined
    };
  }

  private getSeed(value: string): number {
    return Array.from(value).reduce((acc, char) => acc + char.charCodeAt(0), 0);
  }
}
