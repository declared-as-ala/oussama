import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService } from '../../../core/services/user.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { DocumentListItemResponse } from '../../documents/models/document.models';
import { DocumentService } from '../../documents/services/document.service';
import {
  AssignProcessActorItem,
  PROCESS_ACTOR_TYPE_OPTIONS,
  PROCESS_STATUS_OPTIONS,
  PROCESS_TYPE_OPTIONS,
  ProcessActorResponse,
  ProcessDetailsResponse,
  ProcessActorType,
  ProcessType
} from '../models/process.models';
import { ProcessService } from '../services/process.service';
import { ProcedureListItemResponse } from '../../procedures/models/procedure.models';
import { ProcedureService } from '../../procedures/services/procedure.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-process-details',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatInputModule,
    MatTooltipModule,
    TranslatePipe
  ],
  templateUrl: './process-details.component.html',
  styleUrls: ['./process-details.component.scss']
})
export class ProcessDetailsComponent implements OnInit {
  readonly actorTypeOptions = PROCESS_ACTOR_TYPE_OPTIONS;
  readonly displayedActorColumns: string[] = ['fullName', 'email', 'function', 'actorType', 'assignedAt', 'actions'];
  readonly displayedDocumentColumns: string[] = ['code', 'title', 'procedure', 'status'];

  readonly actorForm = this.fb.group({
    userId: this.fb.control<number | null>(null, Validators.required),
    actorType: this.fb.nonNullable.control<ProcessActorType>('CONTRIBUTEUR', Validators.required)
  });

