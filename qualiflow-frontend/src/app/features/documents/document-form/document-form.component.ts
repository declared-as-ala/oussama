import { Component, OnInit, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { forkJoin, map, of, switchMap } from 'rxjs';
import { AuthService, MeResponse } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UserListResponse, UserResponse, UserService } from '../../../core/services/user.service';
import { ProcessListItemResponse } from '../../processes/models/process.models';
import { ProcessService } from '../../processes/services/process.service';
import { ProcedureListItemResponse } from '../../procedures/models/procedure.models';
import { ProcedureService } from '../../procedures/services/procedure.service';
import { DepartmentListItemResponse } from '../../departments/models/department.models';
import { DepartmentService } from '../../departments/services/department.service';
import {
  CreateDocumentRequest,
  CreateDocumentVersionRequest,
  DOCUMENT_STATUS_OPTIONS,
  DOCUMENT_TYPE_OPTIONS,
  DocumentResponse,
  DocumentStatus,
  DocumentType
} from '../models/document.models';
import { DocumentService } from '../services/document.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-document-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDatepickerModule,
    MatNativeDateModule,
    TranslatePipe
  ],
  templateUrl: './document-form.component.html',
  styleUrls: ['./document-form.component.scss']
})
export class DocumentFormComponent implements OnInit, AfterViewInit {
  private _signatureCanvas!: ElementRef<HTMLCanvasElement>;

  @ViewChild('signatureCanvas') set signatureCanvas(el: ElementRef<HTMLCanvasElement>) {
    if (el) {
      this._signatureCanvas = el;
      // Use setTimeout to ensure the element is fully rendered and has dimensions
      setTimeout(() => this.initCanvas(), 0);
    }
  }

  private ctx!: CanvasRenderingContext2D;
  private isDrawing = false;
  isCanvasEmpty = true;
  readonly typeOptions = DOCUMENT_TYPE_OPTIONS;
  readonly statusOptions = DOCUMENT_STATUS_OPTIONS;

