import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { NotificationService } from '../../../core/services/notification.service';
import { IndicatorValueResponse } from '../models/indicator.models';
import { IndicatorService } from '../services/indicator.service';

@Component({
  selector: 'app-indicator-values',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule
  ],
  templateUrl: './indicator-values.component.html',
  styleUrls: ['./indicator-values.component.scss']
})
export class IndicatorValuesComponent implements OnInit, OnChanges {
  @Input({ required: true }) indicatorId!: number;
  @Input() canWrite = false;
  @Output() valuesChanged = new EventEmitter<void>();

  readonly form = this.fb.group({
    periodLabel: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    measuredValue: this.fb.nonNullable.control(0, Validators.required),
    measuredAt: this.fb.nonNullable.control('', Validators.required),
    comment: this.fb.control<string>('')
  });

  readonly baseColumns = ['periodLabel', 'measuredValue', 'measuredAt', 'comment', 'enteredBy'];
  values: IndicatorValueResponse[] = [];
  loading = false;
  saving = false;
  editingValueId: number | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly dialog: MatDialog,
    private readonly indicatorService: IndicatorService,
    private readonly notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    if (this.indicatorId > 0) {
      this.loadValues();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    const indicatorChange = changes['indicatorId'];
    if (indicatorChange && !indicatorChange.firstChange && this.indicatorId > 0) {
      this.cancelEdit();
      this.loadValues();
    }
  }

  get displayedColumns(): string[] {
    return this.canWrite ? [...this.baseColumns, 'actions'] : this.baseColumns;
  }

  get isEditing(): boolean {
    return this.editingValueId !== null;
  }

  submit(): void {
    if (!this.canWrite) {
      return;
    }

    if (this.form.invalid || this.indicatorId <= 0) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const payload = {
      periodLabel: raw.periodLabel.trim(),
      measuredValue: Number(raw.measuredValue),
      measuredAt: `${raw.measuredAt}T00:00:00Z`,
      comment: raw.comment?.trim() || null
    };

    this.saving = true;

    if (this.editingValueId) {
      this.indicatorService.updateValue(this.indicatorId, this.editingValueId, payload).subscribe({
        next: () => {
          this.saving = false;
          this.notificationService.showSuccess('Valeur mise a jour.');
          this.cancelEdit();
          this.loadValues();
          this.valuesChanged.emit();
        },
        error: () => {
          this.saving = false;
          this.notificationService.showError('Mise a jour impossible pour cette valeur.');
        }
      });
      return;
    }

    this.indicatorService.createValue(this.indicatorId, payload).subscribe({
      next: () => {
        this.saving = false;
        this.notificationService.showSuccess('Valeur enregistree.');
        this.cancelEdit();
        this.loadValues();
        this.valuesChanged.emit();
      },
      error: () => {
        this.saving = false;
        this.notificationService.showError('Creation de valeur impossible.');
      }
    });
  }

  edit(value: IndicatorValueResponse): void {
    if (!this.canWrite) {
      return;
    }

    this.editingValueId = value.id;
    this.form.patchValue({
      periodLabel: value.periodLabel,
      measuredValue: value.measuredValue,
      measuredAt: this.toDateInputValue(value.measuredAt),
      comment: value.comment || ''
    });
  }

  cancelEdit(): void {
    this.editingValueId = null;
    this.form.reset({
      periodLabel: '',
      measuredValue: 0,
      measuredAt: '',
      comment: ''
    });
  }

  delete(value: IndicatorValueResponse): void {
    if (!this.canWrite) {
      return;
    }

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer valeur KPI',
        message: `Confirmer la suppression de la periode ${value.periodLabel} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.indicatorService.deleteValue(this.indicatorId, value.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Valeur supprimee.');
          this.loadValues();
          this.valuesChanged.emit();
        },
        error: () => this.notificationService.showError('Suppression impossible.')
      });
    });
  }

  trackById(_index: number, value: IndicatorValueResponse): number {
    return value.id;
  }

  private loadValues(): void {
    if (this.indicatorId <= 0) {
      return;
    }

    this.loading = true;
    this.indicatorService.getValues(this.indicatorId).subscribe({
      next: values => {
        this.values = values;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les valeurs de cet indicateur.');
      }
    });
  }

  private toDateInputValue(value?: string | null): string {
    if (!value) {
      return '';
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return '';
    }

    const year = parsed.getUTCFullYear();
    const month = `${parsed.getUTCMonth() + 1}`.padStart(2, '0');
    const day = `${parsed.getUTCDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
