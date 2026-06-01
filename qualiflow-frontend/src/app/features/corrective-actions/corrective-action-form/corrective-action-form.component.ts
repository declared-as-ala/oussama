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
import { forkJoin, map, of, switchMap } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService as CoreUserService } from '../../../core/services/user.service';
import { DocumentListItemResponse } from '../../documents/models/document.models';
import { DocumentService } from '../../documents/services/document.service';
import { NonConformityListItemResponse } from '../../non-conformities/models/nonconformity.models';
import { NonConformityService } from '../../non-conformities/services/nonconformity.service';
import { ProcessService } from '../../processes/services/process.service';
import {
  CORRECTIVE_ACTION_STATUS_OPTIONS,
  CORRECTIVE_ACTION_TYPE_OPTIONS,
  CorrectiveActionStatus,
  CorrectiveActionType,
  CorrectiveActionAttachmentResponse,
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
  activeTab = 0;

  users: UserResponse[] = [];
  nonConformities: NonConformityListItemResponse[] = [];
  proofRecords: DocumentListItemResponse[] = [];
  existingAttachments: CorrectiveActionAttachmentResponse[] = [];
  selectedFiles: File[] = [];
  processActors: { userId: number; fullName: string }[] = [];
  loadingActors = false;
  readonly acceptedAttachmentTypes = '.png,.jpg,.jpeg,.webp,.gif,.pdf,.doc,.docx,.xls,.xlsx';
  readonly allowedAttachmentFormatsLabel = 'images, PDF, Word ou Excel';

  /** Returns actors of the NC's linked process, or all users if no process is linked. */
  get responsibleUsers(): UserResponse[] {
    if (this.processActors.length === 0) {
      return this.users;
    }
    const actorIds = new Set(this.processActors.map(a => a.userId));
    return this.users.filter(u => actorIds.has(u.id));
  }

  get selectedNcProcessCode(): string | null {
    const ncId = this.form.getRawValue().nonConformityId;
    if (!ncId) return null;
    const nc = this.nonConformities.find(n => n.id === ncId);
    return nc?.processCode ?? null;
  }

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly notificationService: NotificationService,
    private readonly userService: CoreUserService,
    private readonly nonConformityService: NonConformityService,
    private readonly documentService: DocumentService,
    private readonly correctiveActionService: CorrectiveActionService,
    private readonly processService: ProcessService
  ) { }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.correctiveActionId = idParam ? Number(idParam) : null;
    this.isEdit = this.correctiveActionId !== null && !Number.isNaN(this.correctiveActionId);

    this.loadData();

    // When NC changes, load its linked process actors to filter the responsible dropdown
    this.form.get('nonConformityId')?.valueChanges.subscribe(val => {
      const ncId = val ? Number(val) : null;
      if (!ncId) {
        this.processActors = [];
        return;
      }
      const nc = this.nonConformities.find(n => n.id === ncId);
      if (nc?.processId) {
        this.loadingActors = true;
        this.processService.getActors(nc.processId).subscribe({
          next: (actors) => {
            this.processActors = actors.map(a => ({ userId: a.userId, fullName: a.fullName }));
            // Reset responsible if no longer in new actor list
            const currentResp = this.form.getRawValue().responsibleUserId;
            const actorIds = new Set(this.processActors.map(a => a.userId));
            if (currentResp && !actorIds.has(currentResp)) {
              this.form.patchValue({ responsibleUserId: null }, { emitEvent: false });
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
      this.correctiveActionService.updateCorrectiveAction(this.correctiveActionId, payload)
        .pipe(switchMap(response => this.uploadSelectedFiles(response.id).pipe(map(() => response))))
        .subscribe({
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
    this.correctiveActionService.createCorrectiveAction(payload)
      .pipe(switchMap(response => this.uploadSelectedFiles(response.id).pipe(map(() => response))))
      .subscribe({
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
          this.existingAttachments = details.attachments ?? [];
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

  onFilesSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const files = Array.from(target.files ?? []);
    const validFiles = files.filter(file => this.isAllowedAttachmentFile(file));

    if (validFiles.length !== files.length) {
      this.notificationService.showWarning(`Format non autorise. Ajoutez uniquement: ${this.allowedAttachmentFormatsLabel}.`);
    }

    this.selectedFiles = [...this.selectedFiles, ...validFiles];
    target.value = '';
  }

  removeSelectedFile(index: number): void {
    this.selectedFiles = this.selectedFiles.filter((_, itemIndex) => itemIndex !== index);
  }

  isImageFile(file: File): boolean {
    return file.type.startsWith('image/');
  }

  isImageAttachment(attachment: CorrectiveActionAttachmentResponse): boolean {
    return (attachment.mimeType ?? '').startsWith('image/');
  }

  formatFileSize(size?: number | null): string {
    if (!size) {
      return '-';
    }

    if (size < 1024 * 1024) {
      return `${Math.max(1, Math.round(size / 1024))} Ko`;
    }

    return `${(size / 1024 / 1024).toFixed(1)} Mo`;
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

  private uploadSelectedFiles(actionId: number) {
    if (this.selectedFiles.length === 0) {
      return of([]);
    }

    return forkJoin(this.selectedFiles.map(file => this.correctiveActionService.uploadAttachment(actionId, file)));
  }

  private isAllowedAttachmentFile(file: File): boolean {
    const name = file.name.toLowerCase();
    return name.endsWith('.png')
      || name.endsWith('.jpg')
      || name.endsWith('.jpeg')
      || name.endsWith('.webp')
      || name.endsWith('.gif')
      || name.endsWith('.pdf')
      || name.endsWith('.doc')
      || name.endsWith('.docx')
      || name.endsWith('.xls')
      || name.endsWith('.xlsx');
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
