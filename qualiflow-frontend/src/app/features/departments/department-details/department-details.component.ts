import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { DepartmentDocumentResponse, DepartmentDetailsResponse, DepartmentUserResponse } from '../models/department.models';
import { DepartmentService } from '../services/department.service';
import { DocumentListItemResponse } from '../../documents/models/document.models';
import { DocumentService } from '../../documents/services/document.service';
import { UserResponse, UserService } from '../../users/services/user.service';

@Component({
  selector: 'app-department-details',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatTableModule
  ],
  templateUrl: './department-details.component.html',
  styleUrls: ['./department-details.component.scss']
})
export class DepartmentDetailsComponent implements OnInit {
  readonly userColumns = ['name', 'email', 'role', 'status'];
  readonly documentColumns = ['code', 'title', 'type', 'docStatus', 'actions'];

  loading = false;
  savingUsers = false;
  savingDocuments = false;
  departmentId = 0;
  details: DepartmentDetailsResponse | null = null;
  allUsers: UserResponse[] = [];
  allDocuments: DocumentListItemResponse[] = [];

  readonly usersForm = this.fb.group({
    userIds: this.fb.control<number[]>([], { nonNullable: true })
  });

  readonly documentsForm = this.fb.group({
    documentIds: this.fb.control<number[]>([], { nonNullable: true })
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly departmentService: DepartmentService,
    private readonly userService: UserService,
    private readonly documentService: DocumentService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
  ) { }

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    const parsedId = rawId ? Number(rawId) : Number.NaN;

    if (Number.isNaN(parsedId)) {
      this.notificationService.showError('Identifiant de département invalide.');
      this.router.navigate(['/departments']);
      return;
    }

    this.departmentId = parsedId;
    this.loadData();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE']);
  }

