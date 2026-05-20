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
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  DOCUMENT_STATUS_OPTIONS,
  DocumentListItemResponse,
  DocumentQueryParams,
  DocumentStatisticsResponse,
  DocumentStatus,
  DocumentType,
  PagedDocumentResponse
} from '../models/document.models';
import { DocumentService } from '../services/document.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

type ToolbarTypeFilter = '' | 'MANUEL' | 'PROCEDURE' | 'ENREGISTREMENT' | 'FORMULAIRE';
type ToolbarStatusFilter = '' | 'APPROUVE' | 'PUBLIE' | 'EN_REVISION' | 'REJETE' | 'PERIME';
type ListMode = 'GLOBAL' | 'PENDING_VALIDATION';

@Component({
  selector: 'app-documents-list',
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
    MatTooltipModule,
    MatMenuModule,
    TranslatePipe
  ],
  templateUrl: './documents-list.component.html',
  styleUrls: ['./documents-list.component.scss']
})
export class DocumentsListComponent implements OnInit {
  readonly activeColumns: string[] = ['document', 'type', 'process', 'status', 'revision', 'updatedAt', 'owner', 'actions'];
  readonly trashColumns: string[] = ['document', 'type', 'deletedAt', 'retention', 'owner', 'actions'];
  readonly statusOptions = DOCUMENT_STATUS_OPTIONS;

  readonly filtersForm = this.fb.group({
    search: [''],
    type: ['' as ToolbarTypeFilter],
    status: ['' as ToolbarStatusFilter],
    mineOnly: [false],
    listMode: ['GLOBAL' as ListMode]
  });

  loading = false;
  documents: DocumentListItemResponse[] = [];
  statistics: DocumentStatisticsResponse | null = null;
  total = 0;
  pageNumber = 1;
  pageSize = 10;
  showingTrash = false;

  constructor(
    private readonly fb: FormBuilder,
    private readonly documentService: DocumentService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog,
    private readonly router: Router
  ) { }

