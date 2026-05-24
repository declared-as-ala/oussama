import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { ProcessService } from '../services/process.service';
import { ProcedureService } from '../../procedures/services/procedure.service';
import { DocumentService } from '../../documents/services/document.service';
import { AuthService } from '../../../core/services/auth.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { ProcedureListItemResponse } from '../../procedures/models/procedure.models';
import { DocumentListItemResponse } from '../../documents/models/document.models';
import { ProcessDetailsResponse } from '../models/process.models';

@Component({
  selector: 'app-process-documents',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe
  ],
  templateUrl: './process-documents.component.html',
  styleUrls: ['./process-documents.component.scss']
})
export class ProcessDocumentsComponent implements OnInit {
  loading = false;
  processId!: number;
  details: ProcessDetailsResponse | null = null;
  procedures: ProcedureListItemResponse[] = [];
  allDocuments: DocumentListItemResponse[] = [];

  allAvailableDocs: DocumentListItemResponse[] = [];
  docSearchTerm = '';
  selectedDocIdToLink: number | null = null;
  linkingDoc = false;

  /** null = "Tous les documents" */
  selectedProcedureId: number | null = null;
  searchTerm = '';
  showSearch = false;

  readonly displayedColumns: string[] = ['code', 'title', 'procedure', 'status', 'actions'];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly processService: ProcessService,
    private readonly procedureService: ProcedureService,
    private readonly documentService: DocumentService,
    private readonly notificationService: NotificationService,
    private readonly authService: AuthService
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
    this.loadData();
  }

  loadData(): void {
    this.loading = true;
    this.searchTerm = '';
    this.selectedProcedureId = null;

    forkJoin({
      details: this.processService.getProcessById(this.processId),
      procedures: this.procedureService.getProceduresByProcess(this.processId),
      documents: this.documentService.getDocuments({
        pageNumber: 1,
        pageSize: 500,
        processId: this.processId
      })
    }).subscribe({
      next: ({ details, procedures, documents }) => {
        this.details = details;
        this.procedures = procedures;
        this.allDocuments = documents.items.filter(d => d.processId === this.processId || (d.processIds && d.processIds.includes(this.processId)));
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les documents du processus.');
        this.router.navigate(['/processes']);
      }
    });
  }

  selectProcedure(id: number | null): void {
    this.selectedProcedureId = id;
    this.searchTerm = '';
  }

  /** Filtered document list shown in the table */
  get visibleDocuments(): DocumentListItemResponse[] {
    let docs = this.selectedProcedureId === null
      ? this.allDocuments
      : this.allDocuments.filter(d => d.procedureId === this.selectedProcedureId);

    const term = this.searchTerm.toLowerCase().trim();
    if (term) {
      docs = docs.filter(d =>
        d.code.toLowerCase().includes(term) ||
        d.title.toLowerCase().includes(term) ||
        (d.procedureCode || '').toLowerCase().includes(term)
      );
    }

    return docs.sort((a, b) =>
      new Date(b.updatedAt || 0).getTime() - new Date(a.updatedAt || 0).getTime()
    );
  }

  get selectedProcedure(): ProcedureListItemResponse | null {
    return this.procedures.find(p => p.id === this.selectedProcedureId) ?? null;
  }

  getDocumentCount(procedureId: number | null): number {
    return procedureId === null
      ? this.allDocuments.length
      : this.allDocuments.filter(d => d.procedureId === procedureId).length;
  }

  getDocumentStatusClass(status?: string | null): string {
    switch ((status || 'BROUILLON').toUpperCase()) {
      case 'APPROUVE':    return 'conforme';
      case 'EN_REVISION': return 'revision';
      case 'PERIME':      return 'perime';
      default:            return 'gray';
    }
  }

  viewDocument(id: number): void {
    this.router.navigate(['/documents', id]);
  }

  backToProcess(): void {
    this.router.navigate(['/processes', this.processId]);
  }

  toggleSearch(): void {
    this.showSearch = !this.showSearch;
    if (!this.showSearch) {
      this.searchTerm = '';
    }
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

  loadAllAvailableDocs(): void {
    if (this.allAvailableDocs.length > 0) return;
    this.documentService.getDocuments({ pageSize: 500, pageNumber: 1 }).subscribe({
      next: (res) => {
        this.allAvailableDocs = res.items;
      }
    });
  }

  get filteredAvailableDocs(): DocumentListItemResponse[] {
    const term = this.docSearchTerm.toLowerCase().trim();
    const linkedIds = new Set(this.allDocuments.map(d => d.id));
    return this.allAvailableDocs.filter(d =>
      !linkedIds.has(d.id) &&
      (!term || d.title.toLowerCase().includes(term) || d.code.toLowerCase().includes(term))
    );
  }

  addDocumentLink(): void {
    if (!this.selectedDocIdToLink) return;

    const doc = this.allAvailableDocs.find(d => d.id === this.selectedDocIdToLink);
    if (!doc) return;

    const confirmed = window.confirm(
      `Confirmer la liaison ?\n\nDocument : [${doc.code}] ${doc.title}\n\nCe document sera associé à ce processus.`
    );
    if (!confirmed) return;

    this.linkingDoc = true;
    this.processService.addDocumentLink(this.processId, this.selectedDocIdToLink).subscribe({
      next: () => {
        this.notificationService.showSuccess(`Document [${doc.code}] lié avec succès.`);
        this.selectedDocIdToLink = null;
        this.docSearchTerm = '';
        this.loadData();
        this.linkingDoc = false;
      },
      error: () => {
        this.notificationService.showError('Impossible de lier le document.');
        this.linkingDoc = false;
      }
    });
  }

  getSelectedDocCode(): string {
    return this.allAvailableDocs.find(d => d.id === this.selectedDocIdToLink)?.code || '';
  }

  getSelectedDocTitle(): string {
    return this.allAvailableDocs.find(d => d.id === this.selectedDocIdToLink)?.title || '';
  }

  removeDocumentLink(documentId: number): void {
    const doc = this.allDocuments.find(d => d.id === documentId);
    const label = doc ? `[${doc.code}] ${doc.title}` : `#${documentId}`;
    const confirmed = window.confirm(
      `Confirmer la déliaison ?\n\nDocument : ${label}\n\nCe document ne sera plus associé à ce processus.`
    );
    if (!confirmed) return;

    this.processService.removeDocumentLink(this.processId, documentId).subscribe({
      next: () => {
        this.notificationService.showSuccess(`Document ${label} délié avec succès.`);
        this.loadData();
      },
      error: () => {
        this.notificationService.showError('Impossible de délier le document.');
      }
    });
  }
}
