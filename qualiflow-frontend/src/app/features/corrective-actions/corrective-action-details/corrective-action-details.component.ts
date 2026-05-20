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
  readonly historyColumns = ['oldStatus', 'newStatus', 'comment', 'user', 'date'];

  readonly verificationForm = this.fb.group({
    effectivenessVerified: this.fb.nonNullable.control(true, Validators.required),
    effectivenessComment: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3)])
  });

  loading = false;
  savingVerification = false;
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
