import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  DocumentActionLogResponse,
  DocumentDetailsResponse,
  DocumentStatus,
  DocumentType,
  DocumentVersionResponse
} from '../models/document.models';
import { DocumentService } from '../services/document.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { VersionStatusDialogComponent } from '../document-versions/version-status-dialog.component';

@Component({
  selector: 'app-document-details',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe
  ],
  templateUrl: './document-details.component.html',
  styleUrls: ['./document-details.component.scss']
})
export class DocumentDetailsComponent implements OnInit {
  readonly displayedVersionColumns: string[] = ['version', 'status', 'file', 'date', 'author', 'verifiedBy', 'validatedBy', 'actions'];
  readonly displayedActionLogColumns: string[] = ['action', 'version', 'user', 'date', 'comment'];

  loading = false;
  actionLogsLoading = false;
  documentId!: number;
  details: DocumentDetailsResponse | null = null;
  actionLogs: DocumentActionLogResponse[] = [];
  creationLog: DocumentActionLogResponse | null = null;
  lastLog: DocumentActionLogResponse | null = null;
  totalActions = 0;
  activeTab = 0;

  setActiveTab(index: number): void {
    this.activeTab = index;
  }

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly documentService: DocumentService,
    private readonly authService: AuthService,
    private readonly dialog: MatDialog,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    const parsedId = rawId ? Number(rawId) : Number.NaN;

    if (Number.isNaN(parsedId)) {
      this.notificationService.showError('Identifiant document invalide.');
      this.router.navigate(['/documents']);
      return;
    }

    this.documentId = parsedId;
    this.loadDetails();
    this.loadActionLogs();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get hasData(): boolean {
    return this.details !== null;
  }

  loadDetails(): void {
    this.loading = true;
    this.documentService.getDocumentById(this.documentId).subscribe({
      next: (details) => {
        this.details = details;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les details du document.');
        this.router.navigate(['/documents']);
      }
    });
  }

  backToList(): void {
    this.router.navigate(['/documents']);
  }

  editDocument(): void {
    this.router.navigate(['/documents', this.documentId, 'edit']);
  }

  openVersions(): void {
    this.router.navigate(['/documents', this.documentId, 'versions']);
  }

