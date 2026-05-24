import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  CreateDocumentVersionRequest,
  DOCUMENT_STATUS_OPTIONS,
  DocumentDetailsResponse,
  DocumentStatus,
  DocumentVersionResponse
} from '../models/document.models';
import { DocumentService } from '../services/document.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { VersionStatusDialogComponent } from './version-status-dialog.component';

@Component({
  selector: 'app-document-versions',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatDialogModule
  ],
  templateUrl: './document-versions.component.html',
  styleUrls: ['./document-versions.component.scss']
})
export class DocumentVersionsComponent implements OnInit {
  readonly statusOptions = DOCUMENT_STATUS_OPTIONS;
  readonly displayedColumns: string[] = ['version', 'status', 'file', 'effectiveDate', 'author', 'verifiedBy', 'validatedBy', 'comment', 'actions'];
  readonly acceptedFileTypes = '.pdf,.docx,.xlsx';
  readonly allowedFileFormatsLabel = 'PDF, Word (.docx) ou Excel (.xlsx)';

  readonly createVersionForm = this.fb.group({
    status: this.fb.nonNullable.control<DocumentStatus>('BROUILLON', Validators.required),
    revisionComment: this.fb.control<string>(''),
    effectiveDate: this.fb.control<string>(this.getTodayAsInputDate()),
    expiryDate: this.fb.control<string>('')
  });

  loading = false;
  submitting = false;
  documentId!: number;
  details: DocumentDetailsResponse | null = null;
  selectedFile: File | null = null;
  isDragging = false;
  statusByVersion: Record<number, DocumentStatus> = {};
  commentByVersion: Record<number, string> = {};
  activeFormTab = 0;

  setFormTab(index: number): void {
    this.activeFormTab = index;
  }

  // Signature Pad
  private signatureCanvasElement?: HTMLCanvasElement;
  private ctx: CanvasRenderingContext2D | null = null;
  private isDrawing = false;

  @ViewChild('signatureCanvas') set signatureCanvas(content: ElementRef<HTMLCanvasElement>) {
    if (content) {
      this.signatureCanvasElement = content.nativeElement;
      this.initCanvas();
    }
  }

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly documentService: DocumentService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
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
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get documentCode(): string {
    return this.details?.document.code ?? 'DOC';
  }

  get versions(): DocumentVersionResponse[] {
    return this.details?.versions ?? [];
  }

  loadDetails(): void {
    this.loading = true;
    this.documentService.getDocumentById(this.documentId).subscribe({
      next: (details) => {
        this.details = details;
        this.hydrateStatusForms(details.versions);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les versions du document.');
        this.router.navigate(['/documents']);
      }
    });
  }