  loading = false;
  processId!: number;
  details: ProcessDetailsResponse | null = null;
  users: UserResponse[] = [];
  procedures: ProcedureListItemResponse[] = [];
  allDocuments: DocumentListItemResponse[] = [];
  linkedDocuments: DocumentListItemResponse[] = [];
  selectedProcedureId: number | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly processService: ProcessService,
    private readonly procedureService: ProcedureService,
    private readonly documentService: DocumentService,
    private readonly userService: UserService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
  ) {}

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    const parsedId = rawId ? Number(rawId) : Number.NaN;

    if (Number.isNaN(parsedId)) {
      this.notificationService.showError('Identifiant du processus invalide.');
      this.router.navigate(['/processes']);
      return;
    }

    this.processId = parsedId;
    this.loadDetails();
  }

  get canWrite(): boolean {
    if (this.authService.hasRole(['SUPER_ADMIN', 'ADMIN_ORG', 'RESPONSABLE_QUALITE'])) {
      return true;
    }

    const currentUserId = this.authService.getCurrentUser()?.id;
    if (!currentUserId || !this.details) {
      return false;
    }

    if (this.details.process.pilotUserId === currentUserId) {
      return true;
    }

    return this.details.actors.some(
      actor => actor.userId === currentUserId &&
      (actor.actorType === 'PILOTE' || actor.actorType === 'COPILOTE')
    );
  }

  get hasData(): boolean {
    return this.details !== null;
  }

  get availableUsers(): UserResponse[] {
    if (!this.details) {
      return [];
    }

    return this.users.filter(user => user.isActive && user.organizationId === this.details?.process.organizationId);
  }

  get selectedProcedure(): ProcedureListItemResponse | null {
    return this.procedures.find(item => item.id === this.selectedProcedureId) ?? null;
  }

  get selectedDocumentsTitle(): string {
    if (this.selectedProcedure) {
      return `Documents liés à la procédure ${this.selectedProcedure.code}`;
    }

    return 'Documents liés au processus';
  }

  loadDetails(): void {
    this.loading = true;

    forkJoin({
      details: this.processService.getProcessById(this.processId),
      users: this.userService.getAll(1, 300),
      procedures: this.procedureService.getProceduresByProcess(this.processId),
      documents: this.documentService.getDocuments({
        pageNumber: 1,
        pageSize: 500,
        processId: this.processId
      })
    }).subscribe({
      next: ({ details, users, procedures, documents }) => {
        this.details = details;
        this.users = users.items;
        this.procedures = procedures;
        this.allDocuments = documents.items.filter(item => item.processId === this.processId);
        this.selectedProcedureId = this.procedures[0]?.id ?? null;
        this.refreshLinkedDocuments();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger le processus.');
        this.router.navigate(['/processes']);
      }
    });
  }

  editProcess(): void {
    this.router.navigate(['/processes', this.processId, 'edit']);
  }

  backToList(): void {
    this.router.navigate(['/processes']);
  }

  selectProcedure(procedureId: number | null): void {
    this.selectedProcedureId = procedureId;
    this.refreshLinkedDocuments();
  }

  viewDocument(documentId: number): void {
    this.router.navigate(['/documents', documentId]);
  }

  refreshLinkedDocuments(): void {
    const documents = this.selectedProcedureId
      ? this.allDocuments.filter(item => item.procedureId === this.selectedProcedureId)
      : this.allDocuments;

    this.linkedDocuments = [...documents].sort((left, right) => {
      const leftDate = new Date(left.updatedAt || 0).getTime();
      const rightDate = new Date(right.updatedAt || 0).getTime();
      return rightDate - leftDate;
    });
  }

  addActor(): void {
    if (!this.details) {
      return;
    }

    if (this.actorForm.invalid) {
      this.actorForm.markAllAsTouched();
      return;
    }

    const raw = this.actorForm.getRawValue();
    const userId = raw.userId;
    const actorType = raw.actorType;

    if (!userId) {
      return;
    }

    const duplicate = this.details.actors.some(actor => actor.userId === userId);
    if (duplicate) {
      this.notificationService.showWarning('Cet utilisateur est deja acteur du processus.');
      return;
    }

    const actorsPayload: AssignProcessActorItem[] = [
      ...this.details.actors.map(actor => ({
        userId: actor.userId,
        actorType: actor.actorType
      })),
      {
        userId,
        actorType
      }
    ];

    this.processService.assignActors(this.processId, { actors: actorsPayload }).subscribe({
      next: (actors) => {
        if (!this.details) {
          return;
        }

        this.details.actors = actors;
        this.actorForm.reset({ userId: null, actorType: 'CONTRIBUTEUR' });
        this.notificationService.showSuccess('Acteur ajoute au processus.');
      },
      error: () => {
        this.notificationService.showError('Ajout de l acteur impossible.');
      }
    });
  }

  removeActor(actor: ProcessActorResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Retirer cet acteur',
        message: `Confirmer le retrait de ${actor.fullName} ?`,
        confirmText: 'Retirer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.processService.removeActor(this.processId, actor.userId).subscribe({
        next: () => {
          if (!this.details) {
            return;
          }

          this.details.actors = this.details.actors.filter(item => item.userId !== actor.userId);
          this.notificationService.showSuccess('Acteur retire du processus.');
        },
        error: () => {
          this.notificationService.showError('Suppression de l acteur impossible.');
        }
      });
    });
  }

  formatList(values: string[]): string {
    if (!values.length) {
      return 'Aucune donnee.';
    }

    return values.join(' | ');
  }

  getTypeLabel(type: ProcessType): string {
    return PROCESS_TYPE_OPTIONS.find(option => option.value === type)?.label ?? type;
  }

  getStatusLabel(status: string): string {
    return PROCESS_STATUS_OPTIONS.find(option => option.value === status)?.label ?? status;
  }

  getActorTypeLabel(actorType: string): string {
    return PROCESS_ACTOR_TYPE_OPTIONS.find(option => option.value === actorType)?.label ?? actorType;
  }

  getProcedureDocumentCount(procedureId: number): number {
    return this.allDocuments.filter(item => item.procedureId === procedureId).length;
  }

  getDocumentStatusClass(status?: string | null): string {
    switch ((status || 'BROUILLON').toUpperCase()) {
      case 'APPROUVE':
        return 'conforme';
      case 'EN_REVISION':
        return 'revision';
      case 'PERIME':
        return 'perime';
      case 'ARCHIVE':
      case 'BROUILLON':
      default:
        return 'gray';
    }
  }
}
