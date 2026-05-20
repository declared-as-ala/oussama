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
import { catchError, forkJoin, of } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserResponse, UserService as CoreUserService } from '../../../core/services/user.service';
import { ProcessListItemResponse } from '../../processes/models/process.models';
import { ProcessService } from '../../processes/services/process.service';
import { ProcedureListItemResponse } from '../../procedures/models/procedure.models';
import { ProcedureService } from '../../procedures/services/procedure.service';
import {
  CreateNonConformityRequest,
  NON_CONFORMITY_SEVERITY_OPTIONS,
  NON_CONFORMITY_STATUS_OPTIONS,
  NON_CONFORMITY_TYPE_OPTIONS,
  NonConformityResponse,
  NonConformitySeverity,
  NonConformityStatus,
  NonConformityType,
  UpdateNonConformityRequest
} from '../models/nonconformity.models';
import { NonConformityService } from '../services/nonconformity.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-nonconformity-form',
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
  templateUrl: './nonconformity-form.component.html',
  styleUrls: ['./nonconformity-form.component.scss']
})
export class NonconformityFormComponent implements OnInit {
  readonly typeOptions = NON_CONFORMITY_TYPE_OPTIONS;
  readonly severityOptions = NON_CONFORMITY_SEVERITY_OPTIONS;
  readonly statusOptions = NON_CONFORMITY_STATUS_OPTIONS;

