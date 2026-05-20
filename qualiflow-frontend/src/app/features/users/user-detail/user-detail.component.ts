import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { forkJoin, Observable } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { UserResponse, UserRole, UserService } from '../services/user.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-user-detail',
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
    MatChipsModule,
    MatDividerModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './user-detail.component.html',
  styleUrls: ['./user-detail.component.scss']
})
export class UserDetailComponent implements OnInit {
  user?: UserResponse;
  userForm: FormGroup;
  isEditMode = false;
  isLoading = false;
  isSaving = false;
  userId: number = 0;

  readonly roleOptions: Array<{ value: UserRole; label: string }> = [
    { value: 'ADMIN_ORG', label: 'Admin org' },
    { value: 'RESPONSABLE_QUALITE', label: 'Responsable qualite' },
    { value: 'CHEF_SERVICE', label: 'Chef service' },
    { value: 'UTILISATEUR', label: 'Utilisateur' }
  ];

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly userService: UserService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService
  ) {
    this.userForm = this.fb.group({
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
      role: ['', [Validators.required]],
      function: [''],
      department: [''],
      isActive: [true]
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.userId = Number(id);
      this.loadUser();
    } else {
      this.notificationService.showError('ID utilisateur manquant.');
      this.goBack();
    }
  }

  loadUser(): void {
    this.isLoading = true;
    this.userService.getUserById(this.userId).subscribe({
      next: (user) => {
        this.user = user;
        this.patchForm(user);
        this.isLoading = false;
      },
      error: () => {
        this.notificationService.showError('Utilisateur non trouvé.');
        this.isLoading = false;
        this.goBack();
      }
    });
  }

  private patchForm(user: UserResponse): void {
    this.userForm.patchValue({
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      role: user.role,
      function: user.function || '',
      department: user.department || '',
      isActive: user.isActive
    });
  }

  toggleEditMode(): void {
    this.isEditMode = !this.isEditMode;
    if (!this.isEditMode && this.user) {
      this.patchForm(this.user);
    }
  }

  saveChanges(): void {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    const formData = this.userForm.value;
    const requests: Observable<any>[] = [];

    // Core info update
    requests.push(this.userService.updateUser(this.userId, {
      firstName: formData.firstName,
      lastName: formData.lastName,
      email: formData.email,
      function: formData.function,
      department: formData.department
    }));

    // Role update if changed
    if (formData.role !== this.user?.role) {
      requests.push(this.userService.changeRole(this.userId, { role: formData.role }));
    }

    // Status toggle if changed
    if (formData.isActive !== this.user?.isActive) {
      requests.push(this.userService.toggleStatus(this.userId, formData.isActive));
    }

    forkJoin(requests).subscribe({
      next: () => {
        const currentUser = this.authService.getCurrentUser();
        if (currentUser?.id === this.userId) {
          this.authService.getProfile().subscribe({
            next: () => {
              this.notificationService.showSuccess('Utilisateur mis à jour avec succès.');
              this.isSaving = false;
              this.isEditMode = false;
              this.loadUser();
            },
            error: () => {
              this.notificationService.showSuccess('Utilisateur mis à jour avec succès.');
              this.isSaving = false;
              this.isEditMode = false;
              this.loadUser();
            }
          });
          return;
        }

        this.notificationService.showSuccess('Utilisateur mis à jour avec succès.');
        this.isSaving = false;
        this.isEditMode = false;
        this.loadUser();
      },
      error: (error) => {
        console.error('Update error:', error);
        const message = error.error?.message || 'Erreur lors de la mise à jour.';
        this.notificationService.showError(message);
        this.isSaving = false;
      }
    });
  }

  isAdminOrg(): boolean {
    return this.authService.hasRole('ADMIN_ORG');
  }

  formatDate(dateString?: string): string {
    if (!dateString) return 'Jamais';
    return new Date(dateString).toLocaleDateString('fr-FR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  goBack(): void {
    this.router.navigate(['/users']);
  }
}
