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
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService as CoreUserService } from '../../../core/services/user.service';
import { ProcessListItemResponse } from '../../processes/models/process.models';
import { ProcessService } from '../../processes/services/process.service';
import {
  CreateIndicatorRequest,
  IndicatorStatus,
  MeasurementFrequency,
  UpdateIndicatorRequest,
  INDICATOR_FREQUENCY_OPTIONS,
  INDICATOR_STATUS_OPTIONS
} from '../models/indicator.models';
import { IndicatorService } from '../services/indicator.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-indicator-form',
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
    MatProgressSpinnerModule,
    TranslatePipe
  ],
  templateUrl: './indicator-form.component.html',
  styleUrls: ['./indicator-form.component.scss']
})
export class IndicatorFormComponent implements OnInit {
  readonly statusOptions = INDICATOR_STATUS_OPTIONS;
  readonly frequencyOptions = INDICATOR_FREQUENCY_OPTIONS;

  readonly form = this.fb.group({
    processId: this.fb.nonNullable.control(0, [Validators.required, Validators.min(1)]),
    code: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    name: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(255)]),
    description: this.fb.control<string>(''),
    calculationMethod: this.fb.control<string>(''),
    unit: this.fb.control<string>(''),
    targetValue: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    alertThreshold: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    measurementFrequency: this.fb.nonNullable.control<MeasurementFrequency>('MENSUEL', Validators.required),
    responsibleUserId: this.fb.nonNullable.control(0, [Validators.required, Validators.min(1)]),
    status: this.fb.nonNullable.control<IndicatorStatus>('ACTIF', Validators.required)
  });

  loading = false;
  saving = false;
  isEdit = false;
  indicatorId: number | null = null;

  processes: ProcessListItemResponse[] = [];
  users: UserResponse[] = [];

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly notificationService: NotificationService,
    private readonly processService: ProcessService,
    private readonly userService: CoreUserService,
    private readonly indicatorService: IndicatorService
  ) { }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.indicatorId = idParam ? Number(idParam) : null;
    this.isEdit = this.indicatorId !== null && !Number.isNaN(this.indicatorId);
    this.loadData();
  }

  get title(): string {
    return this.isEdit ? 'Modifier indicateur' : 'Nouvel indicateur';
  }

  goBack(): void {
    if (this.isEdit && this.indicatorId) {
      this.router.navigate(['/indicators', this.indicatorId]);
      return;
    }

    this.router.navigate(['/indicators']);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;

    if (this.isEdit && this.indicatorId) {
      const payload = this.buildUpdatePayload();
      this.indicatorService.updateIndicator(this.indicatorId, payload).subscribe({
        next: result => {
          this.saving = false;
          this.notificationService.showSuccess('Indicateur mis a jour.');
          this.router.navigate(['/indicators', result.id]);
        },
        error: () => {
          this.saving = false;
          this.notificationService.showError('Mise a jour impossible.');
        }
      });
      return;
    }

    const payload = this.buildCreatePayload();
    this.indicatorService.createIndicator(payload).subscribe({
      next: result => {
        this.saving = false;
        this.notificationService.showSuccess('Indicateur cree.');
        this.router.navigate(['/indicators', result.id]);
      },
      error: () => {
        this.saving = false;
        this.notificationService.showError('Creation impossible.');
      }
    });
  }

  private loadData(): void {
    this.loading = true;

    const refs$ = forkJoin({
      processes: this.processService.getProcesses({ pageNumber: 1, pageSize: 300 }),
      users: this.userService.getAll(1, 300)
    });

    if (this.isEdit && this.indicatorId) {
      forkJoin({
        refs: refs$,
        details: this.indicatorService.getIndicatorById(this.indicatorId)
      }).subscribe({
        next: ({ refs, details }) => {
          this.processes = refs.processes.items;
          this.users = refs.users.items.filter(user => user.isActive);
          this.patchForm(details.indicator);
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.showError('Chargement du formulaire impossible.');
          this.router.navigate(['/indicators']);
        }
      });

      return;
    }

    refs$.subscribe({
      next: ({ processes, users }) => {
        this.processes = processes.items;
        this.users = users.items.filter(user => user.isActive);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Chargement des references impossible.');
      }
    });
  }

  private patchForm(indicator: {
    processId: number;
    code: string;
    name: string;
    description?: string | null;
    calculationMethod?: string | null;
    unit?: string | null;
    targetValue: number;
    alertThreshold: number;
    measurementFrequency: MeasurementFrequency;
    responsibleUserId: number;
    status: IndicatorStatus;
  }): void {
    this.form.patchValue({
      processId: indicator.processId,
      code: indicator.code,
      name: indicator.name,
      description: indicator.description || '',
      calculationMethod: indicator.calculationMethod || '',
      unit: indicator.unit || '',
      targetValue: indicator.targetValue,
      alertThreshold: indicator.alertThreshold,
      measurementFrequency: indicator.measurementFrequency,
      responsibleUserId: indicator.responsibleUserId,
      status: indicator.status
    });
  }

  private buildCreatePayload(): CreateIndicatorRequest {
    const raw = this.form.getRawValue();

    return {
      processId: raw.processId,
      code: raw.code.trim().toUpperCase(),
      name: raw.name.trim(),
      description: raw.description?.trim() || null,
      calculationMethod: raw.calculationMethod?.trim() || null,
      unit: raw.unit?.trim() || null,
      targetValue: Number(raw.targetValue),
      alertThreshold: Number(raw.alertThreshold),
      measurementFrequency: raw.measurementFrequency,
      responsibleUserId: raw.responsibleUserId,
      status: raw.status
    };
  }

  private buildUpdatePayload(): UpdateIndicatorRequest {
    return this.buildCreatePayload();
  }
}
