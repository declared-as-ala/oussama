import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService } from '../../../core/services/user.service';
import {
  CreateProcessRequest,
  PROCESS_STATUS_OPTIONS,
  PROCESS_TYPE_OPTIONS,
  ProcessResponse,
  ProcessStatus,
  ProcessType,
  UpdateProcessRequest
} from '../models/process.models';
import { ProcessService } from '../services/process.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

type ListFieldKey = 'finalities' | 'scope' | 'suppliers' | 'clients' | 'inputData' | 'outputData' | 'objectives';

interface ListFieldMeta {
  key: ListFieldKey;
  label: string;
  placeholder: string;
  helper: string;
}

@Component({
  selector: 'app-process-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    TranslatePipe
  ],
  templateUrl: './process-form.component.html',
  styleUrls: ['./process-form.component.scss']
})
export class ProcessFormComponent implements OnInit {
  readonly separatorKeysCodes: number[] = [ENTER, COMMA];
  readonly typeOptions = PROCESS_TYPE_OPTIONS;
  readonly statusOptions = PROCESS_STATUS_OPTIONS;

  readonly listFieldMeta: Record<ListFieldKey, ListFieldMeta> = {
    finalities: {
      key: 'finalities',
      label: 'Finalites',
      placeholder: 'Ajouter une finalite',
      helper: 'Resultats attendus et valeur produite par le processus.'
    },
    scope: {
      key: 'scope',
      label: 'Perimetre',
      placeholder: 'Ajouter un element du perimetre',
      helper: 'Entites, services ou activites couverts.'
    },
    suppliers: {
      key: 'suppliers',
      label: 'Fournisseurs',
      placeholder: 'Ajouter un fournisseur',
      helper: 'Sources internes ou externes des intrants.'
    },
    clients: {
      key: 'clients',
      label: 'Clients',
      placeholder: 'Ajouter un client',
      helper: 'Beneficiaires du resultat du processus.'
    },
    inputData: {
      key: 'inputData',
      label: 'Donnees d entree',
      placeholder: 'Ajouter une donnee d entree',
      helper: 'Informations ou documents necessaires au demarrage.'
    },
    outputData: {
      key: 'outputData',
      label: 'Donnees de sortie',
      placeholder: 'Ajouter une donnee de sortie',
      helper: 'Livrables et informations produites en sortie.'
    },
    objectives: {
      key: 'objectives',
      label: 'Objectifs',
      placeholder: 'Ajouter un objectif',
      helper: 'KPI cibles et engagements de performance.'
    }
  };

  readonly interfaceFields: ListFieldKey[] = ['scope', 'suppliers', 'clients', 'inputData', 'outputData'];
  readonly performanceFields: ListFieldKey[] = ['finalities', 'objectives'];

