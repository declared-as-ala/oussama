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
  INDICATOR_STATUS_OPTIONS,
  IndicatorListItemResponse
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

  readonly unitOptions: { value: string; label: string }[] = [
    { value: '%',       label: 'Pourcentage (%)' },
    { value: 'nombre',  label: 'Nombre' },
    { value: 'score',   label: 'Score' },
    { value: 'heures',  label: 'Heures' },
    { value: 'jours',   label: 'Jours' },
    { value: 'MAD',     label: 'Dirhams (MAD)' },
    { value: 'EUR',     label: 'Euros (EUR)' },
    { value: 'USD',     label: 'Dollars (USD)' },
    { value: 'unités',  label: 'Unités' },
    { value: 'km',      label: 'Kilomètres (km)' },
    { value: 'kg',      label: 'Kilogrammes (kg)' },
    { value: 'autre',   label: 'Autre' },
  ];

  readonly form = this.fb.group({
    processId: this.fb.nonNullable.control(0, [Validators.required, Validators.min(1)]),
    code: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    name: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(255)]),
    description: this.fb.control<string>(''),
    calculationMethod: this.fb.control<string>(''),
    unit: this.fb.nonNullable.control('%'),
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

  activeTab = 0;

  setActiveTab(index: number): void {
    this.activeTab = index;
  }

  nextTab(): void {
    if (this.activeTab < 1) {
      this.activeTab++;
    }
  }

  prevTab(): void {
    if (this.activeTab > 0) {
      this.activeTab--;
    }
  }

  isInvalid(fieldName: string): boolean {
    const control = this.form.get(fieldName);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  processes: ProcessListItemResponse[] = [];
  users: UserResponse[] = [];
  processActors: { userId: number; fullName: string }[] = [];
  loadingActors = false;
  existingIndicators: IndicatorListItemResponse[] = [];

  get responsibleUsers(): UserResponse[] {
    if (this.processActors.length === 0) {
      return this.users;
    }
    const actorIds = new Set(this.processActors.map(a => a.userId));
    return this.users.filter(u => actorIds.has(u.id));
  }

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

    // Auto-generate code when processId or name changes
    this.form.controls.processId.valueChanges.subscribe(() => this.autoGenerateCode());
    this.form.controls.name.valueChanges.subscribe(() => this.autoGenerateCode());

    // Add duplicate code validator
    this.form.controls.code.addValidators(this.duplicateCodeValidator());

    this.loadData();

    // When process changes, load its actors to filter the responsible dropdown
    this.form.get('processId')?.valueChanges.subscribe(val => {
      const processId = val ? Number(val) : null;
      if (processId && processId > 0) {
        this.loadingActors = true;
        this.processService.getActors(processId).subscribe({
          next: (actors) => {
            this.processActors = actors.map(a => ({ userId: a.userId, fullName: a.fullName }));
            // Reset responsible if current selection is not in the new process actors
            const currentResponsible = this.form.getRawValue().responsibleUserId;
            const actorIds = new Set(this.processActors.map(a => a.userId));
            if (currentResponsible && !actorIds.has(currentResponsible)) {
              this.form.patchValue({ responsibleUserId: 0 }, { emitEvent: false });
            }
            this.loadingActors = false;
          },
          error: () => {
            this.processActors = [];
            this.loadingActors = false;
          }
        });
      } else {
        this.processActors = [];
      }
    });
  }

  get title(): string {
    return this.isEdit ? 'Modifier indicateur' : 'Nouvel indicateur';
  }

  get selectedProcess(): ProcessListItemResponse | undefined {
    const pid = this.form.getRawValue().processId;
    return pid ? this.processes.find(p => p.id === pid) : undefined;
  }

  get selectedProcessPilotName(): string | null {
    if (!this.selectedProcess?.pilotUserId) return null;
    const pilot = this.users.find(u => u.id === this.selectedProcess!.pilotUserId);
    return pilot ? `${pilot.firstName} ${pilot.lastName}` : null;
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
      users: this.userService.getAll(1, 300),
      indicators: this.indicatorService.getIndicators({ pageNumber: 1, pageSize: 500 })
    });

    if (this.isEdit && this.indicatorId) {
      forkJoin({
        refs: refs$,
        details: this.indicatorService.getIndicatorById(this.indicatorId)
      }).subscribe({
        next: ({ refs, details }) => {
          this.processes = refs.processes.items;
          this.users = refs.users.items.filter(user => user.isActive);
          this.existingIndicators = refs.indicators.items;
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
      next: ({ processes, users, indicators }) => {
        this.processes = processes.items;
        this.users = users.items.filter(user => user.isActive);
        this.existingIndicators = indicators.items;
        this.loading = false;

        const qProcessId = this.route.snapshot.queryParamMap.get('processId');
        if (qProcessId) {
          const parsed = Number(qProcessId);
          if (!Number.isNaN(parsed)) {
            this.form.patchValue({ processId: parsed });
          }
        }
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
    }, { emitEvent: false });
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

  private generateTitleCode(title: string): string {
    if (!title) return '';

    // Normalize: remove accents
    const normalized = title.normalize('NFD').replace(/[\u0300-\u036f]/g, '');

    // Keep only alphanumeric characters and spaces/hyphens
    const cleaned = normalized.replace(/[^a-zA-Z0-9\s-]/g, '').trim();

    const words = cleaned.split(/[\s-]+/).filter(w => w.length > 0);

    if (words.length === 1) {
      // Single word: take up to 4 characters
      return words[0].substring(0, 4).toUpperCase();
    } else {
      // Multiple words: take the initials of words, ignoring short stop words
      const stopWords = ['DE', 'LA', 'LE', 'DES', 'EN', 'ET', 'UN', 'UNE', 'DU', 'AU', 'AUX', 'POUR', 'PAR', 'SUR', 'D', 'L'];
      const filteredWords = words.filter(w => !stopWords.includes(w.toUpperCase()));

      const finalWords = filteredWords.length > 0 ? filteredWords : words;
      return finalWords.map(w => w[0].toUpperCase()).join('');
    }
  }

  private autoGenerateCode(): void {
    const codeCtrl = this.form.controls.code;

    // If the field is dirty (manually changed by the user) and is not empty, don't overwrite it
    if (codeCtrl.dirty && codeCtrl.value) {
      return;
    }

    // If editing and we already have a value, don't overwrite it
    if (this.isEdit && codeCtrl.value) {
      return;
    }

    const processIdRaw = this.form.controls.processId.value;
    const name = this.form.controls.name.value;

    if (!processIdRaw || !name) {
      codeCtrl.setValue('', { emitEvent: false });
      return;
    }

    const processId = Number(processIdRaw);
    const selectedProcess = this.processes.find(p => p.id === processId);
    if (!selectedProcess) {
      codeCtrl.setValue('', { emitEvent: false });
      return;
    }

    // Base process code (extract prefix, e.g. PIL-GRH from PIL-GRH-2026)
    let processPrefix = selectedProcess.code;
    const yearPattern = /-\d{4}$/;
    if (yearPattern.test(processPrefix)) {
      processPrefix = processPrefix.replace(yearPattern, '');
    }

    const titleCode = this.generateTitleCode(name);
    const year = new Date().getFullYear();

    const generatedCode = `IND-${processPrefix}-${titleCode}-${year}`;

    // Prevent duplicate codes
    let finalCode = generatedCode;
    let counter = 1;
    while (this.existingIndicators.some(ind => ind.code.toUpperCase() === finalCode.toUpperCase() && ind.id !== this.indicatorId)) {
      const suffix = counter < 10 ? `-0${counter}` : `-${counter}`;
      finalCode = `${generatedCode}${suffix}`;
      counter++;
    }

    codeCtrl.setValue(finalCode, { emitEvent: false });
  }

  duplicateCodeValidator(): import('@angular/forms').ValidatorFn {
    return (control: import('@angular/forms').AbstractControl): import('@angular/forms').ValidationErrors | null => {
      const value = control.value;
      if (!value) return null;

      const exists = this.existingIndicators.some(
        ind => ind.code.toUpperCase() === value.trim().toUpperCase() && ind.id !== this.indicatorId
      );

      return exists ? { duplicateCode: true } : null;
    };
  }
}
