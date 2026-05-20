import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService } from '../../users/services/user.service';
import {
  DEPARTMENT_STATUS_OPTIONS,
  DepartmentResponse,
  DepartmentStatus
} from '../models/department.models';
import { DepartmentService } from '../services/department.service';

@Component({
  selector: 'app-department-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: './department-form.component.html',
  styleUrls: ['./department-form.component.scss']
})
export class DepartmentFormComponent implements OnInit {
  readonly statusOptions = DEPARTMENT_STATUS_OPTIONS;

  departmentId?: number;
  isEditMode = false;
  isLoading = false;
  isSaving = false;
  users: UserResponse[] = [];
  currentDepartment: DepartmentResponse | null = null;

  readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(255)]],
    code: ['', [Validators.required, Validators.maxLength(50)]],
    description: [''],
    managerUserId: [null as number | null],
    status: ['ACTIF' as DepartmentStatus, [Validators.required]]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly departmentService: DepartmentService,
    private readonly userService: UserService,
    private readonly notificationService: NotificationService,
    private readonly authService: AuthService
  ) {}

  ngOnInit(): void {
    this.departmentId = this.route.snapshot.paramMap.get('id') ? Number(this.route.snapshot.paramMap.get('id')) : undefined;
    this.isEditMode = !!this.departmentId;

    if (!this.canWrite) {
      this.notificationService.showError('Accès refusé.');
      this.router.navigate(['/departments']);
      return;
    }

    this.loadInitialData();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE']);
  }

  get title(): string {
    return this.isEditMode ? 'Modifier le département' : 'Créer un département';
  }

  get submitLabel(): string {
    return this.isEditMode ? 'Enregistrer les modifications' : 'Créer le département';
  }

  get departmentStats(): string {
    return this.currentDepartment
      ? `${this.currentDepartment.usersCount} utilisateurs • ${this.currentDepartment.documentsCount} documents`
      : '';
  }

  get selectedManagerLabel(): string {
    const managerId = this.form.get('managerUserId')?.value;
    if (!managerId) {
      return 'Aucun chef de service';
    }

    const manager = this.users.find((user) => user.id === managerId);
    return manager ? `${manager.firstName} ${manager.lastName} - ${manager.role}` : 'Chef de service sélectionné';
  }

  private loadInitialData(): void {
    this.isLoading = true;

    const users$ = this.userService.getUsers(1, 500);
    const department$ = this.isEditMode && this.departmentId
      ? this.departmentService.getDepartmentById(this.departmentId)
      : of(null);

    forkJoin({
      department: department$,
      users: users$
    }).subscribe({
      next: (result) => {
        this.users = result.users.items.sort((left, right) => {
          const leftName = `${left.lastName} ${left.firstName}`.toLowerCase();
          const rightName = `${right.lastName} ${right.firstName}`.toLowerCase();
          return leftName.localeCompare(rightName);
        });

        if (result.department) {
          this.currentDepartment = result.department.department;
          this.form.patchValue({
            name: result.department.department.name,
            code: result.department.department.code,
            description: result.department.department.description || '',
            managerUserId: result.department.department.managerUserId ?? null,
            status: result.department.department.status
          });
        }

        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notificationService.showError('Impossible de charger le formulaire département.');
        this.router.navigate(['/departments']);
      }
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const payload = {
      name: value.name?.trim() || '',
      code: value.code?.trim().toUpperCase() || '',
      description: value.description?.trim() || null,
      managerUserId: value.managerUserId ?? null,
      status: value.status as DepartmentStatus
    };

    this.isSaving = true;

    const request$ = this.isEditMode && this.departmentId
      ? this.departmentService.updateDepartment(this.departmentId, payload)
      : this.departmentService.createDepartment(payload);

    request$.subscribe({
      next: (department) => {
        this.notificationService.showSuccess(this.isEditMode ? 'Département mis à jour.' : 'Département créé.');
        this.isSaving = false;
        this.router.navigate(['/departments', department.id]);
      },
      error: (error) => {
        this.isSaving = false;
        const message = error?.error?.message || 'Erreur lors de la sauvegarde.';
        this.notificationService.showError(message);
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/departments']);
  }
}
