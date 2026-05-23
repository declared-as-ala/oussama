import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  CORRECTIVE_ACTION_STATUS_OPTIONS,
  CorrectiveActionAttachmentResponse,
  CorrectiveActionDetailsResponse,
  CorrectiveActionStatus
} from '../models/corrective-action.models';
import { CorrectiveActionService } from '../services/corrective-action.service';

interface CorrectiveActionPlanStep {
  id: number;
  title: string;
  completed: boolean;
}

@Component({
  selector: 'app-corrective-action-details',
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
    MatProgressSpinnerModule,
    MatCheckboxModule,
    MatDialogModule,
    MatMenuModule
  ],
  templateUrl: './corrective-action-details.component.html',
  styleUrls: ['./corrective-action-details.component.scss']
})
export class CorrectiveActionDetailsComponent implements OnInit, OnDestroy {
  readonly statusOptions = CORRECTIVE_ACTION_STATUS_OPTIONS;
  readonly historyColumns = ['action', 'comment', 'user', 'date'];
  activeTab = 0;
  planSteps: CorrectiveActionPlanStep[] = [];
  planAutoCompletionNotified = false;

  readonly verificationForm = this.fb.group({
    effectivenessVerified: this.fb.nonNullable.control(true, Validators.required),
    effectivenessComment: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3)])
  });

  readonly completionForm = this.fb.group({
    comment: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3)])
  });

  readonly newStepForm = this.fb.group({
    title: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3)])
  });

  loading = false;
  savingVerification = false;
  savingCompletion = false;
  uploadingAttachments = false;
  actionId!: number;
  details: CorrectiveActionDetailsResponse | null = null;
  selectedAttachmentFiles: File[] = [];
  readonly acceptedAttachmentTypes = '.png,.jpg,.jpeg,.webp,.gif,.pdf,.doc,.docx,.xls,.xlsx';
  readonly allowedAttachmentFormatsLabel = 'images, PDF, Word ou Excel';
  imagePreviewUrls: Record<number, string> = {};

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly dialog: MatDialog,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly correctiveActionService: CorrectiveActionService
  ) {}

  ngOnDestroy(): void {
    this.revokeImagePreviews();
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const parsed = idParam ? Number(idParam) : Number.NaN;

    if (Number.isNaN(parsed)) {
      this.notificationService.showError('Identifiant action corrective invalide.');
      this.router.navigate(['/corrective-actions']);
      return;
    }

    this.actionId = parsed;
    this.load();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get isAssignee(): boolean {
    const user = this.authService.getCurrentUser();
    if (!user || !this.details) {
      return false;
    }
    return this.details.action.responsibleUserId === user.id;
  }

  get canVerify(): boolean {
    if (!this.details) {
      return false;
    }

    return this.details.action.status === 'REALISEE' || this.details.action.status === 'VERIFIEE';
  }

  get canManagePlan(): boolean {
    return this.canWrite || this.isAssignee;
  }

  get canCompleteFromPlan(): boolean {
    return this.canWrite || this.isAssignee;
  }

  get canChangeStatus(): boolean {
    return this.canWrite;
  }

  get canManageAttachments(): boolean {
    return this.canWrite || this.isAssignee;
  }

  get completedStepsCount(): number {
    return this.planSteps.filter(step => step.completed).length;
  }

  get planProgress(): number {
    if (this.planSteps.length === 0) {
      return 0;
    }

    return Math.round((this.completedStepsCount / this.planSteps.length) * 100);
  }

  get isPlanCompleted(): boolean {
    return this.planSteps.length > 0 && this.completedStepsCount === this.planSteps.length;
  }

  get shouldOfferCompletion(): boolean {
    if (!this.details) {
      return false;
    }

    return this.isPlanCompleted
      && this.details.action.status !== 'REALISEE'
      && this.details.action.status !== 'VERIFIEE';
  }

  get statusLabel(): string {
    if (!this.details) {
      return '';
    }

    return this.getStatusLabel(this.details.action.status);
  }

  setActiveTab(index: number): void {
    this.activeTab = index;
  }

  goBack(): void {
    this.router.navigate(['/corrective-actions']);
  }

  edit(): void {
    if (!this.details) {
      return;
    }

    this.router.navigate(['/corrective-actions', this.details.action.id, 'edit']);
  }

  delete(): void {
    if (!this.details) {
      return;
    }

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer action corrective',
        message: `Confirmer la suppression de ${this.details.action.title} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.correctiveActionService.deleteCorrectiveAction(this.actionId).subscribe({
        next: () => {
          this.notificationService.showSuccess('Action corrective supprimee.');
          this.router.navigate(['/corrective-actions']);
        },
        error: () => this.notificationService.showError('Suppression impossible.')
      });
    });
  }

  changeStatus(status: CorrectiveActionStatus): void {
    if (!this.details || this.details.action.status === status) {
      return;
    }

    this.correctiveActionService.updateCorrectiveActionStatus(this.actionId, {
      status,
      comment: 'Mise a jour depuis la fiche detail.'
    }).subscribe({
      next: () => {
        this.notificationService.showSuccess('Statut mis a jour.');
        this.load();
      },
      error: () => this.notificationService.showError('Transition de statut impossible.')
    });
  }

  addPlanStep(): void {
    if (this.newStepForm.invalid) {
      this.newStepForm.markAllAsTouched();
      return;
    }

    const title = this.newStepForm.controls.title.value.trim();
    const nextId = this.planSteps.length > 0
      ? Math.max(...this.planSteps.map(step => step.id)) + 1
      : 1;

    this.planSteps = [
      ...this.planSteps,
      {
        id: nextId,
        title,
        completed: false
      }
    ];
    this.newStepForm.reset({ title: '' });
    this.savePlanSteps();
  }

  togglePlanStep(step: CorrectiveActionPlanStep, completed: boolean): void {
    if (!this.canManagePlan) {
      return;
    }

    this.planSteps = this.planSteps.map(item =>
      item.id === step.id ? { ...item, completed } : item
    );
    this.savePlanSteps();
    this.handlePlanProgressChanged();
  }

  removePlanStep(step: CorrectiveActionPlanStep): void {
    if (!this.canManagePlan) {
      return;
    }

    this.planSteps = this.planSteps.filter(item => item.id !== step.id);
    this.savePlanSteps();
    this.handlePlanProgressChanged();
  }

  completeActionFromPlan(): void {
    if (!this.shouldOfferCompletion) {
      return;
    }

    if (!this.canCompleteFromPlan) {
      this.notificationService.showWarning('Plan termine. Le responsable de l action peut notifier la fin, puis la qualite valide le statut.');
      return;
    }

    if (!this.canWrite && this.isAssignee) {
      this.notifyCompletionFromPlan();
      return;
    }

    const responsibleName = this.details?.responsible.fullName || 'le responsable';
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Confirmer la fin de l action',
        message: `Toutes les tâches sont cochées. Confirmez-vous que ${responsibleName} a terminé cette action corrective et qu elle peut passer au statut réalisée ?`,
        confirmText: 'Confirmer terminée',
        cancelText: 'Annuler',
        type: 'info'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.savingCompletion = true;

      this.correctiveActionService.updateCorrectiveActionStatus(this.actionId, {
        status: 'REALISEE',
        comment: 'Plan d action terminé : toutes les étapes sont cochées et confirmées.'
      }).subscribe({
        next: () => {
          this.savingCompletion = false;
          this.notificationService.showRealtimeNotification(
            'Action corrective réalisée',
            'Confirmation enregistrée. La situation de l action est passée à réalisée.',
            'SUCCESS'
          );
          this.load();
        },
        error: () => {
          this.savingCompletion = false;
          this.notificationService.showError('Impossible de marquer l action comme réalisée.');
        }
      });
    });
  }

  private notifyCompletionFromPlan(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Notifier la fin de l action',
        message: 'Toutes les tâches sont cochées. Envoyer une notification au responsable qualité et à l administrateur pour valider le statut ?',
        confirmText: 'Notifier',
        cancelText: 'Annuler',
        type: 'info'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.savingCompletion = true;

      this.correctiveActionService.notifyCompletion(this.actionId).subscribe({
        next: () => {
          this.savingCompletion = false;
          this.notificationService.showSuccess('Notification envoyee au responsable qualite et a l administrateur.');
          this.load();
        },
        error: () => {
          this.savingCompletion = false;
          this.notificationService.showError('Notification impossible.');
        }
      });
    });
  }

  submitCompletion(): void {
    if (this.completionForm.invalid) {
      this.completionForm.markAllAsTouched();
      return;
    }

    this.savingCompletion = true;
    const comment = this.completionForm.controls.comment.value.trim();

    this.correctiveActionService.updateCorrectiveActionStatus(this.actionId, {
      status: 'REALISEE',
      comment
    }).subscribe({
      next: () => {
        this.savingCompletion = false;
        this.notificationService.showSuccess('Action marquée comme réalisée !');
        this.load();
      },
      error: () => {
        this.savingCompletion = false;
        this.notificationService.showError('Mise à jour impossible.');
      }
    });
  }

  submitVerification(): void {
    if (this.verificationForm.invalid || !this.canVerify) {
      this.verificationForm.markAllAsTouched();
      return;
    }

    this.savingVerification = true;
    const raw = this.verificationForm.getRawValue();

    this.correctiveActionService.verifyEffectiveness(this.actionId, {
      effectivenessVerified: raw.effectivenessVerified,
      effectivenessComment: raw.effectivenessComment.trim()
    }).subscribe({
      next: () => {
        this.savingVerification = false;
        this.notificationService.showSuccess('Verification d efficacite enregistree.');
        this.load();
      },
      error: () => {
        this.savingVerification = false;
        this.notificationService.showError('Verification impossible.');
      }
    });
  }

  getStatusLabel(status: CorrectiveActionStatus): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? status;
  }

  getActionLabel(actionType: string): string {
    switch (actionType) {
      case 'CORRECTIVE_ACTION_CREATED':
        return 'Création de l\'action';
      case 'CORRECTIVE_ACTION_UPDATED':
        return 'Modification de l\'action';
      case 'STATUS_CHANGED':
        return 'Changement de statut';
      case 'EFFECTIVENESS_VERIFIED':
        return 'Efficacité vérifiée';
      case 'COMPLETION_NOTIFIED':
        return 'Fin notifiée';
      case 'ATTACHMENT_ADDED':
        return 'Ajout de fichier';
      case 'ATTACHMENT_DELETED':
        return 'Suppression de fichier';
      default:
        return actionType.replace(/_/g, ' ').toLowerCase();
    }
  }

  getActionIcon(actionType: string): string {
    if (actionType.includes('CREATED')) {
      return 'add_circle_outline';
    }
    if (actionType.includes('UPDATED')) {
      return 'edit';
    }
    if (actionType.includes('STATUS')) {
      return 'published_with_changes';
    }
    if (actionType.includes('VERIFIED')) {
      return 'verified';
    }
    if (actionType.includes('COMPLETION')) {
      return 'outgoing_mail';
    }
    if (actionType.includes('ATTACHMENT')) {
      return 'attach_file';
    }
    return 'history';
  }

  onAttachmentFilesSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const files = Array.from(target.files ?? []);
    const validFiles = files.filter(file => this.isAllowedAttachmentFile(file));

    if (validFiles.length !== files.length) {
      this.notificationService.showWarning(`Format non autorise. Ajoutez uniquement: ${this.allowedAttachmentFormatsLabel}.`);
    }

    this.selectedAttachmentFiles = [...this.selectedAttachmentFiles, ...validFiles];
    target.value = '';
  }

  removeSelectedAttachmentFile(index: number): void {
    this.selectedAttachmentFiles = this.selectedAttachmentFiles.filter((_, itemIndex) => itemIndex !== index);
  }

  uploadAttachments(): void {
    if (!this.canManageAttachments || this.selectedAttachmentFiles.length === 0) {
      return;
    }

    this.uploadingAttachments = true;

    forkJoin(this.selectedAttachmentFiles.map(file => this.correctiveActionService.uploadAttachment(this.actionId, file))).subscribe({
      next: () => {
        this.uploadingAttachments = false;
        this.selectedAttachmentFiles = [];
        this.notificationService.showSuccess('Pieces jointes ajoutees.');
        this.load();
      },
      error: () => {
        this.uploadingAttachments = false;
        this.notificationService.showError('Upload impossible.');
      }
    });
  }

  downloadAttachment(attachment: CorrectiveActionAttachmentResponse): void {
    this.correctiveActionService.downloadAttachment(attachment.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = attachment.originalFileName;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.notificationService.showError('Telechargement impossible.')
    });
  }

  deleteAttachment(attachment: CorrectiveActionAttachmentResponse): void {
    if (!this.canManageAttachments) {
      return;
    }

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer la piece jointe',
        message: `Supprimer ${attachment.originalFileName} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.correctiveActionService.deleteAttachment(attachment.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Piece jointe supprimee.');
          this.load();
        },
        error: () => this.notificationService.showError('Suppression impossible.')
      });
    });
  }

  isImageAttachment(attachment: CorrectiveActionAttachmentResponse): boolean {
    return (attachment.mimeType ?? '').startsWith('image/');
  }

  isImageFile(file: File): boolean {
    return file.type.startsWith('image/');
  }

  formatFileSize(size?: number | null): string {
    if (!size) {
      return '-';
    }

    if (size < 1024 * 1024) {
      return `${Math.max(1, Math.round(size / 1024))} Ko`;
    }

    return `${(size / 1024 / 1024).toFixed(1)} Mo`;
  }

  parseChanges(comment: string | null | undefined): string[] {
    if (!comment) {
      return [];
    }

    if (comment.startsWith("Modifications : ")) {
      return comment.replace("Modifications : ", "").split(" | ");
    }

    return [comment];
  }

  deleteActionLog(logId: number): void {
    if (!this.canWrite) {
      return;
    }

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer la ligne d\'historique',
        message: 'Êtes-vous sûr de vouloir supprimer cette ligne d\'historique ? Cette opération est irréversible.',
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.correctiveActionService.deleteCorrectiveActionActionLog(this.actionId, logId).subscribe({
        next: () => {
          this.notificationService.showSuccess('Ligne d\'historique supprimée.');
          this.load();
        },
        error: () => this.notificationService.showError('Suppression impossible.')
      });
    });
  }

  private load(): void {
    this.loading = true;

    this.correctiveActionService.getCorrectiveActionById(this.actionId).subscribe({
      next: details => {
        this.details = details;
        this.loadPlanSteps(details);
        this.hydrateImagePreviews(details.attachments ?? []);
        this.verificationForm.patchValue({
          effectivenessVerified: details.action.effectivenessVerified ?? true,
          effectivenessComment: details.action.effectivenessComment || ''
        });
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger cette action corrective.');
        this.router.navigate(['/corrective-actions']);
      }
    });
  }

  private loadPlanSteps(details: CorrectiveActionDetailsResponse): void {
    const saved = localStorage.getItem(this.planStorageKey(details.action.id));
    this.planAutoCompletionNotified = false;

    if (saved) {
      try {
        const parsed = JSON.parse(saved) as CorrectiveActionPlanStep[];
        this.planSteps = Array.isArray(parsed) ? parsed : this.buildDefaultPlanSteps();
        return;
      } catch {
        this.planSteps = this.buildDefaultPlanSteps();
        this.savePlanSteps();
        return;
      }
    }

    this.planSteps = this.buildDefaultPlanSteps();
    this.savePlanSteps();
  }

  private buildDefaultPlanSteps(): CorrectiveActionPlanStep[] {
    return [
      { id: 1, title: 'Analyser la cause et le périmètre de l action', completed: false },
      { id: 2, title: 'Préparer les mesures correctives à appliquer', completed: false },
      { id: 3, title: 'Exécuter les tâches prévues avec le responsable', completed: false },
      { id: 4, title: 'Ajouter ou vérifier la preuve de réalisation', completed: false },
      { id: 5, title: 'Confirmer que l action peut passer au statut réalisée', completed: false }
    ];
  }

  private savePlanSteps(): void {
    localStorage.setItem(this.planStorageKey(this.actionId), JSON.stringify(this.planSteps));
  }

  private handlePlanProgressChanged(): void {
    if (!this.isPlanCompleted || this.planAutoCompletionNotified) {
      return;
    }

    this.planAutoCompletionNotified = true;
    this.notificationService.showRealtimeNotification(
      'Plan terminé',
      'Toutes les étapes sont cochées. Confirmez la fin de l action pour la passer en réalisée.',
      'SUCCESS'
    );

    if (this.canCompleteFromPlan && this.shouldOfferCompletion) {
      this.completeActionFromPlan();
    }
  }

  private planStorageKey(actionId: number): string {
    return `corrective-action-plan:${actionId}`;
  }

  private hydrateImagePreviews(attachments: CorrectiveActionAttachmentResponse[]): void {
    this.revokeImagePreviews();

    attachments
      .filter(attachment => this.isImageAttachment(attachment))
      .forEach(attachment => {
        this.correctiveActionService.downloadAttachment(attachment.id).subscribe({
          next: blob => {
            this.imagePreviewUrls = {
              ...this.imagePreviewUrls,
              [attachment.id]: URL.createObjectURL(blob)
            };
          }
        });
      });
  }

  private revokeImagePreviews(): void {
    Object.values(this.imagePreviewUrls).forEach(url => URL.revokeObjectURL(url));
    this.imagePreviewUrls = {};
  }

  private isAllowedAttachmentFile(file: File): boolean {
    const name = file.name.toLowerCase();
    return name.endsWith('.png')
      || name.endsWith('.jpg')
      || name.endsWith('.jpeg')
      || name.endsWith('.webp')
      || name.endsWith('.gif')
      || name.endsWith('.pdf')
      || name.endsWith('.doc')
      || name.endsWith('.docx')
      || name.endsWith('.xls')
      || name.endsWith('.xlsx');
  }
}
