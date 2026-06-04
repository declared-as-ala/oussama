import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserResponse, UserService } from '../../../core/services/user.service';
import { ProcessListItemResponse } from '../../processes/models/process.models';
import { ProcessService } from '../../processes/services/process.service';
import {
  CreateProcedureRequest,
  PROCEDURE_STATUS_OPTIONS,
  ProcedureResponse,
  ProcedureStatus,
  UpdateProcedureRequest,
  ProcedureListItemResponse
} from '../models/procedure.models';
import { ProcedureService } from '../services/procedure.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-procedure-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslatePipe
  ],
  templateUrl: './procedure-form.component.html',
  styleUrls: ['./procedure-form.component.scss']
})
export class ProcedureFormComponent implements OnInit {
  readonly statusOptions = PROCEDURE_STATUS_OPTIONS;

  readonly procedureForm = this.fb.group({
    processIds: this.fb.nonNullable.control<number[]>([], [Validators.required, Validators.minLength(1)]),
    code: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(30), Validators.pattern(/^[A-Za-z0-9_\-/]+$/)]),
    title: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3), Validators.maxLength(255)]),
    objective: this.fb.control<string>('', [Validators.maxLength(1200)]),
    scope: this.fb.control<string>('', [Validators.maxLength(1200)]),
    description: this.fb.control<string>('', [Validators.maxLength(2000)]),
    responsibleUserId: this.fb.control<number | null>(null),
    status: this.fb.nonNullable.control<ProcedureStatus>('ACTIF', Validators.required),
    versionNumber: this.fb.control<string>('1.0', [Validators.required]),
    revisionComment: this.fb.control<string>('')
  });

  loading = false;
  saving = false;
  isEdit = false;
  procedureId: number | null = null;
  processes: ProcessListItemResponse[] = [];
  responsibles: UserResponse[] = [];
  existingProcedures: ProcedureListItemResponse[] = [];
  activeTab = 0;

  /** Search string for the process picker */
  processSearch = '';

  /** Selected process IDs (mirror of the form control) */
  get selectedProcessIds(): number[] {
    return this.procedureForm.controls.processIds.value ?? [];
  }

  /** Processes filtered by the search input */
  get filteredProcesses(): ProcessListItemResponse[] {
    const q = this.processSearch.trim().toLowerCase();
    if (!q) return this.processes;
    return this.processes.filter(
      p => p.code.toLowerCase().includes(q) || p.name.toLowerCase().includes(q)
    );
  }

  /** Toggle a process in/out of the selection */
  toggleProcess(id: number): void {
    const current = [...this.selectedProcessIds];
    const idx = current.indexOf(id);
    if (idx >= 0) {
      current.splice(idx, 1);
    } else {
      current.push(id);
    }
    this.procedureForm.controls.processIds.setValue(current);
    this.procedureForm.controls.processIds.markAsTouched();
    this.autoGenerateCode();
  }

  /** Whether a process is currently selected */
  isProcessSelected(id: number): boolean {
    return this.selectedProcessIds.includes(id);
  }

  /** Find a process by ID */
  getProcessById(id: number): ProcessListItemResponse | undefined {
    return this.processes.find(p => p.id === id);
  }

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly procedureService: ProcedureService,
    private readonly processService: ProcessService,
    private readonly userService: UserService,
    private readonly notificationService: NotificationService,
    private readonly authService: AuthService
  ) { }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.procedureId = idParam ? Number(idParam) : null;
    this.isEdit = this.procedureId !== null && !Number.isNaN(this.procedureId);

    // Auto-generate code when processIds or title changes
    this.procedureForm.controls.processIds.valueChanges.subscribe(() => this.autoGenerateCode());
    this.procedureForm.controls.title.valueChanges.subscribe(() => this.autoGenerateCode());

    // Add duplicate code validator
    this.procedureForm.controls.code.addValidators(this.duplicateCodeValidator());

    this.loading = true;

    const baseLoad$ = forkJoin({
      users: this.userService.getAll(1, 300),
      processes: this.processService.getProcesses({ pageNumber: 1, pageSize: 300 }),
      procedures: this.procedureService.getProcedures({ pageNumber: 1, pageSize: 500 })
    });

    if (this.isEdit && this.procedureId) {
      forkJoin({
        base: baseLoad$,
        details: this.procedureService.getProcedureById(this.procedureId)
      }).subscribe({
        next: ({ base, details }) => {
          this.responsibles = base.users.items.filter(user => user.isActive && (user.role === 'RESPONSABLE_QUALITE' || user.role === 'SUPER_ADMIN' || user.role === 'ADMIN_ORG'));
          this.processes = base.processes.items;
          this.existingProcedures = base.procedures.items;
          this.patchForm(details.procedure);
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.showError('Impossible de charger le formulaire de la procedure.');
          this.router.navigate(['/procedures']);
        }
      });
      return;
    }

    baseLoad$.subscribe({
      next: ({ users, processes, procedures }) => {
        this.responsibles = users.items.filter(user => user.isActive && (user.role === 'RESPONSABLE_QUALITE' || user.role === 'SUPER_ADMIN' || user.role === 'ADMIN_ORG'));
        this.processes = processes.items;
        this.existingProcedures = procedures.items;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les donnees de reference.');
      }
    });
  }

  get canChangeResponsible(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'SUPER_ADMIN']);
  }

  getResponsibleName(): string {
    const id = this.procedureForm.get('responsibleUserId')?.value;
    if (!id) return 'Non défini';
    const r = this.responsibles.find(u => u.id === id);
    return r ? `${r.firstName} ${r.lastName}` : 'Non défini';
  }

  get title(): string {
    return this.isEdit ? 'Modifier une procedure' : 'Nouvelle procedure';
  }

  get completionPercent(): number {
    const processIds = this.procedureForm.controls.processIds.value;
    const requiredControls = [
      this.procedureForm.controls.code,
      this.procedureForm.controls.title,
      this.procedureForm.controls.status
    ];

    const processValid = Array.isArray(processIds) && processIds.length > 0 ? 1 : 0;
    const done = requiredControls.filter(control => control.valid && `${control.value ?? ''}`.toString().trim().length > 0).length;
    return Math.round(((done + processValid) / (requiredControls.length + 1)) * 100);
  }

  get nextVersion(): string {
    const current = this.procedureForm.controls.versionNumber.value || '1.0';
    const num = parseFloat(current);
    return isNaN(num) ? current : (num + 0.1).toFixed(1);
  }

  isInvalid(fieldName: 'processIds' | 'code' | 'title' | 'objective' | 'scope' | 'description' | 'versionNumber'): boolean {
    const control = this.procedureForm.controls[fieldName];
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  goBack(): void {
    if (this.isEdit && this.procedureId) {
      this.router.navigate(['/procedures', this.procedureId]);
      return;
    }

    this.router.navigate(['/procedures']);
  }

  submit(): void {
    if (this.procedureForm.invalid) {
      this.procedureForm.markAllAsTouched();
      const invalidControls: string[] = [];
      Object.keys(this.procedureForm.controls).forEach(key => {
        const control = this.procedureForm.get(key);
        if (control && control.invalid) {
          invalidControls.push(key);
        }
      });
      console.warn('Formulaire invalide. Champs en erreur:', invalidControls);
      this.notificationService.showError('Le formulaire est invalide. Veuillez vérifier les champs suivants : ' + invalidControls.join(', '));
      return;
    }

    this.saving = true;

    const payload = this.buildPayload();

    const request$ = this.isEdit && this.procedureId
      ? this.procedureService.updateProcedure(this.procedureId, payload as UpdateProcedureRequest)
      : this.procedureService.createProcedure(payload);

    request$.subscribe({
      next: (response) => {
        this.saving = false;
        this.notificationService.showSuccess(this.isEdit ? 'Procedure mise a jour avec succes.' : 'Procedure creee avec succes.');
        this.router.navigate(['/procedures', response.id]);
      },
      error: () => {
        this.saving = false;
        this.notificationService.showError('Enregistrement impossible. Verifie les champs puis recommence.');
      }
    });
  }

  private patchForm(procedure: ProcedureResponse): void {
    // Build the list of processIds from the linked processes array
    const processIds = procedure.processes && procedure.processes.length > 0
      ? procedure.processes.map(p => p.id)
      : (procedure.processId ? [procedure.processId] : []);

    this.procedureForm.patchValue({
      processIds,
      code: procedure.code,
      title: procedure.title,
      objective: procedure.objective ?? '',
      scope: procedure.scope ?? '',
      description: procedure.description ?? '',
      responsibleUserId: procedure.responsibleUserId ?? null,
      status: procedure.status,
      versionNumber: procedure.versionNumber ?? '1.0',
      revisionComment: ''
    });
  }

  private buildPayload(): CreateProcedureRequest {
    const raw = this.procedureForm.getRawValue();

    return {
      processIds: raw.processIds,
      code: raw.code.trim(),
      title: raw.title.trim(),
      objective: raw.objective?.trim() || null,
      scope: raw.scope?.trim() || null,
      description: raw.description?.trim() || null,
      responsibleUserId: raw.responsibleUserId ?? null,
      status: raw.status,
      versionNumber: raw.versionNumber,
      revisionComment: raw.revisionComment || null
    };
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
    const codeCtrl = this.procedureForm.controls.code;

    // If the field is dirty (manually changed by the user) and is not empty, don't overwrite it
    if (codeCtrl.dirty && codeCtrl.value) {
      return;
    }

    // If editing and we already have a value, don't overwrite it
    if (this.isEdit && codeCtrl.value) {
      return;
    }

    const processIds = this.procedureForm.controls.processIds.value;
    const title = this.procedureForm.controls.title.value;

    if (!processIds || processIds.length === 0 || !title) {
      codeCtrl.setValue('', { emitEvent: false });
      return;
    }

    // Use the first selected process for code generation
    const primaryProcessId = processIds[0];
    const selectedProcess = this.processes.find(p => p.id === primaryProcessId);
    if (!selectedProcess) {
      codeCtrl.setValue('', { emitEvent: false });
      return;
    }

    // Base process code (extract prefix, e.g. PIL-GRH from PIL-GRH-2026)
    // If it has a year suffix (e.g. -2026 at the end), let's strip it to keep the code clean
    let processPrefix = selectedProcess.code;
    const yearPattern = /-\d{4}$/;
    if (yearPattern.test(processPrefix)) {
      processPrefix = processPrefix.replace(yearPattern, '');
    }

    const titleCode = this.generateTitleCode(title);
    const year = new Date().getFullYear();

    const generatedCode = `${processPrefix}-${titleCode}-${year}`;

    // Prevent duplicate codes
    let finalCode = generatedCode;
    let counter = 1;
    while (this.existingProcedures.some(p => p.code.toUpperCase() === finalCode.toUpperCase() && p.id !== this.procedureId)) {
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

      const exists = this.existingProcedures.some(
        p => p.code.toUpperCase() === value.trim().toUpperCase() && p.id !== this.procedureId
      );

      return exists ? { duplicateCode: true } : null;
    };
  }

  setActiveTab(index: number): void {
    this.activeTab = index;
  }

  nextTab(): void {
    if (this.activeTab < 2) {
      this.activeTab++;
    }
  }

  prevTab(): void {
    if (this.activeTab > 0) {
      this.activeTab--;
    }
  }
}
