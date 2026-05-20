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
import { catchError, forkJoin, of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService } from '../../../core/services/user.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ProcessService } from '../../processes/services/process.service';
import { ProcessListItemResponse } from '../../processes/models/process.models';
import {
  PagedProcedureResponse,
  PROCEDURE_STATUS_OPTIONS,
  ProcedureListItemResponse,
  ProcedureQueryParams,
  ProcedureStatus
} from '../models/procedure.models';
import { ProcedureService } from '../services/procedure.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-procedure-list',
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
    TranslatePipe
  ],
  templateUrl: './procedure-list.component.html',
  styleUrls: ['./procedure-list.component.scss']
})
export class ProcedureListComponent implements OnInit {
  readonly statusOptions = PROCEDURE_STATUS_OPTIONS;
  readonly displayedColumns: string[] = ['code', 'title', 'process', 'responsible', 'status', 'createdAt', 'actions'];

  readonly filtersForm = this.fb.group({
    search: [''],
    processId: [null as number | null],
    status: ['' as ProcedureStatus | ''],
    responsibleUserId: [null as number | null]
  });

  loading = false;
  showFilters = true;
  procedures: ProcedureListItemResponse[] = [];
  processes: ProcessListItemResponse[] = [];
  responsibles: UserResponse[] = [];
  total = 0;
  pageNumber = 1;
  pageSize = 10;
  viewMode: 'cards' | 'table' = 'table';

  constructor(
    private readonly fb: FormBuilder,
    private readonly procedureService: ProcedureService,
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
    const usersRequest$ = this.canWrite
      ? this.userService.getAll(1, 300).pipe(catchError(() => of(emptyUsersResponse)))
      : of(emptyUsersResponse);

    forkJoin({
      users: usersRequest$,
      processes: this.processService.getProcesses({ pageNumber: 1, pageSize: 300 })
    }).subscribe({
      next: ({ users, processes }) => {
        this.responsibles = users.items.filter(user => user.isActive);
        this.processes = processes.items;
        this.refresh();
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les filtres des procedures.');
      }
    });
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get activeCount(): number {
    return this.procedures.filter(item => item.status === 'ACTIF').length;
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
      processId: null,
      status: '',
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

  refresh(): void {
    this.loading = true;

    this.procedureService.getProcedures(this.buildQuery()).subscribe({
      next: (page) => {
        this.applyPage(page);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Erreur lors du chargement des procedures.');
      }
    });
  }

  createProcedure(): void {
    this.router.navigate(['/procedures/new']);
  }

  viewProcedure(item: ProcedureListItemResponse): void {
    this.router.navigate(['/procedures', item.id]);
  }

  editProcedure(item: ProcedureListItemResponse): void {
    this.router.navigate(['/procedures', item.id, 'edit']);
  }

  toggleStatus(item: ProcedureListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Changer le statut',
        message: `Basculer le statut de la procedure ${item.code} ?`,
        confirmText: 'Confirmer',
        cancelText: 'Annuler',
        type: 'info'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.procedureService.toggleProcedureStatus(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Statut de la procedure mis a jour.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Impossible de changer le statut.');
        }
      });
    });
  }

  deleteProcedure(item: ProcedureListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer la procedure',
        message: `Confirmer la suppression de ${item.code} - ${item.title} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.procedureService.deleteProcedure(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Procedure supprimee avec succes.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Suppression impossible.');
        }
      });
    });
  }

  getStatusLabel(status: ProcedureStatus): string {
    return status === 'ACTIF' ? 'Actif' : 'Inactif';
  }

  trackByProcedureId(_index: number, item: ProcedureListItemResponse): number {
    return item.id;
  }

  private applyPage(page: PagedProcedureResponse): void {
    this.procedures = page.items;
    this.total = page.total;
    this.pageNumber = page.pageNumber;
    this.pageSize = page.pageSize;
  }

  private buildQuery(): ProcedureQueryParams {
    const raw = this.filtersForm.getRawValue();

    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: raw.search?.trim() || undefined,
      processId: raw.processId ?? undefined,
      status: (raw.status as ProcedureStatus | '') || undefined,
      responsibleUserId: raw.responsibleUserId ?? undefined
    };
  }
}