  readonly form = this.fb.group({
    code: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(30)]),
    title: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3), Validators.maxLength(255)]),
    description: this.fb.control<string>(''),
    type: this.fb.nonNullable.control<NonConformityType>('INTERNE', Validators.required),
    severity: this.fb.nonNullable.control<NonConformitySeverity>('MINEURE', Validators.required),
    processId: this.fb.control<number | null>(null),
    procedureId: this.fb.control<number | null>(null),
    detectedDate: this.fb.nonNullable.control('', Validators.required),
    responsibleUserId: this.fb.nonNullable.control<number>(0, [Validators.required, Validators.min(1)]),
    status: this.fb.nonNullable.control<NonConformityStatus>('EN_ATTENTE_VALIDATION', Validators.required)
  });

  loading = false;
  saving = false;
  isEdit = false;
  nonConformityId: number | null = null;

  processes: ProcessListItemResponse[] = [];
  procedures: ProcedureListItemResponse[] = [];
  users: UserResponse[] = [];

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly nonConformityService: NonConformityService,
    private readonly processService: ProcessService,
    private readonly procedureService: ProcedureService,
    private readonly userService: CoreUserService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService
  ) { }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.nonConformityId = idParam ? Number(idParam) : null;
    this.isEdit = this.nonConformityId !== null && !Number.isNaN(this.nonConformityId);

    this.form.controls.processId.valueChanges.subscribe(processId => {
      const selectedProcedureId = this.form.controls.procedureId.value;
      if (!selectedProcedureId) {
        return;
      }

      const selectedProcedure = this.procedures.find(item => item.id === selectedProcedureId);
      if (!selectedProcedure) {
        this.form.controls.procedureId.setValue(null);
        return;
      }

      if (processId && selectedProcedure.processId !== processId) {
        this.form.controls.procedureId.setValue(null);
      }
    });

    // Auto-select responsible from procedure
    this.form.controls.procedureId.valueChanges.subscribe(procedureId => {
      if (!procedureId) {
        return;
      }

      const selectedProcedure = this.procedures.find(item => item.id === procedureId);
      if (selectedProcedure?.responsibleUserId) {
        this.form.controls.responsibleUserId.setValue(selectedProcedure.responsibleUserId);
      }
    });

    this.loading = true;

    const emptyUsersResponse = {
      total: 0,
      page: 1,
      pageSize: 300,
      items: [] as UserResponse[]
    };

    const usersRequest$ = this.canLoadUsers
      ? this.userService.getAll(1, 300).pipe(catchError(() => of(emptyUsersResponse)))
      : of(emptyUsersResponse);

    const baseLoad$ = forkJoin({
      processes: this.processService.getProcesses({ pageNumber: 1, pageSize: 300 }),
      procedures: this.procedureService.getProcedures({ pageNumber: 1, pageSize: 300 }),
      users: usersRequest$
    });

    if (this.isEdit && this.nonConformityId) {
      forkJoin({
        base: baseLoad$,
        details: this.nonConformityService.getNonConformityById(this.nonConformityId)
      }).subscribe({
        next: ({ base, details }) => {
          this.applyReferences(base.processes.items, base.procedures.items, base.users.items);
          this.patchForm(details.nonConformity);
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.showError('Impossible de charger le formulaire non-conformite.');
          this.router.navigate(['/non-conformities']);
        }
      });

      return;
    }

    baseLoad$.subscribe({
      next: ({ processes, procedures, users }) => {
        this.applyReferences(processes.items, procedures.items, users.items);
        this.form.controls.detectedDate.setValue(this.toDateInputValue(new Date().toISOString()));
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les donnees de reference.');
      }
    });
  }

  get title(): string {
    return this.isEdit ? 'Modifier une non-conformite' : 'Nouvelle non-conformite';
  }

  get canLoadUsers(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  /** Only ADMIN_ORG and RESPONSABLE_QUALITE can choose the initial NC status. */
  get canSetStatus(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get filteredProcedures(): ProcedureListItemResponse[] {
    const processId = this.form.controls.processId.value;
    if (!processId) {
      return this.procedures;
    }

    return this.procedures.filter(item => item.processId === processId);
  }

  goBack(): void {
    if (this.isEdit && this.nonConformityId) {
      this.router.navigate(['/non-conformities', this.nonConformityId]);
      return;
    }

    this.router.navigate(['/non-conformities']);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    const payload = this.buildPayload();

    const request$ = this.isEdit && this.nonConformityId
      ? this.nonConformityService.updateNonConformity(this.nonConformityId, payload as UpdateNonConformityRequest)
      : this.nonConformityService.createNonConformity(payload);

    request$.subscribe({
      next: (response) => {
        this.saving = false;
        this.notificationService.showSuccess(this.isEdit ? 'Non-conformite mise a jour.' : 'Non-conformite creee.');
        this.router.navigate(['/non-conformities', response.id]);
      },
      error: () => {
        this.saving = false;
        this.notificationService.showError('Enregistrement impossible.');
      }
    });
  }

  private applyReferences(
    processes: ProcessListItemResponse[],
    procedures: ProcedureListItemResponse[],
    users: UserResponse[]) {
    this.processes = processes;
    this.procedures = procedures;
    this.users = users.filter(user => user.isActive);

    if (this.users.length === 0) {
      const currentUser = this.authService.getCurrentUser();
      if (currentUser?.id) {
        this.users = [
          {
            id: currentUser.id,
            organizationId: currentUser.organizationId,
            firstName: currentUser.firstName,
            lastName: currentUser.lastName,
            email: currentUser.email,
            role: currentUser.role,
            function: currentUser.function,
            department: currentUser.department,
            isActive: true,
            createdAt: currentUser.createdAt
          }
        ];
      }
    }

    if (!this.isEdit && this.users.length > 0) {
      this.form.controls.responsibleUserId.setValue(this.users[0].id);
    }
  }

  private patchForm(nc: NonConformityResponse): void {
    this.form.patchValue({
      code: nc.code,
      title: nc.title,
      description: nc.description || '',
      type: nc.type,
      severity: nc.severity,
      processId: nc.processId ?? null,
      procedureId: nc.procedureId ?? null,
      detectedDate: this.toDateInputValue(nc.detectedDate),
      responsibleUserId: nc.responsibleUserId,
      status: nc.status
    });
  }

  private buildPayload(): CreateNonConformityRequest {
    const raw = this.form.getRawValue();

    return {
      code: raw.code.trim(),
      title: raw.title.trim(),
      description: raw.description?.trim() || null,
      type: raw.type,
      severity: raw.severity,
      processId: raw.processId ?? null,
      procedureId: raw.procedureId ?? null,
      detectedDate: raw.detectedDate ? `${raw.detectedDate}T00:00:00Z` : new Date().toISOString(),
      responsibleUserId: raw.responsibleUserId,
      // Non-privileged roles send no status: backend forces EN_ATTENTE_VALIDATION
      status: this.canSetStatus ? raw.status : undefined
    };
  }

  private toDateInputValue(value?: string): string {
    if (!value) {
      return '';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const year = date.getUTCFullYear();
    const month = `${date.getUTCMonth() + 1}`.padStart(2, '0');
    const day = `${date.getUTCDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
