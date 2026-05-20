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
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  CreateInstructionRequest,
  InstructionResponse,
  PROCEDURE_STATUS_OPTIONS,
  ProcedureDetailsResponse,
  ProcedureStatus,
  UpdateInstructionRequest
} from '../models/procedure.models';
import { ProcedureService } from '../services/procedure.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-procedure-details',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatTooltipModule,
    TranslatePipe
  ],
  templateUrl: './procedure-details.component.html',
  styleUrls: ['./procedure-details.component.scss']
})
export class ProcedureDetailsComponent implements OnInit {
  readonly statusOptions = PROCEDURE_STATUS_OPTIONS;
  readonly displayedInstructionColumns: string[] = ['orderIndex', 'code', 'title', 'status', 'actions'];

  readonly instructionForm = this.fb.group({
    code: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]),
    title: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(255)]),
    description: this.fb.control<string>('', [Validators.maxLength(1200)]),
    status: this.fb.nonNullable.control<ProcedureStatus>('ACTIF', Validators.required),
    orderIndex: this.fb.control<number | null>(null)
  });

  loading = false;
  savingInstruction = false;
  procedureId!: number;
  details: ProcedureDetailsResponse | null = null;
  editingInstructionId: number | null = null;
  isAddingInstruction = false;
  activeTab = 0;

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly procedureService: ProcedureService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
  ) { }

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    const parsedId = rawId ? Number(rawId) : Number.NaN;

    if (Number.isNaN(parsedId)) {
      this.notificationService.showError('Identifiant de procedure invalide.');
      this.router.navigate(['/procedures']);
      return;
    }

    this.procedureId = parsedId;
    this.loadDetails();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get hasData(): boolean {
    return this.details !== null;
  }

  get instructionFormTitle(): string {
    return this.editingInstructionId ? 'Modifier une instruction' : 'Ajouter une instruction';
  }

  loadDetails(): void {
    this.loading = true;

    this.procedureService.getProcedureById(this.procedureId).subscribe({
      next: (details) => {
        this.details = details;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger la procedure.');
        this.router.navigate(['/procedures']);
      }
    });
  }

  backToList(): void {
    this.router.navigate(['/procedures']);
  }

  setActiveTab(index: number): void {
    this.activeTab = index;
  }

  editProcedure(): void {
    this.router.navigate(['/procedures', this.procedureId, 'edit']);
  }

  viewHistory(): void {
    this.router.navigate(['/procedures', this.procedureId, 'history']);
  }

  saveInstruction(): void {
    if (!this.details) {
      return;
    }

    if (this.instructionForm.invalid) {
      this.instructionForm.markAllAsTouched();
      return;
    }

    this.savingInstruction = true;
    const payload = this.buildInstructionPayload();

    const request$ = this.editingInstructionId
      ? this.procedureService.updateInstruction(this.procedureId, this.editingInstructionId, payload as UpdateInstructionRequest)
      : this.procedureService.createInstruction(this.procedureId, payload as CreateInstructionRequest);

    request$.subscribe({
      next: () => {
        this.savingInstruction = false;
        this.isAddingInstruction = false;
        this.notificationService.showSuccess(this.editingInstructionId ? 'Instruction mise a jour.' : 'Instruction ajoutee.');
        this.cancelInstructionEdit();
        this.loadDetails();
      },
      error: () => {
        this.savingInstruction = false;
        this.notificationService.showError('Enregistrement de l instruction impossible.');
      }
    });
  }

  editInstruction(instruction: InstructionResponse): void {
    this.editingInstructionId = instruction.id;
    this.isAddingInstruction = false;
    this.instructionForm.patchValue({
      code: instruction.code,
      title: instruction.title,
      description: instruction.description ?? '',
      status: instruction.status,
      orderIndex: instruction.orderIndex
    });
  }

  cancelInstructionEdit(): void {
    this.editingInstructionId = null;
    this.isAddingInstruction = false;
    this.instructionForm.reset({
      code: '',
      title: '',
      description: '',
      status: 'ACTIF',
      orderIndex: null
    });
  }

  deleteInstruction(instruction: InstructionResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer l instruction',
        message: `Confirmer la suppression de ${instruction.code} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.procedureService.deleteInstruction(this.procedureId, instruction.id).subscribe({
        next: () => {
          this.notificationService.showSuccess('Instruction supprimee.');
          this.loadDetails();
        },
        error: () => {
          this.notificationService.showError('Suppression de l instruction impossible.');
        }
      });
    });
  }

  getStatusLabel(status: ProcedureStatus): string {
    return status === 'ACTIF' ? 'Actif' : 'Inactif';
  }

  getTypeLabel(type: string): string {
    switch (type) {
      case 'MANUEL':
        return 'Manuel';
      case 'PROCEDURE':
        return 'Procédure';
      case 'ENREGISTREMENT':
        return 'Enregistrement';
      case 'FORMULAIRE':
        return 'Formulaire';
      case 'INSTRUCTION':
        return 'Instruction';
      case 'POLITIQUE':
        return 'Politique';
      default:
        return type || 'Autre';
    }
  }

  getDocumentStatusLabel(status: string): string {
    switch (status) {
      case 'APPROUVE':
        return 'Approuvé';
      case 'PUBLIE':
        return 'Publié';
      case 'EN_REVISION':
        return 'En révision';
      case 'REJETE':
        return 'Rejeté';
      case 'PERIME':
        return 'Périmé';
      case 'ARCHIVE':
        return 'Archivé';
      default:
        return status || 'Brouillon';
    }
  }

  startAddInstruction(): void {
    this.isAddingInstruction = true;
    this.editingInstructionId = null;
    this.instructionForm.reset({
      code: '',
      title: '',
      description: '',
      status: 'ACTIF',
      orderIndex: (this.details?.instructions.length ?? 0) + 1
    });
  }

  trackByInstructionId(_index: number, item: InstructionResponse): number {
    return item.id;
  }

  private buildInstructionPayload(): CreateInstructionRequest {
    const raw = this.instructionForm.getRawValue();

    return {
      code: raw.code.trim(),
      title: raw.title.trim(),
      description: raw.description?.trim() || null,
      status: raw.status,
      orderIndex: raw.orderIndex ?? null
    };
  }
}

