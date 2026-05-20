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
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService } from '../../../core/services/user.service';
import { ProcessListItemResponse } from '../../processes/models/process.models';
import { ProcessService } from '../../processes/services/process.service';
import {
  CreateProcedureRequest,
  PROCEDURE_STATUS_OPTIONS,
  ProcedureResponse,
  ProcedureStatus,
  UpdateProcedureRequest
} from '../models/procedure.models';
import { ProcedureService } from '../services/procedure.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-procedure-form',
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
    MatProgressSpinnerModule,
    TranslatePipe
  ],
  templateUrl: './procedure-form.component.html',
  styleUrls: ['./procedure-form.component.scss']
})
export class ProcedureFormComponent implements OnInit {
  readonly statusOptions = PROCEDURE_STATUS_OPTIONS;

  readonly procedureForm = this.fb.group({
    processId: this.fb.nonNullable.control<number>(0, [Validators.required, Validators.min(1)]),
    code: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(30), Validators.pattern(/^[A-Za-z0-9_\-/]+$/)]),
    title: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3), Validators.maxLength(255)]),
    objective: this.fb.control<string>('', [Validators.maxLength(1200)]),
    scope: this.fb.control<string>('', [Validators.maxLength(1200)]),
    description: this.fb.control<string>('', [Validators.maxLength(2000)]),
    responsibleUserId: this.fb.control<number | null>(null),
    status: this.fb.nonNullable.control<ProcedureStatus>('ACTIF', Validators.required)
  });

  loading = false;
  saving = false;
  isEdit = false;
  procedureId: number | null = null;
  processes: ProcessListItemResponse[] = [];
  responsibles: UserResponse[] = [];

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly procedureService: ProcedureService,
    private readonly processService: ProcessService,
    private readonly userService: UserService,
    private readonly notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.procedureId = idParam ? Number(idParam) : null;
    this.isEdit = this.procedureId !== null && !Number.isNaN(this.procedureId);

    this.loading = true;

    const baseLoad$ = forkJoin({
      users: this.userService.getAll(1, 300),
      processes: this.processService.getProcesses({ pageNumber: 1, pageSize: 300 })
    });

    if (this.isEdit && this.procedureId) {
      forkJoin({
        base: baseLoad$,
        details: this.procedureService.getProcedureById(this.procedureId)
      }).subscribe({
        next: ({ base, details }) => {
          this.responsibles = base.users.items.filter(user => user.isActive);
          this.processes = base.processes.items;
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
      next: ({ users, processes }) => {
        this.responsibles = users.items.filter(user => user.isActive);
        this.processes = processes.items;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les donnees de reference.');
      }
    });
  }

  get title(): string {
    return this.isEdit ? 'Modifier une procedure' : 'Nouvelle procedure';
  }

  get completionPercent(): number {
    const requiredControls = [
      this.procedureForm.controls.processId,
      this.procedureForm.controls.code,
      this.procedureForm.controls.title,
      this.procedureForm.controls.status
    ];

    const done = requiredControls.filter(control => control.valid && `${control.value ?? ''}`.toString().trim().length > 0).length;
    return Math.round((done / requiredControls.length) * 100);
  }

  isInvalid(fieldName: 'processId' | 'code' | 'title' | 'objective' | 'scope' | 'description'): boolean {
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
    this.procedureForm.patchValue({
      processId: procedure.processId,
      code: procedure.code,
      title: procedure.title,
      objective: procedure.objective ?? '',
      scope: procedure.scope ?? '',
      description: procedure.description ?? '',
      responsibleUserId: procedure.responsibleUserId ?? null,
      status: procedure.status
    });
  }

  private buildPayload(): CreateProcedureRequest {
    const raw = this.procedureForm.getRawValue();

    return {
      processId: raw.processId,
      code: raw.code.trim(),
      title: raw.title.trim(),
      objective: raw.objective?.trim() || null,
      scope: raw.scope?.trim() || null,
      description: raw.description?.trim() || null,
      responsibleUserId: raw.responsibleUserId ?? null,
      status: raw.status
    };
  }
}
