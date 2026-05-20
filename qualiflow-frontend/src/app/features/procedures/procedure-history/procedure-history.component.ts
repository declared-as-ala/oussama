import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { ProcedureService } from '../services/procedure.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { 
  ProcedureDetailsResponse, 
  ProcedureActionLogResponse 
} from '../models/procedure.models';
import { ProcedureHistoryDetailleComponent } from './procedure-history-detaille/procedure-history-detaille.component';

@Component({
  selector: 'app-procedure-history',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatInputModule,
    MatDialogModule,
    TranslatePipe,
    ProcedureHistoryDetailleComponent
  ],
  templateUrl: './procedure-history.component.html',
  styleUrls: ['./procedure-history.component.scss']
})
export class ProcedureHistoryComponent implements OnInit {
  loading = false;
  procedureId!: number;
  details: ProcedureDetailsResponse | null = null;
  actionLogs: ProcedureActionLogResponse[] = [];
  creationLog: ProcedureActionLogResponse | null = null;
  lastLog: ProcedureActionLogResponse | null = null;
  totalActions = 0;
  
  // Search & Filter state
  searchText = '';
  selectedCategory = 'ALL';

  // Details sidebar state
  selectedLog: ProcedureActionLogResponse | null = null;

  @Input() inputprocedureId?: number;

  readonly displayedActionLogColumns: string[] = ['action', 'user', 'date', 'comment', 'actions'];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly procedureService: ProcedureService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
  ) {}

  ngOnInit(): void {
    if (this.inputprocedureId) {
      this.procedureId = this.inputprocedureId;
      this.loadData();
      return;
    }

    const rawId = this.route.snapshot.paramMap.get('id');
    const parsedId = rawId ? Number(rawId) : Number.NaN;

    if (Number.isNaN(parsedId)) {
      this.notificationService.showError('Identifiant de la procédure invalide.');
      this.router.navigate(['/procedures']);
      return;
    }

    this.procedureId = parsedId;
    this.loadData();
  }

  loadData(): void {
    this.loading = true;

    forkJoin({
      details: this.procedureService.getProcedureById(this.procedureId),
      logs: this.procedureService.getActionLogs(this.procedureId)
    }).subscribe({
      next: ({ details, logs }) => {
        this.details = details;
        this.actionLogs = logs;
        this.creationLog = logs.find(log => log.actionType.includes('CREATED') || log.actionType.includes('INITIAL')) || null;
        this.lastLog = logs.length > 0 ? logs[0] : null;
        this.totalActions = logs.length;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.creationLog = null;
        this.lastLog = null;
        this.totalActions = 0;
        this.notificationService.showError('Impossible de charger le journal d\'actions.');
        this.router.navigate(['/procedures']);
      }
    });
  }

  get filteredActionLogs(): ProcedureActionLogResponse[] {
    let filtered = this.actionLogs;

    // Filter by category
    if (this.selectedCategory !== 'ALL') {
      filtered = filtered.filter(log => {
        const type = log.actionType;
        switch (this.selectedCategory) {
          case 'INSTRUCTIONS':
            return type.includes('INSTRUCTION');
          case 'MODIFICATION':
            return type.includes('UPDATED') || type.includes('DELETED');
          case 'CREATION':
            return type.includes('CREATED') || type.includes('INITIAL');
          case 'STATUS':
            return type.includes('STATUS');
          default:
            return true;
        }
      });
    }

    // Filter by search text
    if (this.searchText.trim()) {
      const query = this.searchText.toLowerCase().trim();
      filtered = filtered.filter(log => {
        const actionLabel = this.getActionLabel(log.actionType).toLowerCase();
        const user = (log.performedByFullName || 'système').toLowerCase();
        const comment = (log.comment || '').toLowerCase();
        return actionLabel.includes(query) || user.includes(query) || comment.includes(query);
      });
    }

    return filtered;
  }

  getCategoryCount(category: string): number {
    const logs = this.actionLogs;
    if (category === 'ALL') {
      return logs.length;
    }
    return logs.filter(log => {
      const type = log.actionType;
      switch (category) {
        case 'INSTRUCTIONS':
          return type.includes('INSTRUCTION');
        case 'MODIFICATION':
          return type.includes('UPDATED') || type.includes('DELETED');
        case 'CREATION':
          return type.includes('CREATED') || type.includes('INITIAL');
        case 'STATUS':
          return type.includes('STATUS');
        default:
          return false;
      }
    }).length;
  }

  selectLog(log: ProcedureActionLogResponse): void {
    this.viewLogDetails(log);
  }

  viewLogDetails(log: ProcedureActionLogResponse): void {
    this.dialog.open(ProcedureHistoryDetailleComponent, {
      width: '650px',
      data: { log, procedureId: this.procedureId }
    });
  }

  parseChanges(comment: string | null | undefined): string[] {
    if (!comment) {
      return [];
    }

    if (comment.startsWith("Modifications : ")) {
      return comment.replace("Modifications : ", "").split(" | ");
    }

    if (comment.startsWith("Changements d'acteurs : ")) {
      return comment.replace("Changements d'acteurs : ", "").split(" | ");
    }

    return [comment];
  }

  parseStateValues(value: string | null | undefined): string[] {
    if (!value) {
      return [];
    }

    return value.split(", ").map(v => v.trim());
  }

  getActionLabel(actionType: string): string {
    switch (actionType) {
      case 'PROCEDURE_CREATED':
        return 'Procédure créée';
      case 'PROCEDURE_UPDATED':
        return 'Procédure modifiée';
      case 'PROCEDURE_DELETED':
        return 'Procédure supprimée';
      case 'STATUS_TOGGLED':
        return 'Statut modifié';
      case 'INSTRUCTION_ADDED':
        return 'Instruction ajoutée';
      case 'INSTRUCTION_UPDATED':
        return 'Instruction modifiée';
      case 'INSTRUCTION_DELETED':
        return 'Instruction supprimée';
      default:
        return actionType.replace(/_/g, ' ').toLowerCase();
    }
  }

  getActionIcon(actionType: string): string {
    if (actionType.includes('CREATED')) {
      return 'add_circle_outline';
    }

    if (actionType.includes('DELETED')) {
      return 'delete_outline';
    }

    if (actionType.includes('UPDATED')) {
      return 'edit';
    }

    if (actionType.includes('STATUS')) {
      return 'published_with_changes';
    }

    if (actionType.includes('INSTRUCTION')) {
      return 'playlist_add';
    }

    return 'history';
  }

  backToProcedure(): void {
    this.router.navigate(['/procedures', this.procedureId]);
  }

}