  deleteDocument(event?: Event): void {
    this.blurEventTarget(event);

    if (!this.details?.document) {
      return;
    }

    const documentRef = this.details.document.code || this.details.document.title;
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer le document',
        message: `Confirmer la suppression logique de ${documentRef} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.documentService.deleteDocument(this.documentId).subscribe({
        next: () => {
          this.notificationService.showSuccess('Document supprime.');
          this.router.navigate(['/documents']);
        },
        error: () => {
          this.notificationService.showError('Suppression impossible.');
        }
      });
    });
  }

  downloadCurrent(): void {
    this.documentService.downloadLatest(this.documentId).subscribe({
      next: ({ blob, version }) => {
        const code = this.details?.document.code ?? 'document';
        const sourceName = version.originalFileName ?? version.fileName ?? undefined;
        const fileName = this.buildDownloadFileName(code, version.versionNumber, sourceName);
        this.saveBlob(blob, fileName);
        this.loadActionLogs();
      },
      error: () => {
        this.notificationService.showError('Telechargement impossible.');
      }
    });
  }

  updateVersionStatus(version: DocumentVersionResponse): void {
    if (!this.canWrite) {
      return;
    }

    const dialogRef = this.dialog.open(VersionStatusDialogComponent, {
      data: {
        versionNumber: version.versionNumber,
        currentStatus: version.status,
        currentComment: version.revisionComment || ''
      },
      width: '500px'
    });

    dialogRef.afterClosed().subscribe(result => {
      if (!result) {
        return;
      }

      const { status, revisionComment } = result;

      this.documentService.updateVersionStatus(this.documentId, version.id, {
        status,
        revisionComment: revisionComment?.trim() || null
      }).subscribe({
        next: () => {
          this.notificationService.showSuccess(status === 'PUBLIE'
            ? 'Version publiée avec succès.'
            : 'Statut de version mis à jour.');
          this.loadDetails();
          this.loadActionLogs();
        },
        error: () => {
          this.notificationService.showError('Mise à jour du statut impossible.');
        }
      });
    });
  }

  downloadVersion(version: DocumentVersionResponse): void {
    this.documentService.downloadVersion(this.documentId, version.id).subscribe({
      next: (blob) => {
        const code = this.details?.document.code ?? 'document';
        const sourceName = version.originalFileName ?? version.fileName ?? undefined;
        const fileName = this.buildDownloadFileName(code, version.versionNumber, sourceName);
        this.saveBlob(blob, fileName);
        this.loadActionLogs();
      },
      error: () => {
        this.notificationService.showError('Telechargement de la version impossible.');
      }
    });
  }

  printCurrent(): void {
    this.documentService.previewCurrentOrLatest(this.documentId).subscribe({
      next: (blob) => {
        const blobUrl = URL.createObjectURL(blob);
        const iframe = window.document.createElement('iframe');
        iframe.style.display = 'none';
        iframe.src = blobUrl;
        window.document.body.appendChild(iframe);
        iframe.contentWindow?.focus();
        iframe.onload = () => {
          iframe.contentWindow?.print();
          setTimeout(() => {
            window.document.body.removeChild(iframe);
            URL.revokeObjectURL(blobUrl);
          }, 2000);
        };
        this.loadActionLogs();
      },
      error: () => {
        this.notificationService.showError('Impression impossible (aucune version disponible).');
      }
    });
  }

  previewCurrent(): void {
    this.documentService.previewCurrentOrLatest(this.documentId).subscribe({
      next: (blob) => {
        const objectUrl = URL.createObjectURL(blob);
        window.open(objectUrl, '_blank', 'noopener');
        setTimeout(() => URL.revokeObjectURL(objectUrl), 120000);
        this.loadActionLogs();
      },
      error: () => {
        this.notificationService.showError('Previsualisation impossible (aucune version disponible).');
      }
    });
  }

  loadActionLogs(): void {
    this.actionLogsLoading = true;
    this.documentService.getActionLogs(this.documentId).subscribe({
      next: (logs) => {
        this.actionLogs = logs;
        this.creationLog = logs.find(log => log.actionType.includes('CREATED') || log.actionType.includes('UPLOADED')) || null;
        this.lastLog = logs.length > 0 ? logs[0] : null;
        this.totalActions = logs.length;
        this.actionLogsLoading = false;
      },
      error: () => {
        this.actionLogs = [];
        this.creationLog = null;
        this.lastLog = null;
        this.totalActions = 0;
        this.actionLogsLoading = false;
        this.notificationService.showError('Impossible de charger le journal d\'actions.');
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
        return 'Approuve';
      case 'PUBLIE':
        return 'Publie';
      case 'EN_REVISION':
        return 'En revision';
      case 'REJETE':
        return 'Rejete';
      case 'PERIME':
        return 'Perime';
      case 'ARCHIVE':
        return 'Archive';
      default:
        return 'Brouillon';
    }
  }

  getActionLabel(actionType: string): string {
    switch (actionType) {
      case 'DOCUMENT_CREATED':
        return 'Document cree';
      case 'DOCUMENT_UPDATED':
        return 'Document modifie';
      case 'DOCUMENT_DELETED':
        return 'Document supprime';
      case 'DOCUMENT_RESTORED':
        return 'Document restaure';
      case 'DOCUMENT_PERMANENTLY_DELETED':
        return 'Suppression definitive';
      case 'DOCUMENT_ACTIVATED':
        return 'Document active';
      case 'DOCUMENT_DEACTIVATED':
        return 'Document desactive';
      case 'VERSION_UPLOADED':
        return 'Version ajoutee';
      case 'VERSION_STATUS_CHANGED':
        return 'Statut modifie';
      case 'CURRENT_VERSION_DOWNLOADED':
        return 'Version courante telechargee';
      case 'VERSION_DOWNLOADED':
        return 'Version telechargee';
      case 'CURRENT_VERSION_PREVIEWED':
        return 'Apercu consulte';
      default:
        return actionType.replace(/_/g, ' ').toLowerCase();
    }
  }

  getActionIcon(actionType: string): string {
    if (actionType.includes('DOWNLOADED')) {
      return 'download';
    }

    if (actionType.includes('PREVIEWED')) {
      return 'visibility';
    }

    if (actionType.includes('DELETED')) {
      return 'delete_outline';
    }

    if (actionType.includes('RESTORED')) {
      return 'restore';
    }

    if (actionType.includes('STATUS')) {
      return 'published_with_changes';
    }

    if (actionType.includes('VERSION')) {
      return 'upload_file';
    }

    if (actionType.includes('UPDATED')) {
      return 'edit';
    }

    return 'history';
  }

  getOwnerInitials(fullName?: string | null): string {
    if (!fullName) {
      return 'NA';
    }

    return fullName
      .split(' ')
      .filter(part => part.length > 0)
      .map(part => part[0])
      .join('')
      .slice(0, 3)
      .toUpperCase();
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
