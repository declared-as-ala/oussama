import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NotificationService } from '../../../core/services/notification.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  ORGANIZATION_STATUS_OPTIONS,
  ORGANIZATION_TYPE_OPTIONS,
  OrganizationResponse,
  OrganizationUserSummaryResponse
} from '../models/organization.models';
import { OrganizationService } from '../services/organization.service';

@Component({
  selector: 'app-super-admin-organization-details',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatDialogModule
  ],
  templateUrl: './organization-details.component.html',
  styleUrls: ['./organization-details.component.scss']
})
export class OrganizationDetailsComponent implements OnInit {
  readonly displayedAdminColumns: string[] = ['name', 'email', 'status', 'createdAt'];
  readonly displayedUserColumns: string[] = ['name', 'email', 'role', 'status', 'createdAt', 'actions'];
  readonly roleOptions: string[] = ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR'];

  loading = false;
  creatingUser = false;
  showCreateUserForm = false;
  createUserForm: FormGroup;
  organization: OrganizationResponse | null = null;
  organizationUsers: OrganizationUserSummaryResponse[] = [];
  roleDraftByUserId: Record<number, string> = {};

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly organizationService: OrganizationService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
  ) {
    this.createUserForm = this.fb.group({
      firstName: ['', [Validators.required, Validators.minLength(2)]],
      lastName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      role: ['UTILISATEUR', [Validators.required]],
      function: [''],
      department: ['']
    });
  }

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    const id = rawId ? Number(rawId) : Number.NaN;

    if (Number.isNaN(id)) {
      this.notificationService.showError('Identifiant organisation invalide.');
      this.router.navigate(['/super-admin/organizations']);
      return;
    }

    this.loading = true;
    this.organizationService.getOrganizationById(id).subscribe({
      next: (response) => {
        this.organization = response;
        this.loadOrganizationUsers(id);
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger l organisation.');
        this.router.navigate(['/super-admin/organizations']);
      }
    });
  }

  private loadOrganizationUsers(organizationId: number): void {
    this.organizationService.getOrganizationUsers(organizationId).subscribe({
      next: (result) => {
        this.organizationUsers = result.items;
        this.roleDraftByUserId = result.items.reduce<Record<number, string>>((acc, user) => {
          acc[user.id] = user.role;
          return acc;
        }, {});
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les utilisateurs de cette organisation.');
      }
    });
  }

  backToList(): void {
    this.router.navigate(['/super-admin/organizations']);
  }

  editOrganization(): void {
    if (!this.organization) {
      return;
    }

    this.router.navigate(['/super-admin/organizations', this.organization.id, 'edit']);
  }

  toggleStatus(): void {
    if (!this.organization) return;

    const nextStatus = this.organization.status === 'ACTIF' ? 'SUSPENDUE' : 'ACTIF';

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Changer le statut',
        message: `Voulez-vous passer ${this.organization.name} au statut ${nextStatus} ?`,
        confirmText: 'Confirmer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;

      this.organizationService.toggleOrganizationStatus(this.organization!.id, { status: nextStatus }).subscribe({
        next: (response) => {
          this.organization = response;
          this.notificationService.showSuccess('Statut mis à jour.');
        },
        error: () => this.notificationService.showError('Erreur lors du changement de statut.')
      });
    });
  }

  deleteOrganization(): void {
    if (!this.organization) return;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer l\'organisation',
        message: `Êtes-vous sûr de vouloir supprimer définitivement ${this.organization.name} ? Cette action est irréversible.`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;

      this.organizationService.deleteOrganization(this.organization!.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Organisation supprimée.');
          this.router.navigate(['/super-admin/organizations']);
        },
        error: () => this.notificationService.showError('Erreur lors de la suppression.')
      });
    });
  }

  getTypeLabel(type: string | null | undefined): string {
    if (!type) {
      return 'N/A';
    }

    return ORGANIZATION_TYPE_OPTIONS.find(item => item.value === type)?.label ?? type;
  }

  getStatusLabel(status: string): string {
    return ORGANIZATION_STATUS_OPTIONS.find(item => item.value === status)?.label ?? status;
  }

  saveRole(user: OrganizationUserSummaryResponse): void {
    if (!this.organization) {
      return;
    }

    const nextRole = this.roleDraftByUserId[user.id];
    if (!nextRole || nextRole === user.role) {
      return;
    }

    this.organizationService.changeOrganizationUserRole(this.organization.id, user.id, nextRole).subscribe({
      next: () => {
        user.role = nextRole;
        this.notificationService.showSuccess('Role utilisateur mis a jour.');
      },
      error: () => {
        this.roleDraftByUserId[user.id] = user.role;
        this.notificationService.showError('Echec de mise a jour du role utilisateur.');
      }
    });
  }

  toggleCreateUserForm(): void {
    this.showCreateUserForm = !this.showCreateUserForm;
  }

  createUser(): void {
    if (!this.organization) {
      return;
    }

    if (this.createUserForm.invalid) {
      this.createUserForm.markAllAsTouched();
      return;
    }

    const raw = this.createUserForm.value;
    this.creatingUser = true;

    this.organizationService.createOrganizationUser(this.organization.id, {
      firstName: raw.firstName,
      lastName: raw.lastName,
      email: raw.email,
      password: raw.password,
      role: raw.role,
      function: raw.function || null,
      department: raw.department || null
    }).subscribe({
      next: () => {
        this.notificationService.showSuccess('Utilisateur créé et email envoyé.');
        this.createUserForm.reset({
          firstName: '',
          lastName: '',
          email: '',
          password: '',
          role: 'UTILISATEUR',
          function: '',
          department: ''
        });
        this.showCreateUserForm = false;
        this.loadOrganizationUsers(this.organization!.id);
      },
      error: () => {
        this.notificationService.showError('Création utilisateur impossible.');
        this.creatingUser = false;
      },
      complete: () => {
        this.creatingUser = false;
      }
    });
  }
}