  // File Upload & Dropzone
  onFileSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0] ?? null;
    if (!this.assignSelectedFile(file)) {
      target.value = '';
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = true;
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;

    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      this.assignSelectedFile(event.dataTransfer.files[0]);
    }
  }

  clearSelectedFile(): void {
    this.selectedFile = null;
  }

  private assignSelectedFile(file: File | null): boolean {
    if (!file) {
      this.selectedFile = null;
      return true;
    }

    if (!this.isAllowedDocumentFile(file)) {
      this.selectedFile = null;
      this.notificationService.showWarning(`Format non autorise. Deposez uniquement: ${this.allowedFileFormatsLabel}.`);
      return false;
    }

    this.selectedFile = file;
    return true;
  }

  private isAllowedDocumentFile(file: File): boolean {
    const name = file.name.toLowerCase();
    return name.endsWith('.pdf') || name.endsWith('.docx') || name.endsWith('.xlsx');
  }

  // Signature Pad Logic
  private initCanvas(): void {
    if (!this.signatureCanvasElement) return;
    this.ctx = this.signatureCanvasElement.getContext('2d');
    if (this.ctx) {
      this.ctx.strokeStyle = '#000';
      this.ctx.lineWidth = 2;
      this.ctx.lineCap = 'round';
      this.ctx.lineJoin = 'round';
    }
  }

  startDrawing(event: MouseEvent | TouchEvent): void {
    event.preventDefault();
    this.isDrawing = true;
    const pos = this.getPointerPos(event);
    this.ctx?.beginPath();
    this.ctx?.moveTo(pos.x, pos.y);
  }

  draw(event: MouseEvent | TouchEvent): void {
    if (!this.isDrawing) return;
    event.preventDefault();
    const pos = this.getPointerPos(event);
    this.ctx?.lineTo(pos.x, pos.y);
    this.ctx?.stroke();
  }

  stopDrawing(): void {
    this.isDrawing = false;
  }

  clearSignature(): void {
    if (this.signatureCanvasElement && this.ctx) {
      this.ctx.clearRect(0, 0, this.signatureCanvasElement.width, this.signatureCanvasElement.height);
    }
  }

  private getPointerPos(event: MouseEvent | TouchEvent): { x: number, y: number } {
    if (!this.signatureCanvasElement) return { x: 0, y: 0 };
    const rect = this.signatureCanvasElement.getBoundingClientRect();
    const clientX = 'touches' in event ? (event as TouchEvent).touches[0].clientX : (event as MouseEvent).clientX;
    const clientY = 'touches' in event ? (event as TouchEvent).touches[0].clientY : (event as MouseEvent).clientY;
    return {
      x: clientX - rect.left,
      y: clientY - rect.top
    };
  }

  backToDocument(): void {
    this.router.navigate(['/documents', this.documentId]);
  }

  submitVersion(): void {
    if (!this.canWrite) {
      return;
    }

    if (this.createVersionForm.invalid) {
      this.createVersionForm.markAllAsTouched();
      return;
    }
    if (!this.selectedFile) {
      this.notificationService.showWarning('Veuillez selectionner un fichier avant de creer une version.');
      return;
    }

    const raw = this.createVersionForm.getRawValue();

    // Capture Signature
    let signatureBase64: string | null = null;
    if (this.signatureCanvasElement) {
      const isCanvasEmpty = this.isCanvasBlank(this.signatureCanvasElement);
      if (!isCanvasEmpty) {
        signatureBase64 = this.signatureCanvasElement.toDataURL('image/png');
      }
    }

    const payload: CreateDocumentVersionRequest = {
      status: raw.status,
      revisionComment: raw.revisionComment?.trim() || null,
      effectiveDate: raw.effectiveDate || this.getTodayAsInputDate(),
      expiryDate: raw.expiryDate || null,
      signature: signatureBase64
    };

    this.submitting = true;

    const request$ = this.documentService.uploadVersion(this.documentId, this.selectedFile, payload);

    request$.subscribe({
      next: () => {
        this.submitting = false;
        this.selectedFile = null;
        this.clearSignature();
        this.createVersionForm.reset({
          status: 'BROUILLON',
          revisionComment: '',
          effectiveDate: this.getTodayAsInputDate(),
          expiryDate: ''
        });
        this.notificationService.showSuccess('Version enregistree avec succes.');
        this.loadDetails();
      },
      error: () => {
        this.submitting = false;
        this.notificationService.showError('Impossible de creer la version.');
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
        },
        error: () => {
          this.notificationService.showError('Mise à jour du statut impossible.');
        }
      });
    });
  }

  /*
  old_updateVersionStatus(version: DocumentVersionResponse): void { return; }
  unused_method_wrapper() {
    if (!this.canWrite) {
      return;
    }

    const status = this.statusByVersion[version.id] || version.status;
    const revisionComment = this.commentByVersion[version.id]?.trim() || null;

    this.documentService.updateVersionStatus(this.documentId, version.id, { status, revisionComment }).subscribe({
      next: () => {
        this.notificationService.showSuccess(status === 'PUBLIE'
          ? 'Version publiee avec succes.'
          : 'Statut de version mis a jour.');
        this.loadDetails();
      },
      error: () => {
        this.notificationService.showError('Mise a jour du statut impossible.');
      }
    });
  }

  }
  */
  downloadVersion(version: DocumentVersionResponse): void {
    this.documentService.downloadVersion(this.documentId, version.id).subscribe({
      next: (blob) => {
        const sourceName = version.originalFileName ?? version.fileName ?? undefined;
        const fileName = this.buildDownloadFileName(this.documentCode, version.versionNumber, sourceName);
        this.saveBlob(blob, fileName);
      },
      error: () => {
        this.notificationService.showError('Telechargement impossible.');
      }
    });
  }

  getStatusLabel(status: DocumentStatus): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? status;
  }

  private hydrateStatusForms(versions: DocumentVersionResponse[]): void {
    this.statusByVersion = {};
    this.commentByVersion = {};

    for (const version of versions) {
      this.statusByVersion[version.id] = version.status;
      this.commentByVersion[version.id] = version.revisionComment ?? '';
    }

    this.createVersionForm.patchValue({
      effectiveDate: this.getTodayAsInputDate()
    });
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

  private getTodayAsInputDate(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = `${now.getMonth() + 1}`.padStart(2, '0');
    const day = `${now.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private isCanvasBlank(canvas: HTMLCanvasElement): boolean {
    const context = canvas.getContext('2d');
    if (!context) return true;
    const pixelBuffer = new Uint32Array(
      context.getImageData(0, 0, canvas.width, canvas.height).data.buffer
    );
    return !pixelBuffer.some(color => color !== 0);
  }
}