  get canLoadAllUsers(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get department(): DepartmentDetailsResponse['department'] | null {
    return this.details?.department ?? null;
  }

  get users(): DepartmentUserResponse[] {
    return this.details?.users ?? [];
  }

  get documents(): DepartmentDocumentResponse[] {
    return this.details?.documents ?? [];
  }

  get stats() {
    return this.details?.statistics ?? null;
  }

  managerInitials(): string {
    if (!this.department?.managerFullName) {
      return '--';
    }

    const parts = this.department.managerFullName
      .split(' ')
      .map((part) => part.trim())
      .filter((part) => part.length > 0);

    return parts
      .slice(0, 2)
      .map((part) => part.charAt(0).toUpperCase())
      .join('') || '--';
  }

  loadData(): void {
    this.loading = true;

    forkJoin({
      details: this.departmentService.getDepartmentById(this.departmentId),
      users: this.canLoadAllUsers
        ? this.userService.getUsers(1, 500).pipe(catchError(() => of({ items: [] as UserResponse[] })))
        : of({ items: [] as UserResponse[] }),
      documentsPage: this.documentService.getDocuments({ pageNumber: 1, pageSize: 500 })
    }).subscribe({
      next: ({ details, users, documentsPage }) => {
        this.details = details;
        this.allUsers = this.canLoadAllUsers
          ? users.items
          : details.users.map(user => ({
              id: user.id,
              firstName: user.firstName,
              lastName: user.lastName,
              email: user.email,
              role: user.role,
              isActive: user.isActive,
              organizationId: user.organizationId ?? undefined,
              departmentId: user.departmentId ?? null,
              createdAt: user.createdAt
            }));
        this.allDocuments = documentsPage.items;

        this.usersForm.patchValue({
          userIds: details.users.map((user) => user.id)
        });

        this.documentsForm.patchValue({
          documentIds: details.documents.map((document) => document.id)
        });

        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger le département.');
        this.router.navigate(['/departments']);
      }
    });
  }

  editDepartment(): void {
    this.router.navigate(['/departments', this.departmentId, 'edit']);
  }

  toggleStatus(): void {
    if (!this.department) {
      return;
    }

    const nextStatusText = this.department.status === 'ACTIF' ? 'désactiver' : 'réactiver';
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Changer le statut',
        message: `Confirmer la demande pour ${nextStatusText} le département ${this.department.code} ?`,
        confirmText: 'Confirmer',
        cancelText: 'Annuler',
        type: 'info'
      }
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.departmentService.toggleStatus(this.departmentId).subscribe({
        next: () => {
          this.notificationService.showSuccess('Statut mis à jour.');
          this.loadData();
        },
        error: () => {
          this.notificationService.showError('Modification impossible.');
        }
      });
    });
  }

  deleteDepartment(): void {
    if (!this.department) {
      return;
    }

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer le département',
        message: `Confirmer la suppression du département ${this.department.code} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.departmentService.deleteDepartment(this.departmentId).subscribe({
        next: () => {
          this.notificationService.showSuccess('Département supprimé.');
          this.router.navigate(['/departments']);
        },
        error: (error) => {
          const message = error?.error?.message || 'Suppression impossible.';
          this.notificationService.showError(message);
        }
      });
    });
  }

  saveUsers(): void {
    const selectedIds = this.usersForm.getRawValue().userIds;
    this.savingUsers = true;

    this.departmentService.assignUsers(this.departmentId, { userIds: selectedIds }).subscribe({
      next: () => {
        this.notificationService.showSuccess('Utilisateurs affectés.');
        this.loadData();
        this.savingUsers = false;
      },
      error: (error) => {
        this.savingUsers = false;
        const message = error?.error?.message || 'Affectation des utilisateurs impossible.';
        this.notificationService.showError(message);
      }
    });
  }

  saveDocuments(): void {
    const selectedIds = this.documentsForm.getRawValue().documentIds;
    this.savingDocuments = true;

    this.departmentService.assignDocuments(this.departmentId, { documentIds: selectedIds }).subscribe({
      next: () => {
        this.notificationService.showSuccess('Documents affectés.');
        this.loadData();
        this.savingDocuments = false;
      },
      error: (error) => {
        this.savingDocuments = false;
        const message = error?.error?.message || 'Affectation des documents impossible.';
        this.notificationService.showError(message);
      }
    });
  }

  back(): void {
    this.router.navigate(['/departments']);
  }

  isUserSelected(userId: number): boolean {
    return (this.usersForm.getRawValue().userIds || []).includes(userId);
  }

  isDocumentSelected(documentId: number): boolean {
    return (this.documentsForm.getRawValue().documentIds || []).includes(documentId);
  }

  viewDocument(documentId: number): void {
    this.router.navigate(['/documents', documentId]);
  }

  downloadDocument(document: DepartmentDocumentResponse): void {
    this.documentService.downloadLatest(document.id).subscribe({
      next: ({ blob, version }) => {
        const sourceName = version.originalFileName ?? version.fileName ?? undefined;
        const fileName = this.buildDownloadFileName(document.code, version.versionNumber, sourceName);
        this.saveBlob(blob, fileName);
      },
      error: () => {
        this.notificationService.showError('Telechargement impossible.');
      }
    });
  }

  managerLabel(): string {
    if (!this.department) {
      return 'Non affecté';
    }

    return this.department.managerFullName || 'Non affecté';
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const objectUrl = URL.createObjectURL(blob);
    const link = window.document.createElement('a');
    link.href = objectUrl;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(objectUrl);
  }

  private buildDownloadFileName(code: string, version: string, sourceName?: string): string {
    const safeCode = (code || 'document').trim();
    const safeVersion = (version || 'current').trim();
    const extension = this.extractExtension(sourceName) ?? 'bin';
    return `${safeCode}_${safeVersion}.${extension}`;
  }

  private extractExtension(fileName?: string): string | null {
    if (!fileName) {
      return null;
    }

    const dotIndex = fileName.lastIndexOf('.');
    if (dotIndex <= 0 || dotIndex === fileName.length - 1) {
      return null;
    }

    return fileName.slice(dotIndex + 1).toLowerCase();
  }
}