  ngOnInit(): void {
    this.refresh();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get canSubmit(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']);
  }

  get isQualityManager(): boolean {
    return this.authService.hasRole('RESPONSABLE_QUALITE');
  }

  get hasPendingReviewFilter(): boolean {
    return this.filtersForm.get('status')?.value === 'EN_REVISION';
  }

  get isPendingValidationMode(): boolean {
    return this.filtersForm.get('listMode')?.value === 'PENDING_VALIDATION';
  }

  get canTrackMyUploads(): boolean {
    return this.authService.hasRole(['UTILISATEUR', 'CHEF_SERVICE']);
  }

  get displayedColumns(): string[] {
    return this.showingTrash ? this.trashColumns : this.activeColumns;
  }

  get isMineOnlyFilterActive(): boolean {
    return this.filtersForm.get('mineOnly')?.value === true;
  }

  refresh(): void {
    this.loading = true;

    if (this.showingTrash) {
      this.documentService.getTrash({
        pageNumber: this.pageNumber,
        pageSize: this.pageSize
      }).subscribe({
        next: page => {
          this.applyPage(page);
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.showError('Erreur lors du chargement de la corbeille.');
        }
      });
      return;
    }

    forkJoin({
      page: this.documentService.getDocuments(this.buildQuery()),
      stats: this.documentService.getDocumentStatistics()
    }).subscribe({
      next: ({ page, stats }) => {
        this.applyPage(page);
        this.statistics = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Erreur lors du chargement des documents.');
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
      mineOnly: false,
      listMode: 'GLOBAL'
    });
    this.pageNumber = 1;
    this.refresh();
  }

  openTrash(): void {
    this.showingTrash = true;
    this.pageNumber = 1;
    this.refresh();
  }

  closeTrash(): void {
    this.showingTrash = false;
    this.pageNumber = 1;
    this.refresh();
  }

  toggleMineOnlyFilter(): void {
    const current = this.filtersForm.get('mineOnly')?.value === true;
    this.filtersForm.patchValue({ mineOnly: !current });
    this.pageNumber = 1;
    this.refresh();
  }

  focusPendingReviews(): void {
    if (this.isQualityManager) {
      this.filtersForm.patchValue({
        listMode: 'PENDING_VALIDATION',
        status: ''
      });
    } else {
      this.filtersForm.patchValue({
        status: 'EN_REVISION'
      });
    }
    this.pageNumber = 1;
    this.refresh();
  }

  showAllDocuments(): void {
    this.filtersForm.patchValue({
      listMode: 'GLOBAL'
    });
    this.pageNumber = 1;
    this.refresh();
  }

  onPageChanged(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.refresh();
  }

  createDocument(): void {
    this.router.navigate(['/documents/new']);
  }

  viewDocument(item: DocumentListItemResponse): void {
    this.router.navigate(['/documents', item.id]);
  }

  editDocument(item: DocumentListItemResponse): void {
    this.router.navigate(['/documents', item.id, 'edit']);
  }

  manageVersions(item: DocumentListItemResponse): void {
    this.router.navigate(['/documents', item.id, 'versions']);
  }

  deleteDocument(item: DocumentListItemResponse, event?: Event): void {
    this.blurEventTarget(event);

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer le document',
        message: `Envoyer ${item.code} dans la corbeille ? Il restera restaurable pendant 30 jours.`,
        confirmText: 'Mettre dans la corbeille',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.documentService.deleteDocument(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Document placé dans la corbeille.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Suppression impossible.');
        }
      });
    });
  }

  restoreDocument(item: DocumentListItemResponse, event?: Event): void {
    this.blurEventTarget(event);

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Restaurer le document',
        message: `Restaurer ${item.code} dans la liste des documents ?`,
        confirmText: 'Restaurer',
        cancelText: 'Annuler',
        type: 'info'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.documentService.restoreDocument(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Document restauré.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Restauration impossible.');
        }
      });
    });
  }

  permanentDeleteDocument(item: DocumentListItemResponse, event?: Event): void {
    this.blurEventTarget(event);

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Suppression définitive',
        message: `Supprimer définitivement ${item.code} ? Cette action est irréversible.`,
        confirmText: 'Supprimer définitivement',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.documentService.permanentDeleteDocument(item.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Document supprimé définitivement.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Suppression définitive impossible.');
        }
      });
    });
  }

  changeStatus(item: DocumentListItemResponse, newStatus: DocumentStatus): void {
    if (item.status === newStatus) {
      return;
    }

    const label = this.getStatusLabel(newStatus);
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Changer le statut',
        message: `Passer le document ${item.code} au statut "${label}" ?`,
        confirmText: 'Confirmer',
        cancelText: 'Annuler',
        type: 'info'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.documentService.updateDocumentStatus(item.id, { status: newStatus }).subscribe({
        next: () => {
          this.notificationService.showSuccess('Statut mis à jour.');
          this.refresh();
        },
        error: () => {
          this.notificationService.showError('Impossible de changer le statut.');
        }
      });
    });
  }

  downloadCurrent(item: DocumentListItemResponse): void {
    this.documentService.downloadLatest(item.id).subscribe({
      next: ({ blob, version }) => {
        const sourceName = version.originalFileName ?? version.fileName ?? item.fileName ?? undefined;
        const fileName = this.buildDownloadFileName(item.code, version.versionNumber, sourceName);
        this.saveBlob(blob, fileName);
      },
      error: () => {
        this.notificationService.showError('Telechargement impossible.');
      }
    });
  }

  getTypeLabel(type: DocumentType): string {
    switch (type) {
      case 'MANUEL':
        return 'Manuel';
      case 'PROCEDURE':
        return 'Procedure';
      case 'ENREGISTREMENT':
        return 'Enregistrement';
      case 'FORMULAIRE':
        return 'Formulaire';
      case 'INSTRUCTION':
        return 'Instruction';
      case 'POLITIQUE':
        return 'Politique';
      default:
        return 'Autre';
    }
  }

  getStatusLabel(status: DocumentStatus): string {
    switch (status) {
      case 'APPROUVE':
        return 'Approuvé';
      case 'PUBLIE':
        return 'Publié';
      case 'EN_REVISION':
        return 'En révision';
      case 'REJETE':
        return 'Rejeté';
      case 'PERIME':
        return 'Périmé';
      case 'ARCHIVE':
        return 'Archivé';
      default:
        return 'Brouillon';
    }
  }

  getExpirationState(item: DocumentListItemResponse): 'VALID' | 'EXPIRING_SOON' | 'EXPIRED' {
    if (item.expirationState) {
      return item.expirationState;
    }

    if (item.status === 'PERIME') {
      return 'EXPIRED';
    }

    if (typeof item.daysToExpiry === 'number') {
      if (item.daysToExpiry < 0) {
        return 'EXPIRED';
      }
      if (item.daysToExpiry <= 30) {
        return 'EXPIRING_SOON';
      }
    }

    return 'VALID';
  }

  getExpirationLabel(item: DocumentListItemResponse): string {
    const state = this.getExpirationState(item);
    if (state === 'EXPIRED') {
      return 'Expiré';
    }

    if (state === 'EXPIRING_SOON') {
      return 'Expire bientôt';
    }

    return 'Valide';
  }

  getTrashRetentionLabel(item: DocumentListItemResponse): string {
    const days = item.daysUntilPermanentDelete ?? 0;
    if (days <= 0) {
      return 'Suppression définitive imminente';
    }

    return `${days} jour${days > 1 ? 's' : ''} restant${days > 1 ? 's' : ''}`;
  }

  getOwnerInitials(fullName?: string | null): string {
    if (!fullName) {
      return 'NA';
    }

    const initials = fullName
      .split(' ')
      .filter(part => part.length > 0)
      .map(part => part[0])
      .join('')
      .toUpperCase();

    return initials || 'NA';
  }

  trackByDocumentId(_index: number, item: DocumentListItemResponse): number {
    return item.id;
  }

  isPendingReview(item: DocumentListItemResponse): boolean {
    return item.status === 'EN_REVISION';
  }

  private applyPage(page: PagedDocumentResponse): void {
    this.documents = page.items;
    this.total = page.total;
    this.pageNumber = page.pageNumber;
    this.pageSize = page.pageSize;
  }

  private buildQuery(): DocumentQueryParams {
    const raw = this.filtersForm.getRawValue();
    const currentUserId = this.authService.getCurrentUser()?.id ?? null;
    const useMineOnly = this.canTrackMyUploads && raw.mineOnly === true && !!currentUserId;

    return {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: raw.search?.trim() || undefined,
      type: (raw.type as DocumentType | '') || undefined,
      status: (raw.status as DocumentStatus | '') || undefined,
      ownerUserId: useMineOnly ? currentUserId : undefined,
      pendingValidationOnly: this.isQualityManager && raw.listMode === 'PENDING_VALIDATION'
    };
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

  private blurEventTarget(event?: Event): void {
    const activeElement = document.activeElement as HTMLElement | null;
    if (activeElement && typeof activeElement.blur === 'function') {
      activeElement.blur();
    }

    const target = event?.target as HTMLElement | null;
    const button = target?.closest('button') as HTMLElement | null;
    if (button && typeof button.blur === 'function') {
      button.blur();
    }
  }
}
