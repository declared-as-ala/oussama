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
import { UserResponse, UserService as CoreUserService } from '../../../core/services/user.service';
import { DocumentListItemResponse } from '../../documents/models/document.models';
import { DocumentService } from '../../documents/services/document.service';
import { NonConformityListItemResponse } from '../../non-conformities/models/nonconformity.models';
import { NonConformityService } from '../../non-conformities/services/nonconformity.service';
import {
  CORRECTIVE_ACTION_STATUS_OPTIONS,
  CORRECTIVE_ACTION_TYPE_OPTIONS,
  CorrectiveActionStatus,
  CorrectiveActionType,
  CreateCorrectiveActionRequest,
  UpdateCorrectiveActionRequest
} from '../models/corrective-action.models';
import { CorrectiveActionService } from '../services/corrective-action.service';

@Component({
  selector: 'app-corrective-action-form',
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
    MatProgressSpinnerModule
  ],
  templateUrl: './corrective-action-form.component.html',
  styleUrls: ['./corrective-action-form.component.scss']
})
export class CorrectiveActionFormComponent implements OnInit {
  readonly statusOptions = CORRECTIVE_ACTION_STATUS_OPTIONS;
  readonly typeOptions = CORRECTIVE_ACTION_TYPE_OPTIONS;

  readonly form = this.fb.group({
    nonConformityId: this.fb.control<number | null>(null, [Validators.required, Validators.min(1)]),
    type: this.fb.nonNullable.control<CorrectiveActionType>('CORRECTIVE', Validators.required),
    title: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3), Validators.maxLength(255)]),
    description: this.fb.control<string>(''),
    responsibleUserId: this.fb.control<number | null>(null, [Validators.required, Validators.min(1)]),
    dueDate: this.fb.nonNullable.control('', Validators.required),
    status: this.fb.nonNullable.control<CorrectiveActionStatus>('PLANIFIEE', Validators.required),
    proofRecordId: this.fb.control<number | null>(null),
    completionDate: this.fb.control<string>('')
  });

  loading = false;
  saving = false;
  isEdit = false;
  correctiveActionId: number | null = null;

  users: UserResponse[] = [];
  nonConformities: NonConformityListItemResponse[] = [];
  proofRecords: DocumentListItemResponse[] = [];

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly notificationService: NotificationService,
    private readonly userService: CoreUserService,
    private readonly nonConformityService: NonConformityService,
    private readonly documentService: DocumentService,
    private readonly correctiveActionService: CorrectiveActionService
  ) { }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.correctiveActionId = idParam ? Number(idParam) : null;
    this.isEdit = this.correctiveActionId !== null && !Number.isNaN(this.correctiveActionId);

    this.loadData();
  }

  get title(): string {
    return this.isEdit ? 'Modifier action corrective' : 'Nouvelle action corrective';
  }

  get showCompletionDate(): boolean {
    const status = this.form.controls.status.value;
    return status === 'REALISEE' || status === 'VERIFIEE';
  }

  goBack(): void {
    if (this.isEdit && this.correctiveActionId) {
      this.router.navigate(['/corrective-actions', this.correctiveActionId]);
      return;
    }

    this.router.navigate(['/corrective-actions']);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;

    if (this.isEdit && this.correctiveActionId) {
      const payload = this.buildUpdatePayload();
      this.correctiveActionService.updateCorrectiveAction(this.correctiveActionId, payload).subscribe({
        next: (response) => {
          this.saving = false;
          this.notificationService.showSuccess('Action corrective mise a jour.');
          this.router.navigate(['/corrective-actions', response.id]);
        },
        error: () => {
          this.saving = false;
          this.notificationService.showError('Mise a jour impossible.');
        }
      });

      return;
    }

    const payload = this.buildCreatePayload();
    this.correctiveActionService.createCorrectiveAction(payload).subscribe({
      next: (response) => {
        this.saving = false;
        this.notificationService.showSuccess('Action corrective creee.');
        this.router.navigate(['/corrective-actions', response.id]);
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
      users: this.userService.getAll(1, 300),
      nonConformities: this.nonConformityService.getNonConformities({ pageNumber: 1, pageSize: 300 }),
      records: this.documentService.getDocuments({ pageNumber: 1, pageSize: 300, type: 'ENREGISTREMENT' })
    });

    if (this.isEdit && this.correctiveActionId) {
      forkJoin({
        refs: refs$,
        details: this.correctiveActionService.getCorrectiveActionById(this.correctiveActionId)
      }).subscribe({
        next: ({ refs, details }) => {
          this.users = refs.users.items.filter(user => user.isActive);
          this.nonConformities = refs.nonConformities.items;
          this.proofRecords = refs.records.items;
          this.patchForm(details.action);
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.showError('Chargement du formulaire impossible.');
          this.router.navigate(['/corrective-actions']);
        }
      });

      return;
    }

    refs$.subscribe({
      next: ({ users, nonConformities, records }) => {
        this.users = users.items.filter(user => user.isActive);
        this.nonConformities = nonConformities.items;
        this.proofRecords = records.items;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Chargement des references impossible.');
      }
    });
  }

  private patchForm(action: any): void {
    this.form.patchValue({
      nonConformityId: action.nonConformityId,
      type: action.type,
      title: action.title,
      description: action.description || '',
      responsibleUserId: action.responsibleUserId,
      dueDate: this.toDateInputValue(action.dueDate),
      status: action.status,
      proofRecordId: action.proofRecordId ?? null,
      completionDate: this.toDateInputValue(action.completionDate)
    });
  }

  private buildCreatePayload(): CreateCorrectiveActionRequest {
    const raw = this.form.getRawValue();

    return {
      nonConformityId: raw.nonConformityId!,
      type: raw.type,
      title: raw.title.trim(),
      description: raw.description?.trim() || null,
      responsibleUserId: raw.responsibleUserId!,
      dueDate: `${raw.dueDate}T00:00:00Z`,
      status: raw.status,
      proofRecordId: raw.proofRecordId ?? null
    };
  }

  private buildUpdatePayload(): UpdateCorrectiveActionRequest {
    const createPayload = this.buildCreatePayload();
    const completionDateRaw = this.form.controls.completionDate.value;

    return {
      ...createPayload,
      completionDate: completionDateRaw ? `${completionDateRaw}T00:00:00Z` : null
    };
  }

  private toDateInputValue(value?: string | null): string {
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
