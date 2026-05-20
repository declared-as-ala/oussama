import { Component, OnInit } from '@angular/core';
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
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  CORRECTIVE_ACTION_STATUS_OPTIONS,
  CorrectiveActionDetailsResponse,
  CorrectiveActionStatus
} from '../models/corrective-action.models';
import { CorrectiveActionService } from '../services/corrective-action.service';

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
    MatDialogModule,
    MatMenuModule
  ],
  templateUrl: './corrective-action-details.component.html',
  styleUrls: ['./corrective-action-details.component.scss']
})
export class CorrectiveActionDetailsComponent implements OnInit {
  readonly statusOptions = CORRECTIVE_ACTION_STATUS_OPTIONS;
  readonly historyColumns = ['action', 'comment', 'user', 'date', 'actions'];

  readonly verificationForm = this.fb.group({
    effectivenessVerified: this.fb.nonNullable.control(true, Validators.required),
    effectivenessComment: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3)])
  });

  readonly completionForm = this.fb.group({
    comment: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3)])
  });

  loading = false;
  savingVerification = false;
  savingCompletion = false;
  actionId!: number;
  details: CorrectiveActionDetailsResponse | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly dialog: MatDialog,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly correctiveActionService: CorrectiveActionService
  ) {}

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

  get statusLabel(): string {
    if (!this.details) {
      return '';
    }

    return this.getStatusLabel(this.details.action.status);
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
    return 'history';
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
}
