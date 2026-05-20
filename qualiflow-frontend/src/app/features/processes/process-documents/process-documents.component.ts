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
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { ProcessService } from '../services/process.service';
import { ProcedureService } from '../../procedures/services/procedure.service';
import { DocumentService } from '../../documents/services/document.service';
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
    private readonly notificationService: NotificationService
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
        this.allDocuments = documents.items.filter(d => d.processId === this.processId);
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
}
