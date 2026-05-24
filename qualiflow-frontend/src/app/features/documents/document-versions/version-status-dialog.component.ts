import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { DocumentStatus, DOCUMENT_STATUS_OPTIONS } from '../models/document.models';

export interface VersionStatusDialogData {
  versionNumber: string;
  currentStatus: DocumentStatus;
  currentComment: string;
}

@Component({
  selector: 'app-version-status-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule
  ],
  template: `
    <div class="version-status-dialog">
      <div class="dialog-header">
        <mat-icon color="primary">published_with_changes</mat-icon>
        <h2>Mettre à jour le statut - {{ data.versionNumber }}</h2>
      </div>

      <form [formGroup]="form" (ngSubmit)="onSubmit()">
        <mat-dialog-content class="dialog-content">
          <p class="dialog-intro">
            Modifiez le statut de cette version de document et ajoutez un commentaire pour l'historique d'audit.
          </p>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Statut</mat-label>
            <mat-select formControlName="status">
              <mat-option *ngFor="let option of statusOptions" [value]="option.value">
                {{ option.label }}
              </mat-option>
            </mat-select>
            <mat-error *ngIf="form.get('status')?.hasError('required')">
              Le statut est obligatoire.
            </mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Commentaire de révision</mat-label>
            <textarea matInput rows="3" formControlName="revisionComment"
              placeholder="Ex: Version vérifiée et validée pour impression..."></textarea>
          </mat-form-field>
        </mat-dialog-content>

        <mat-dialog-actions align="end" class="dialog-actions">
          <button mat-stroked-button type="button" (click)="onCancel()">Annuler</button>
          <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">
            Enregistrer
          </button>
        </mat-dialog-actions>
      </form>
    </div>
  `,
  styles: [`
    .version-status-dialog {
      padding: 12px;
      max-width: 450px;
    }
    .dialog-header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }
    .dialog-header h2 {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 700;
      color: #1e293b;
    }
    .dialog-intro {
      font-size: 0.875rem;
      color: #64748b;
      margin-bottom: 20px;
      line-height: 1.5;
    }
    .dialog-content {
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding: 0 !important;
    }
    .full-width {
      width: 100%;
    }
    .dialog-actions {
      padding: 16px 0 0 0 !important;
      display: flex;
      gap: 8px;
    }
  `]
})
export class VersionStatusDialogComponent {
  readonly statusOptions = DOCUMENT_STATUS_OPTIONS;
  readonly form: FormGroup;

  constructor(
    private readonly fb: FormBuilder,
    private readonly dialogRef: MatDialogRef<VersionStatusDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public readonly data: VersionStatusDialogData
  ) {
    this.form = this.fb.group({
      status: [data.currentStatus, Validators.required],
      revisionComment: [data.currentComment || '']
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      return;
    }
    this.dialogRef.close(this.form.value);
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}