  readonly documentForm = this.fb.group({
    code: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(2),
      Validators.maxLength(50),
      Validators.pattern(/^[A-Za-z0-9_\-/]+$/)
    ]),
    title: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3), Validators.maxLength(255)]),
    type: this.fb.nonNullable.control<DocumentType>('MANUEL', Validators.required),
    description: this.fb.control<string>(''),
    category: this.fb.control<string>(''),
    keywords: this.fb.control<string>(''),
    processId: this.fb.control<number | null>(null),
    procedureId: this.fb.control<number | null>(null),
    ownerUserId: this.fb.control<number | null>(null),
    departmentId: this.fb.control<number | null>(null),
    isActive: this.fb.nonNullable.control(true),
    initialVersionNumber: this.fb.nonNullable.control('v1.0', [Validators.maxLength(30)]),
    initialVersionStatus: this.fb.nonNullable.control<DocumentStatus>('BROUILLON'),
    initialRevisionComment: this.fb.control<string>(''),
    initialEffectiveDate: this.fb.control<Date | null>(null),
    initialExpiryDate: this.fb.control<Date | null>(null),
    signature: this.fb.control<string | null>(null)
  });

  loading = false;
  saving = false;
  isEdit = false;
  documentId: number | null = null;
  selectedFile: File | null = null;
  processes: ProcessListItemResponse[] = [];
  procedures: ProcedureListItemResponse[] = [];
  owners: UserResponse[] = [];
  departments: DepartmentListItemResponse[] = [];
  startWithImport = false;
  signaturePreview: string | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly documentService: DocumentService,
    private readonly processService: ProcessService,
    private readonly procedureService: ProcedureService,
    private readonly departmentService: DepartmentService,
    private readonly userService: UserService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService
  ) { }

  ngAfterViewInit(): void {
    // Initial call might fail if *ngIf is active, handled by setter
    if (this._signatureCanvas) {
      this.initCanvas();
    }
  }

  private initCanvas(): void {
    if (!this._signatureCanvas) return;

    const canvas = this._signatureCanvas.nativeElement;
    this.ctx = canvas.getContext('2d')!;

    // Set internal resolution based on CSS size
    canvas.width = canvas.offsetWidth || 500;
    canvas.height = canvas.offsetHeight || 200;

    // Fill with white background (important for PDF)
    this.ctx.fillStyle = '#ffffff';
    this.ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Line style
    this.ctx.strokeStyle = '#000000';
    this.ctx.lineWidth = 2;
    this.ctx.lineCap = 'round';
    this.ctx.lineJoin = 'round';
  }

  startDrawing(event: MouseEvent): void {
    if (this.signaturePreview || !this.ctx) return;
    this.isDrawing = true;
    this.isCanvasEmpty = false;
    const { x, y } = this.getCoords(event);
    this.ctx.beginPath();
    this.ctx.moveTo(x, y);
  }

  draw(event: MouseEvent): void {
    if (!this.isDrawing || this.signaturePreview || !this.ctx) return;
    const { x, y } = this.getCoords(event);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();
  }

  startDrawingTouch(event: TouchEvent): void {
    if (this.signaturePreview || !this.ctx) return;
    event.preventDefault();
    this.isDrawing = true;
    this.isCanvasEmpty = false;
    const { x, y } = this.getCoordsTouch(event);
    this.ctx.beginPath();
    this.ctx.moveTo(x, y);
  }

  drawTouch(event: TouchEvent): void {
    if (!this.isDrawing || this.signaturePreview || !this.ctx) return;
    event.preventDefault();
    const { x, y } = this.getCoordsTouch(event);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();
  }

  stopDrawing(): void {
    this.isDrawing = false;
  }

  private getCoords(event: MouseEvent): { x: number, y: number } {
    const rect = this._signatureCanvas.nativeElement.getBoundingClientRect();
    return {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top
    };
  }

  private getCoordsTouch(event: TouchEvent): { x: number, y: number } {
    const rect = this._signatureCanvas.nativeElement.getBoundingClientRect();
    const touch = event.touches[0];
    return {
      x: touch.clientX - rect.left,
      y: touch.clientY - rect.top
    };
  }

  saveSignatureFromPad(): void {
    if (!this._signatureCanvas) return;
    const canvas = this._signatureCanvas.nativeElement;
    this.signaturePreview = canvas.toDataURL('image/png');
    this.documentForm.patchValue({ signature: this.signaturePreview });
  }

  clearSignaturePad(): void {
    if (!this._signatureCanvas || !this.ctx) return;
    const canvas = this._signatureCanvas.nativeElement;
    this.ctx.fillStyle = '#ffffff';
    this.ctx.fillRect(0, 0, canvas.width, canvas.height);
    this.signaturePreview = null;
    this.isCanvasEmpty = true;
    this.documentForm.patchValue({ signature: null });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.documentId = idParam ? Number(idParam) : null;
    this.isEdit = this.documentId !== null && !Number.isNaN(this.documentId);
    this.startWithImport = this.route.snapshot.queryParamMap.get('mode') === 'import';
    if (!this.canValidateStatus) {
      this.documentForm.controls.initialVersionStatus.disable({ emitEvent: false });
    }
    if (!this.isEdit) {
      this.documentForm.controls.initialEffectiveDate.setValue(new Date());
    }

    this.loading = true;
    const currentUser = this.authService.getCurrentUser();

    const baseData$ = forkJoin({
      processes: this.processService.getProcesses({ pageNumber: 1, pageSize: 300 }),
      users: this.canSelectOwner
        ? this.userService.getAll(1, 300)
        : of<UserListResponse>({ total: 0, page: 1, pageSize: 0, items: [] }),
      departments: this.departmentService.getDepartments({ pageNumber: 1, pageSize: 300 })
    });

    if (this.isEdit && this.documentId) {
      forkJoin({
        base: baseData$,
        details: this.documentService.getDocumentById(this.documentId)
      }).subscribe({
        next: ({ base, details }) => {
          this.processes = base.processes.items;
          this.owners = base.users.items.filter(user => user.isActive);
          this.departments = base.departments.items.filter(dept => dept.status === 'ACTIF');
          this.patchDocument(details.document);
          if (!this.canSelectOwner && currentUser?.id) {
            this.ensureCurrentUserAsOwnerOption(currentUser);
            this.documentForm.controls.ownerUserId.setValue(currentUser.id);
          }

          if (details.document.processId) {
            this.loadProceduresForProcess(details.document.processId, details.document.procedureId ?? null);
          } else {
            this.loading = false;
          }
        },
        error: () => {
          this.loading = false;
          this.notificationService.showError('Impossible de charger le document.');
          this.router.navigate(['/documents']);
        }
      });

      return;
    }

    baseData$.subscribe({
      next: ({ processes, users, departments }) => {
        this.processes = processes.items;
        this.owners = users.items.filter(user => user.isActive);
        this.departments = departments.items.filter(dept => dept.status === 'ACTIF');
        if (!this.canSelectOwner && currentUser?.id) {
          this.ensureCurrentUserAsOwnerOption(currentUser);
          this.documentForm.controls.ownerUserId.setValue(currentUser.id);
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les donnees de formulaire.');
      }
    });

    // Auto-select responsible from procedure
    this.documentForm.controls.procedureId.valueChanges.subscribe(procedureId => {
      if (!procedureId) {
        return;
      }

      const selectedProcedure = this.procedures.find(item => item.id === procedureId);
      if (selectedProcedure?.responsibleUserId) {
        this.documentForm.controls.ownerUserId.setValue(selectedProcedure.responsibleUserId);
      }
    });
  }

  get title(): string {
    return this.isEdit ? 'Modifier un document' : 'Nouveau document';
  }

  get subtitle(): string {
    if (this.isEdit) {
      return 'Met a jour les metadonnees et ajoute une nouvelle version si necessaire.';
    }

    return this.startWithImport
      ? 'Importe le fichier et cree la premiere version.'
      : 'Definis les metadonnees du document GED.';
  }

  get canValidateStatus(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get canSelectOwner(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  onProcessChanged(): void {
    const processId = this.documentForm.controls.processId.value;
    this.documentForm.controls.procedureId.setValue(null);

    if (!processId) {
      this.procedures = [];
      return;
    }

    this.loadProceduresForProcess(processId, null);
  }

  onFileSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0] ?? null;
    this.selectedFile = file;
  }

  clearFile(): void {
    this.selectedFile = null;
  }

  goBack(): void {
    if (this.isEdit && this.documentId) {
      this.router.navigate(['/documents', this.documentId]);
      return;
    }

    this.router.navigate(['/documents']);
  }

  submit(): void {
    if (this.documentForm.invalid) {
      this.documentForm.markAllAsTouched();
      return;
    }

    if (this.selectedFile && !this.documentForm.controls.initialVersionNumber.value.trim()) {
      this.notificationService.showWarning('Le numero de version est obligatoire pour l upload.');
      return;
    }

    const payload = this.buildDocumentPayload();
    if (!this.canValidateStatus) {
      this.documentForm.controls.initialVersionStatus.setValue('EN_REVISION');
    }
    this.saving = true;

    const save$ = this.isEdit && this.documentId
      ? this.documentService.updateDocument(this.documentId, payload)
      : this.documentService.createDocument(payload);

    save$
      .pipe(
        switchMap(document =>
          this.uploadIfNeeded(document.id).pipe(
            map(uploaded => ({ documentId: document.id, uploaded }))
          )
        )
      )
      .subscribe({
        next: ({ documentId, uploaded }) => {
          this.saving = false;
          const message = this.isEdit
            ? 'Document mis a jour avec succes.'
            : 'Document cree avec succes.';

          this.notificationService.showSuccess(message);

          if (uploaded) {
            this.notificationService.showSuccess('Version televersee avec succes.');
          }

          this.router.navigate(['/documents', documentId]);
        },
        error: (error) => {
          this.saving = false;
          const backendMessage = error?.error?.message;
          this.notificationService.showError(
            typeof backendMessage === 'string' && backendMessage.trim().length > 0
              ? backendMessage
              : 'Enregistrement impossible. Verifie les champs puis recommence.'
          );
        }
      });
  }

  private uploadIfNeeded(documentId: number) {
    if (!this.selectedFile) {
      return of(false);
    }

    const versionPayload = this.buildVersionPayload();
    return this.documentService.uploadVersion(documentId, this.selectedFile, versionPayload).pipe(
      switchMap(() => {
        this.documentId = documentId;
        this.selectedFile = null;
        return of(true);
      })
    );
  }

  private buildDocumentPayload(): CreateDocumentRequest {
    const raw = this.documentForm.getRawValue();

    return {
      processId: raw.processId ?? null,
      procedureId: raw.procedureId ?? null,
      departmentId: raw.departmentId ?? null,
      code: raw.code.trim(),
      title: raw.title.trim(),
      type: raw.type,
      description: raw.description?.trim() || null,
      category: raw.category?.trim() || null,
      keywords: raw.keywords?.trim() || null,
      signature: raw.signature,
      ownerUserId: raw.ownerUserId ?? null,
      isActive: raw.isActive
    };
  }

  private buildVersionPayload(): CreateDocumentVersionRequest {
    const raw = this.documentForm.getRawValue();

    return {
      versionNumber: raw.initialVersionNumber.trim(),
      status: this.canValidateStatus ? raw.initialVersionStatus : 'EN_REVISION',
      revisionComment: raw.initialRevisionComment?.trim() || null,
      effectiveDate: this.formatDateForApi(raw.initialEffectiveDate) || this.getTodayInputDate(),
      expiryDate: this.formatDateForApi(raw.initialExpiryDate)
    };
  }

  private patchDocument(document: DocumentResponse): void {
    this.documentForm.patchValue({
      code: document.code,
      title: document.title,
      type: document.type,
      description: document.description ?? '',
      category: document.category ?? '',
      keywords: document.keywords ?? '',
      processId: document.processId ?? null,
      procedureId: document.procedureId ?? null,
      ownerUserId: document.ownerUserId ?? null,
      departmentId: document.departmentId ?? null,
      isActive: document.isActive,
      initialVersionNumber: document.currentVersionNumber ?? 'v1.0',
      initialVersionStatus: document.currentVersionStatus ?? 'BROUILLON',
      initialRevisionComment: '',
      initialEffectiveDate: document.currentVersionNumber ? new Date() : null,
      initialExpiryDate: null,
      signature: document.signature ?? null
    });
    this.signaturePreview = document.signature ?? null;
  }

  private loadProceduresForProcess(processId: number, selectedProcedureId: number | null): void {
    this.procedureService.getProceduresByProcess(processId).subscribe({
      next: (procedures) => {
        this.procedures = procedures;
        this.documentForm.controls.procedureId.setValue(selectedProcedureId);
        this.loading = false;
      },
      error: () => {
        this.procedures = [];
        this.loading = false;
        this.notificationService.showWarning('Impossible de charger les procedures du processus.');
      }
    });
  }

  private ensureCurrentUserAsOwnerOption(currentUser: MeResponse): void {
    const alreadyExists = this.owners.some(owner => owner.id === currentUser.id);
    if (alreadyExists) {
      return;
    }

    this.owners = [
      {
        id: currentUser.id,
        organizationId: currentUser.organizationId,
        organizationName: currentUser.organizationName,
        firstName: currentUser.firstName,
        lastName: currentUser.lastName,
        email: currentUser.email,
        role: currentUser.role,
        function: currentUser.function,
        department: currentUser.department,
        departmentId: null,
        departmentName: null,
        isActive: currentUser.isActive,
        createdAt: currentUser.createdAt
      },
      ...this.owners
    ];
  }

  private getTodayInputDate(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = `${now.getMonth() + 1}`.padStart(2, '0');
    const day = `${now.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private formatDateForApi(value: Date | string | null | undefined): string | null {
    if (!value) {
      return null;
    }

    if (typeof value === 'string') {
      return value.trim() || null;
    }

    const year = value.getFullYear();
    const month = `${value.getMonth() + 1}`.padStart(2, '0');
    const day = `${value.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