  readonly processForm = this.fb.group({
    code: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(2),
      Validators.maxLength(30),
      Validators.pattern(/^[A-Za-z0-9_\-/]+$/)
    ]),
    name: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3), Validators.maxLength(160)]),
    description: this.fb.control<string>('', [Validators.maxLength(1200)]),
    type: this.fb.nonNullable.control<ProcessType>('PILOTAGE', Validators.required),
    finalities: this.fb.nonNullable.control<string[]>([]),
    scope: this.fb.nonNullable.control<string[]>([]),
    suppliers: this.fb.nonNullable.control<string[]>([]),
    clients: this.fb.nonNullable.control<string[]>([]),
    inputData: this.fb.nonNullable.control<string[]>([]),
    outputData: this.fb.nonNullable.control<string[]>([]),
    objectives: this.fb.nonNullable.control<string[]>([]),
    pilotUserId: this.fb.control<number | null>(null),
    status: this.fb.nonNullable.control<ProcessStatus>('ACTIF', Validators.required)
  });

  loading = false;
  saving = false;
  isEdit = false;
  processId: number | null = null;
  pilots: UserResponse[] = [];

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly processService: ProcessService,
    private readonly userService: UserService,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.processId = idParam ? Number(idParam) : null;
    this.isEdit = this.processId !== null && !Number.isNaN(this.processId);

    this.loading = true;

    if (this.isEdit && this.processId) {
      forkJoin({
        users: this.userService.getAll(1, 300),
        details: this.processService.getProcessById(this.processId)
      }).subscribe({
        next: ({ users, details }) => {
          this.pilots = users.items.filter(user => user.isActive);
          this.patchForm(details.process);
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.showError('Impossible de charger le formulaire du processus.');
          this.router.navigate(['/processes']);
        }
      });
      return;
    }

    this.userService.getAll(1, 300).subscribe({
      next: (users) => {
        this.pilots = users.items.filter(user => user.isActive);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les utilisateurs pilotes.');
      }
    });
  }

  get title(): string {
    return this.isEdit ? 'Modifier un processus' : 'Nouveau processus';
  }

  get completionPercent(): number {
    const requiredControls = [
      this.processForm.controls.code,
      this.processForm.controls.name,
      this.processForm.controls.type,
      this.processForm.controls.status
    ];

    const requiredDone = requiredControls.filter(control => control.valid && `${control.value ?? ''}`.trim().length > 0).length;
    const optionalDone = Object.keys(this.listFieldMeta)
      .map(key => this.getListValues(key as ListFieldKey).length)
      .filter(length => length > 0).length;

    const requiredScore = (requiredDone / requiredControls.length) * 70;
    const optionalScore = (optionalDone / Object.keys(this.listFieldMeta).length) * 30;
    return Math.round(requiredScore + optionalScore);
  }

  get totalChipsCount(): number {
    return Object.keys(this.listFieldMeta)
      .map(key => this.getListValues(key as ListFieldKey).length)
      .reduce((sum, value) => sum + value, 0);
  }

  isInvalid(fieldName: 'code' | 'name' | 'description'): boolean {
    const control = this.processForm.controls[fieldName];
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  getMeta(field: ListFieldKey): ListFieldMeta {
    return this.listFieldMeta[field];
  }

  addChip(field: ListFieldKey, event: MatChipInputEvent): void {
    const value = (event.value || '').trim();
    if (!value) {
      event.chipInput?.clear();
      return;
    }

    const current = this.getListValues(field);
    if (!current.some(item => item.toLowerCase() === value.toLowerCase())) {
      this.processForm.controls[field].setValue([...current, value]);
    }

    event.chipInput?.clear();
  }

  removeChip(field: ListFieldKey, value: string): void {
    const nextValues = this.getListValues(field).filter(item => item !== value);
    this.processForm.controls[field].setValue(nextValues);
  }

  getListValues(field: ListFieldKey): string[] {
    return this.processForm.controls[field].value;
  }

  goBack(): void {
    if (this.isEdit && this.processId) {
      this.router.navigate(['/processes', this.processId]);
      return;
    }

    this.router.navigate(['/processes']);
  }

  submit(): void {
    if (this.processForm.invalid) {
      this.processForm.markAllAsTouched();
      return;
    }

    const payload = this.buildPayload();
    this.saving = true;

    const request$ = this.isEdit && this.processId
      ? this.processService.updateProcess(this.processId, payload as UpdateProcessRequest)
      : this.processService.createProcess(payload);

    request$.subscribe({
      next: (response) => {
        this.saving = false;
        const successMessage = this.isEdit
          ? 'Processus mis a jour avec succes.'
          : 'Processus cree avec succes.';

        this.notificationService.showSuccess(successMessage);
        this.router.navigate(['/processes', response.id]);
      },
      error: () => {
        this.saving = false;
        this.notificationService.showError('Enregistrement impossible. Verifie les champs puis recommence.');
      }
    });
  }

  private patchForm(process: ProcessResponse): void {
    this.processForm.patchValue({
      code: process.code,
      name: process.name,
      description: process.description ?? '',
      type: process.type,
      finalities: [...process.finalities],
      scope: [...process.scope],
      suppliers: [...process.suppliers],
      clients: [...process.clients],
      inputData: [...process.inputData],
      outputData: [...process.outputData],
      objectives: [...process.objectives],
      pilotUserId: process.pilotUserId ?? null,
      status: process.status
    });
  }

  private buildPayload(): CreateProcessRequest {
    const raw = this.processForm.getRawValue();

    return {
      code: raw.code.trim(),
      name: raw.name.trim(),
      description: raw.description?.trim() || null,
      type: raw.type,
      finalities: this.normalizeList(raw.finalities),
      scope: this.normalizeList(raw.scope),
      suppliers: this.normalizeList(raw.suppliers),
      clients: this.normalizeList(raw.clients),
      inputData: this.normalizeList(raw.inputData),
      outputData: this.normalizeList(raw.outputData),
      objectives: this.normalizeList(raw.objectives),
      pilotUserId: raw.pilotUserId ?? null,
      status: raw.status
    };
  }

  private normalizeList(values: string[]): string[] {
    const normalized = values
      .map(value => value.trim())
      .filter(value => value.length > 0);

    return Array.from(new Set(normalized));
  }
}
