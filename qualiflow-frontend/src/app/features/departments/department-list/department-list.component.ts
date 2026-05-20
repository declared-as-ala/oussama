import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  DEPARTMENT_STATUS_OPTIONS,
  DepartmentListItemResponse,
  DepartmentStatisticsResponse,
  DepartmentStatus,
  PagedDepartmentResponse
} from '../models/department.models';
import { DepartmentService } from '../services/department.service';

@Component({
  selector: 'app-department-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule
  ],
  templateUrl: './department-list.component.html',
  styleUrls: ['./department-list.component.scss']
})
export class DepartmentListComponent implements OnInit {
  readonly displayedColumnsAdmin: string[] = ['name', 'code', 'manager', 'counts', 'status', 'actions'];
  readonly displayedColumnsUser: string[] = ['name', 'code', 'manager', 'counts', 'status', 'actions'];
  readonly statusOptions = DEPARTMENT_STATUS_OPTIONS;

  departments: DepartmentListItemResponse[] = [];
  statistics: DepartmentStatisticsResponse | null = null;
  total = 0;
  pageNumber = 1;
  pageSize = 10;
  loading = false;
  search = '';
  status: DepartmentStatus | '' = '';
  managerUserId: number | null = null;

  constructor(
    private readonly departmentService: DepartmentService,
    private readonly notificationService: NotificationService,
    private readonly authService: AuthService,
    private readonly dialog: MatDialog,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.loadDepartments();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE']);
  }

  get isUtilisateur(): boolean {
    return this.authService.hasRole('UTILISATEUR');
  }

  get displayedColumns(): string[] {
    return this.isUtilisateur ? this.displayedColumnsUser : this.displayedColumnsAdmin;
  }

  get userDepartmentName(): string {
    return this.authService.getCurrentUser()?.department?.trim() || '';
  }

  loadDepartments(): void {
    this.loading = true;

    forkJoin({
      page: this.departmentService.getDepartments(this.buildQuery()),
      stats: this.departmentService.getStatistics()
    }).subscribe({
      next: ({ page, stats }) => {
        this.applyPage(page);
        this.statistics = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Erreur lors du chargement des departements.');
      }
    });
  }

  onSearch(): void {
    this.pageNumber = 1;
    this.loadDepartments();
  }

  clearFilters(): void {
    this.search = '';
    this.status = '';
    this.managerUserId = null;
    this.pageNumber = 1;
    this.loadDepartments();
  }

  onPageChanged(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadDepartments();
  }

  createDepartment(): void {
    this.router.navigate(['/departments/new']);
  }

  viewDepartment(item: DepartmentListItemResponse): void {
    this.router.navigate(['/departments', item.id]);
  }

  editDepartment(item: DepartmentListItemResponse): void {
    this.router.navigate(['/departments', item.id, 'edit']);
  }

  manageUsers(item: DepartmentListItemResponse): void {
    this.router.navigate(['/departments', item.id], { fragment: 'users' });
  }

  toggleStatus(item: DepartmentListItemResponse): void {
    const nextStatus = item.status === 'ACTIF' ? 'INACTIF' : 'ACTIF';
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Changer le statut',
        message: `Confirmer le passage du departement ${item.code} en ${nextStatus === 'ACTIF' ? 'actif' : 'inactif'} ?`,
        confirmText: 'Confirmer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.departmentService.toggleStatus(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Statut du departement mis a jour.');
          this.loadDepartments();
        },
        error: () => {
          this.notificationService.showError('Impossible de modifier le statut.');
        }
      });
    });
  }

  deleteDepartment(item: DepartmentListItemResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer le departement',
        message: `Confirmer la suppression du departement ${item.code} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.departmentService.deleteDepartment(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Departement supprime.');
          this.loadDepartments();
        },
        error: (error) => {
          const message = error?.error?.message || 'Suppression impossible.';
          this.notificationService.showError(message);
        }
      });
    });
  }

  getStatusLabel(status: DepartmentStatus): string {
    return status === 'ACTIF' ? 'Actif' : 'Inactif';
  }

  trackByDepartmentId(_index: number, item: DepartmentListItemResponse): number {
    return item.id;
  }

  private applyPage(page: PagedDepartmentResponse): void {
    this.departments = page.items;
    this.total = page.total;
    this.pageNumber = page.pageNumber;
    this.pageSize = page.pageSize;
  }

  private buildQuery() {
    const currentUserId = this.authService.getCurrentUser()?.id ?? null;
    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: this.search.trim() || undefined,
      status: this.status || undefined,
      managerUserId: this.managerUserId ?? undefined,
      userId: this.isUtilisateur && currentUserId ? currentUserId : undefined
    };
  }
}
