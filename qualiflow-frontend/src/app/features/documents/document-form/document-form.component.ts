import { Component, OnInit, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators, FormArray, FormGroup } from '@angular/forms';
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
import {
  CreateDocumentRequest,
  CreateDocumentVersionRequest,
  DOCUMENT_STATUS_OPTIONS,
  DOCUMENT_TYPE_OPTIONS,
  DocumentResponse,
  DocumentStatus,
  DocumentType,
  DocumentListItemResponse
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
  readonly acceptedFileTypes = '.pdf,.docx,.xlsx,.txt';
  readonly allowedFileFormatsLabel = 'PDF, Word (.docx), Excel (.xlsx) ou Texte (.txt)';

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
    processIds: this.fb.control<number[]>([]),
    procedureIds: this.fb.control<number[]>([]),
    ownerUserId: this.fb.control<number | null>(null),
    isActive: this.fb.nonNullable.control(true),
    initialVersionStatus: this.fb.nonNullable.control<DocumentStatus>('BROUILLON'),
    initialRevisionComment: this.fb.control<string>(''),
    initialEffectiveDate: this.fb.control<Date | null>(null),
    initialExpiryDate: this.fb.control<Date | null>(null),
    signature: this.fb.control<string | null>(null),
    inputMode: this.fb.nonNullable.control<'upload' | 'online'>('upload'),
    onlineTemplate: this.fb.nonNullable.control<'simple' | 'attendance' | 'announcement' | 'meeting' | 'evaluation'>('simple'),
    announcementSubject: this.fb.control<string>(''),
    announcementBody: this.fb.control<string>(''),
    announcementTarget: this.fb.control<string>(''),
    attendanceDate: this.fb.control<Date | null>(null),
    attendanceRows: this.fb.array([]),
    meetingSubject: this.fb.control<string>(''),
    meetingDate: this.fb.control<Date | null>(null),
    meetingAgenda: this.fb.control<string>(''),
    meetingDecisions: this.fb.control<string>(''),
    meetingParticipants: this.fb.array([]),
    evaluationSubject: this.fb.control<string>(''),
    evaluationDate: this.fb.control<Date | null>(null),
    evaluationEvaluator: this.fb.control<string>(''),
    evaluationRecommendations: this.fb.control<string>(''),
    evaluationCriteria: this.fb.array([])
  });

  loading = false;
  saving = false;
  isEdit = false;
  documentId: number | null = null;
  selectedFile: File | null = null;
  processes: ProcessListItemResponse[] = [];
  procedures: ProcedureListItemResponse[] = [];
  owners: UserResponse[] = [];
  existingDocuments: DocumentListItemResponse[] = [];
  startWithImport = false;
  signaturePreview: string | null = null;
  activeTab = 0;

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly documentService: DocumentService,
    private readonly processService: ProcessService,
    private readonly procedureService: ProcedureService,
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

    // Auto-generate code when title or type changes
    this.documentForm.controls.title.valueChanges.subscribe(() => this.autoGenerateCode());
    this.documentForm.controls.type.valueChanges.subscribe(() => this.autoGenerateCode());

    // Add duplicate code validator
    this.documentForm.controls.code.addValidators(this.duplicateCodeValidator());

    if (!this.canValidateStatus) {
      this.documentForm.controls.initialVersionStatus.disable({ emitEvent: false });
    }
    if (!this.isEdit) {
      this.documentForm.controls.initialEffectiveDate.setValue(new Date());
    }

    this.loading = true;
    const currentUser = this.authService.getCurrentUser();

    // Pour UTILISATEUR : le backend filtre automatiquement par acteur/pilote du processus
    // Pour ADMIN/RESPONSABLE : tous les processus de l'organisation
    const processParams = { pageNumber: 1, pageSize: 300 };

    const baseData$ = forkJoin({
      processes: this.processService.getProcesses(processParams),
      users: this.canSelectOwner
        ? this.userService.getAll(1, 300)
        : of<UserListResponse>({ total: 0, page: 1, pageSize: 0, items: [] }),
      documents: this.documentService.getDocuments({ pageNumber: 1, pageSize: 500 })
    });

    if (this.isEdit && this.documentId) {
      forkJoin({
        base: baseData$,
        details: this.documentService.getDocumentById(this.documentId)
      }).subscribe({
        next: ({ base, details }) => {
          this.owners = base.users.items.filter(user => user.isActive);
          this.existingDocuments = base.documents.items;

          if (!this.canSelectOwner && currentUser) {
            this.processes = base.processes.items.filter(p => p.pilotUserId === currentUser.id);
          } else {
            this.processes = base.processes.items;
          }

          if (details.document.ownerUserId) {
            this.ensurePilotInOwners(details.document.ownerUserId, details.document.ownerFullName ?? null);
          }

          this.patchDocument(details.document);

          if (currentUser) {
            this.ensureCurrentUserAsOwnerOption(currentUser);
          }

          // Disable ownerUserId control only if user cannot select owner
          if (!this.canSelectOwner) {
            this.documentForm.controls.ownerUserId.disable({ emitEvent: false });
          }

          const pIds = details.document.processIds && details.document.processIds.length > 0
            ? details.document.processIds
            : (details.document.processId ? [details.document.processId] : []);

          const currentProcIds = details.document.procedureIds && details.document.procedureIds.length > 0
            ? details.document.procedureIds
            : (details.document.procedureId ? [details.document.procedureId] : []);

          if (pIds.length > 0) {
            this.documentForm.controls.procedureIds.enable({ emitEvent: false });
            const obsList = pIds.map(pId => this.procedureService.getProceduresByProcess(pId));
            forkJoin(obsList).subscribe({
              next: (results) => {
                this.procedures = results.reduce((acc, curr) => acc.concat(curr), []);
                this.documentForm.controls.procedureIds.setValue(currentProcIds);
                this.loading = false;
              },
              error: () => {
                this.procedures = [];
                this.loading = false;
                this.notificationService.showWarning('Impossible de charger les procedures.');
              }
            });
          } else {
            this.documentForm.controls.procedureIds.disable({ emitEvent: false });
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
      next: ({ processes, users, documents }) => {
        this.owners = users.items.filter(user => user.isActive);
        this.existingDocuments = documents.items;

        if (!this.canSelectOwner && currentUser) {
          this.processes = processes.items.filter(p => p.pilotUserId === currentUser.id);
        } else {
          this.processes = processes.items;
        }

        if (currentUser) {
          this.ensureCurrentUserAsOwnerOption(currentUser);
        }

        // Disable ownerUserId control only if user cannot select owner
        if (!this.canSelectOwner) {
          this.documentForm.controls.ownerUserId.disable({ emitEvent: false });
        }
        this.documentForm.controls.procedureIds.disable({ emitEvent: false });

        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les donnees de formulaire.');
      }
    });

    // Auto-select responsible from procedure
    this.documentForm.controls.procedureIds.valueChanges.subscribe(procedureIds => {
      if (!procedureIds || procedureIds.length === 0) {
        return;
      }

      const selectedProcedure = this.procedures.find(item => item.id === procedureIds[0]);
      if (selectedProcedure?.responsibleUserId) {
        this.documentForm.controls.ownerUserId.setValue(selectedProcedure.responsibleUserId);
      }
    });
  }

  get attendanceRows(): FormArray {
    return this.documentForm.get('attendanceRows') as FormArray;
  }

  get meetingParticipants(): FormArray {
    return this.documentForm.get('meetingParticipants') as FormArray;
  }

  get evaluationCriteria(): FormArray {
    return this.documentForm.get('evaluationCriteria') as FormArray;
  }

  addTeacherRow(name = '', status: 'PRESENT' | 'ABSENT' | 'LATE' = 'PRESENT'): void {
    this.attendanceRows.push(
      this.fb.group({
        teacherName: [name, Validators.required],
        status: [status, Validators.required]
      })
    );
  }

  removeTeacherRow(index: number): void {
    this.attendanceRows.removeAt(index);
  }

  addParticipantRow(name = ''): void {
    this.meetingParticipants.push(
      this.fb.group({
        name: [name, Validators.required]
      })
    );
  }

  removeParticipantRow(index: number): void {
    this.meetingParticipants.removeAt(index);
  }

  addCriterionRow(criterion = '', rating = 5, comment = ''): void {
    this.evaluationCriteria.push(
      this.fb.group({
        criterion: [criterion, Validators.required],
        rating: [rating, [Validators.required, Validators.min(1), Validators.max(5)]],
        comment: [comment]
      })
    );
  }

  removeCriterionRow(index: number): void {
    this.evaluationCriteria.removeAt(index);
  }

  generateTextFileFromForm(): File | null {
    const raw = this.documentForm.getRawValue() as any;
    if (raw.inputMode !== 'online') {
      return null;
    }

    let text = '';
    const nowStr = new Date().toLocaleDateString('fr-FR');

    if (raw.onlineTemplate === 'attendance') {
      const dateVal = raw.attendanceDate ? new Date(raw.attendanceDate).toLocaleDateString('fr-FR') : nowStr;
      text += `LISTE DE PRESENCE DES ENSEIGNANTS\n`;
      text += `Date : ${dateVal}\n\n`;
      text += `------------------------------------------------------------\n`;
      text += `| Enseignant                           | Statut            |\n`;
      text += `------------------------------------------------------------\n`;

      const rows = this.attendanceRows.value || [];
      if (rows.length === 0) {
        text += `| Aucun enseignant répertorié          |                   |\n`;
      } else {
        rows.forEach((row: any) => {
          const name = (row.teacherName || '').padEnd(36);
          const status = (row.status === 'PRESENT' ? 'Présent' : row.status === 'ABSENT' ? 'Absent' : 'En retard').padEnd(17);
          text += `| ${name} | ${status} |\n`;
        });
      }
      text += `------------------------------------------------------------\n\n`;
      if (raw.description) {
        text += `Commentaires / Observations :\n${raw.description}\n`;
      }
    } else if (raw.onlineTemplate === 'announcement') {
      text += `ANNONCE OFFICIELLE\n`;
      text += `Sujet : ${raw.announcementSubject || 'Sans sujet'}\n`;
      text += `Date de publication : ${nowStr}\n`;
      text += `Destinataire : ${raw.announcementTarget || 'Tout le personnel'}\n\n`;
      text += `Contenu :\n`;
      text += `${raw.announcementBody || ''}\n\n`;
      if (raw.description) {
        text += `Notes additionnelles :\n${raw.description}\n`;
      }
    } else if (raw.onlineTemplate === 'meeting') {
      const dateVal = raw.meetingDate ? new Date(raw.meetingDate).toLocaleDateString('fr-FR') : nowStr;
      text += `COMPTE-RENDU DE REUNION\n`;
      text += `Sujet : ${raw.meetingSubject || 'Sans objet'}\n`;
      text += `Date de la réunion : ${dateVal}\n\n`;

      text += `PARTICIPANTS :\n`;
      const parts = this.meetingParticipants.value || [];
      if (parts.length === 0) {
        text += `- Aucun participant enregistré\n`;
      } else {
        parts.forEach((p: any) => {
          text += `- ${p.name || ''}\n`;
        });
      }
      text += `\n`;

      text += `ORDRE DU JOUR :\n`;
      text += `${raw.meetingAgenda || 'Non défini'}\n\n`;

      text += `DELIBERATIONS & DECISIONS :\n`;
      text += `${raw.meetingDecisions || 'Non définies'}\n\n`;

      if (raw.description) {
        text += `Remarques additionnelles :\n${raw.description}\n`;
      }
    } else if (raw.onlineTemplate === 'evaluation') {
      const dateVal = raw.evaluationDate ? new Date(raw.evaluationDate).toLocaleDateString('fr-FR') : nowStr;
      text += `FICHE D'EVALUATION QUALITE\n`;
      text += `Objet de l'évaluation : ${raw.evaluationSubject || 'Non spécifié'}\n`;
      text += `Date de l'évaluation : ${dateVal}\n`;
      text += `Évaluateur : ${raw.evaluationEvaluator || 'Non spécifié'}\n\n`;

      text += `CRITERES D'EVALUATION :\n`;
      text += `--------------------------------------------------------------------------------\n`;
      text += `| Critère                                   | Note      | Commentaires          |\n`;
      text += `--------------------------------------------------------------------------------\n`;

      const criteria = this.evaluationCriteria.value || [];
      if (criteria.length === 0) {
        text += `| Aucun critère évalué                      | -         | -                     |\n`;
      } else {
        criteria.forEach((c: any) => {
          const crit = (c.criterion || '').padEnd(41);
          const rat = `${c.rating}/5`.padEnd(9);
          const comm = (c.comment || '').padEnd(21);
          text += `| ${crit} | ${rat} | ${comm} |\n`;
        });
      }
      text += `--------------------------------------------------------------------------------\n\n`;

      text += `RECOMMANDATIONS / ACTIONS A MENER :\n`;
      text += `${raw.evaluationRecommendations || 'Aucune recommandation'}\n\n`;

      if (raw.description) {
        text += `Observations :\n${raw.description}\n`;
      }
    } else {
      // Simple template
      text += `ENREGISTREMENT DE QUALITE\n`;
      text += `Date : ${nowStr}\n\n`;
      text += `Contenu :\n`;
      text += `${raw.description || 'Aucun contenu fourni.'}\n`;
    }

    const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
    return new File([blob], `${raw.code || 'document'}.txt`, { type: 'text/plain;charset=utf-8' });
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

  get isResponsableQualite(): boolean {
    return this.authService.hasRole(['RESPONSABLE_QUALITE']);
  }

  get isAdminOrg(): boolean {
    return this.authService.hasRole(['ADMIN_ORG']);
  }



  private getTodayInputDate(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = `${now.getMonth() + 1}`.padStart(2, '0');
    const day = `${now.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  onProcessChanged(): void {
    const processIds = this.documentForm.controls.processIds.value || [];
    this.documentForm.controls.procedureIds.setValue([]);

    if (processIds.length === 0) {
      this.procedures = [];
      this.documentForm.controls.procedureIds.disable({ emitEvent: false });
      return;
    }

    this.documentForm.controls.procedureIds.enable({ emitEvent: false });

    // Fetch procedures for all selected processes
    const obsList = processIds.map(pId => this.procedureService.getProceduresByProcess(pId));
    forkJoin(obsList).subscribe({
      next: (results) => {
        this.procedures = results.reduce((acc, curr) => acc.concat(curr), []);

        // Auto-select process owner (pilotUserId) of the first selected process
        const selectedProcess = this.processes.find(p => p.id === processIds[0]);
        if (selectedProcess?.pilotUserId) {
          this.ensurePilotInOwners(selectedProcess.pilotUserId, selectedProcess.pilotFullName ?? null);
          this.documentForm.controls.ownerUserId.setValue(selectedProcess.pilotUserId);
        }
      },
      error: () => {
        this.procedures = [];
        this.notificationService.showWarning('Impossible de charger les procedures des processus.');
      }
    });
  }

  onFileSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0] ?? null;
    if (!this.assignSelectedFile(file)) {
      target.value = '';
    }
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

    if (this.documentForm.controls.inputMode.value === 'online') {
      const raw = this.documentForm.getRawValue() as any;
      if (raw.onlineTemplate === 'attendance' && this.attendanceRows.length === 0) {
        this.notificationService.showWarning("Veuillez ajouter au moins un enseignant à la liste de présence.");
        return;
      }
      if (raw.onlineTemplate === 'announcement' && (!raw.announcementSubject || !raw.announcementBody)) {
        this.notificationService.showWarning("Veuillez remplir le sujet et le contenu de l'annonce.");
        return;
      }
      if (raw.onlineTemplate === 'meeting' && (!raw.meetingSubject || !raw.meetingAgenda || this.meetingParticipants.length === 0)) {
        this.notificationService.showWarning("Veuillez remplir le sujet, l'ordre du jour et ajouter au moins un participant pour la réunion.");
        return;
      }
      if (raw.onlineTemplate === 'evaluation' && (!raw.evaluationSubject || this.evaluationCriteria.length === 0)) {
        this.notificationService.showWarning("Veuillez remplir le sujet de l'évaluation et ajouter au moins un critère.");
        return;
      }
      if (raw.onlineTemplate === 'simple' && !raw.description) {
        this.notificationService.showWarning("Veuillez saisir le contenu de l'enregistrement.");
        return;
      }

      const generated = this.generateTextFileFromForm();
      if (generated) {
        this.selectedFile = generated;
      }
    }

    if (!this.isEdit && !this.selectedFile) {
      this.activeTab = 3;
      this.notificationService.showWarning('Veuillez déposer un fichier ou remplir le document en ligne.');
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

  private assignSelectedFile(file: File | null): boolean {
    if (!file) {
      this.selectedFile = null;
      return true;
    }

    if (!this.isAllowedDocumentFile(file)) {
      this.selectedFile = null;
      this.notificationService.showWarning(`Format non autorise. Deposez uniquement: ${this.allowedFileFormatsLabel}.`);
      return false;
    }

    this.selectedFile = file;
    return true;
  }

  private isAllowedDocumentFile(file: File): boolean {
    const name = file.name.toLowerCase();
    return name.endsWith('.pdf') || name.endsWith('.docx') || name.endsWith('.xlsx') || name.endsWith('.txt');
  }

  private buildDocumentPayload(): CreateDocumentRequest {
    const raw = this.documentForm.getRawValue();

    const resolvedProcessIds = new Set<number>();
    if (raw.procedureIds && raw.procedureIds.length > 0) {
      raw.procedureIds.forEach(procId => {
        const proc = this.procedures.find(p => p.id === procId);
        if (proc?.processId) {
          resolvedProcessIds.add(proc.processId);
        }
      });
    }

    const processIdsArray = Array.from(resolvedProcessIds);

    return {
      processId: processIdsArray.length > 0 ? processIdsArray[0] : null,
      procedureId: raw.procedureIds && raw.procedureIds.length > 0 ? raw.procedureIds[0] : null,
      processIds: processIdsArray,
      procedureIds: raw.procedureIds || [],
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
      status: this.canValidateStatus ? raw.initialVersionStatus : 'EN_REVISION',
      revisionComment: raw.initialRevisionComment?.trim() || null,
      effectiveDate: this.formatDateForApi(raw.initialEffectiveDate) || this.getTodayInputDate(),
      expiryDate: this.formatDateForApi(raw.initialExpiryDate),
      signature: raw.signature
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
      processIds: document.processIds ?? (document.processId ? [document.processId] : []),
      procedureIds: document.procedureIds ?? (document.procedureId ? [document.procedureId] : []),
      ownerUserId: document.ownerUserId ?? null,
      isActive: document.isActive,
      initialVersionStatus: document.currentVersionStatus ?? 'BROUILLON',
      initialRevisionComment: '',
      initialEffectiveDate: document.currentVersionNumber ? new Date() : null,
      initialExpiryDate: null,
      signature: document.signature ?? null
    });
    if (document.ownerUserId) {
      this.ensurePilotInOwners(document.ownerUserId, document.ownerFullName ?? null);
    }
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
        isActive: currentUser.isActive,
        createdAt: currentUser.createdAt
      },
      ...this.owners
    ];
  }

  private ensurePilotInOwners(pilotUserId: number, pilotFullName: string | null): void {
    const alreadyExists = this.owners.some(owner => owner.id === pilotUserId);
    if (alreadyExists) {
      return;
    }

    const parts = (pilotFullName ?? '').trim().split(' ');
    const firstName = parts[0] || 'Pilote';
    const lastName = parts.slice(1).join(' ') || 'Processus';

    this.owners = [
      {
        id: pilotUserId,
        firstName,
        lastName,
        email: '',
        role: 'UTILISATEUR',
        isActive: true,
        createdAt: new Date().toISOString()
      } as any,
      ...this.owners
    ];
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

  private getTypePrefix(type: DocumentType): string {
    switch (type) {
      case 'MANUEL': return 'MNL';
      case 'PROCEDURE': return 'PRC';
      case 'INSTRUCTION': return 'INS';
      case 'ENREGISTREMENT': return 'ENR';
      case 'FORMULAIRE': return 'FOR';
      default: return 'DOC';
    }
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
    const codeCtrl = this.documentForm.controls.code;

    // If the field is dirty (manually changed by the user) and is not empty, don't overwrite it
    if (codeCtrl.dirty && codeCtrl.value) {
      return;
    }

    // If editing and we already have a value, don't overwrite it
    if (this.isEdit && codeCtrl.value) {
      return;
    }

    const title = this.documentForm.controls.title.value;
    const type = this.documentForm.controls.type.value;

    if (!title) {
      codeCtrl.setValue('', { emitEvent: false });
      return;
    }

    const typePrefix = this.getTypePrefix(type);
    const titleCode = this.generateTitleCode(title);
    const year = new Date().getFullYear();

    const generatedCode = `${typePrefix}-${titleCode}-${year}`;

    // Prevent duplicate codes
    let finalCode = generatedCode;
    let counter = 1;
    while (this.existingDocuments.some(doc => doc.code.toUpperCase() === finalCode.toUpperCase() && doc.id !== this.documentId)) {
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

      const exists = this.existingDocuments.some(
        doc => doc.code.toUpperCase() === value.trim().toUpperCase() && doc.id !== this.documentId
      );

      return exists ? { duplicateCode: true } : null;
    };
  }

  setActiveTab(index: number): void {
    this.activeTab = index;
  }

  nextTab(): void {
    if (this.activeTab < 3) {
      this.activeTab++;
    }
  }

  prevTab(): void {
    if (this.activeTab > 0) {
      this.activeTab--;
    }
  }
}
