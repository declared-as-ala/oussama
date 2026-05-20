import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserRole, UserService } from '../services/user.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatTooltipModule
  ],
  templateUrl: './users-list.component.html',
  styleUrls: ['./users-list.component.scss']
})
export class UsersListComponent implements OnInit {
  users: UserResponse[] = [];
  displayedColumns: string[] = ['fullName', 'email', 'role', 'status', 'lastLoginAt', 'createdAt', 'actions'];

  readonly roleOptions: Array<{ value: UserRole; label: string }> = [
    { value: 'ADMIN_ORG', label: 'Admin org' },
    { value: 'RESPONSABLE_QUALITE', label: 'Responsable qualite' },
    { value: 'CHEF_SERVICE', label: 'Chef service' },
    { value: 'UTILISATEUR', label: 'Utilisateur' }
  ];

  loading = false;
  searchTerm = '';
  total = 0;
  page = 1;
  pageSize = 10;
  pendingRoles: Record<number, UserRole> = {};

  constructor(
    private readonly userService: UserService,
    private readonly notificationService: NotificationService,
    private readonly authService: AuthService
  ) { }

  ngOnInit(): void {
    this.loadUsers();
  }

  isAdminOrg(): boolean {
    return this.authService.hasRole('ADMIN_ORG');
  }

  loadUsers(): void {
    this.loading = true;

    const request$ = this.searchTerm.trim().length > 0
      ? this.userService.searchUsers(this.searchTerm.trim(), this.page, this.pageSize)
      : this.userService.getUsers(this.page, this.pageSize);

    request$.subscribe({
      next: (response) => {
        this.users = response.items;
        this.total = response.total;

        this.pendingRoles = {};
        for (const user of this.users) {
          this.pendingRoles[user.id] = (user.role as UserRole);
        }

        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Erreur lors du chargement des utilisateurs de votre organisation.');
      }
    });
  }

  onSearch(): void {
    this.page = 1;
    this.loadUsers();
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.page = 1;
    this.loadUsers();
  }

  onPageChanged(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadUsers();
  }

  saveRole(user: UserResponse): void {
    const nextRole = this.pendingRoles[user.id];
    if (!nextRole) {
      return;
    }

    if (nextRole === user.role) {
      this.notificationService.showInfo('Le role est deja applique.');
      return;
    }

    this.userService.changeRole(user.id, { role: nextRole }).subscribe({
      next: () => {
        this.notificationService.showSuccess('Role modifie avec succes.');
        this.loadUsers();
      },
      error: () => {
        this.notificationService.showError('Modification du role impossible.');
      }
    });
  }

  toggleStatus(user: UserResponse): void {
    const nextStatus = !user.isActive;
    const actionText = nextStatus ? 'activer' : 'desactiver';

    if (!confirm(`Confirmer ${actionText} ${user.firstName} ${user.lastName} ?`)) {
      return;
    }

    this.userService.toggleStatus(user.id, nextStatus).subscribe({
      next: () => {
        this.notificationService.showSuccess('Statut utilisateur mis a jour.');
        this.loadUsers();
      },
      error: () => {
        this.notificationService.showError('Mise a jour du statut impossible.');
      }
    });
  }

  deleteUser(user: UserResponse): void {
    if (!confirm(`Confirmer la suppression DÉFINITIVE de ${user.firstName} ${user.lastName} ?\n\nAttention: Cette action est irréversible et supprimera toutes les données associées qui ne sont pas soumises à des restrictions applicatives.`)) {
      return;
    }

    this.userService.hardDeleteUser(user.id).subscribe({
      next: () => {
        this.notificationService.showSuccess('Utilisateur supprimé définitivement.');
        this.loadUsers();
      },
      error: (error) => {
        console.error('Delete error:', error);
        const message = error.error?.message || 'Une erreur est survenue lors de la suppression définitive.';
        this.notificationService.showError(message);
      }
    });
  }

  getRoleLabel(role: string): string {
    return this.roleOptions.find(option => option.value === role)?.label ?? role;
  }

  formatDate(dateString?: string): string {
    if (!dateString) {
      return 'N/A';
    }

    const date = new Date(dateString);
    return date.toLocaleDateString('fr-FR', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
}
