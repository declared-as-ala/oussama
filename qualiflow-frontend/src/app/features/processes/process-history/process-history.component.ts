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
import { ProcessService } from '../services/process.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { 
  ProcessDetailsResponse, 
  ProcessActionLogResponse 
} from '../models/process.models';
import { PrcessHistoryDetailleComponent } from './prcess-history-detaille/prcess-history-detaille.component';

export interface PilotingPeriod {
  pilotName: string;
  startDate: Date;
  endDate: Date | null;
}

@Component({
  selector: 'app-process-history',
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
    PrcessHistoryDetailleComponent
  ],
  templateUrl: './process-history.component.html',
  styleUrls: ['./process-history.component.scss']
})
export class ProcessHistoryComponent implements OnInit {
  loading = false;
  processId!: number;
  details: ProcessDetailsResponse | null = null;
  actionLogs: ProcessActionLogResponse[] = [];
  creationLog: ProcessActionLogResponse | null = null;
  lastLog: ProcessActionLogResponse | null = null;
  totalActions = 0;
  
  // Search & Filter state
  searchText = '';
  selectedCategory = 'ALL';

  // Details sidebar state
  selectedLog: ProcessActionLogResponse | null = null;

  get pilotingTimeline(): PilotingPeriod[] {
    if (!this.details) return [];

    const creationDate = new Date(this.details.process.createdAt);
    const timeline: PilotingPeriod[] = [];

    // 1. Get all PILOT_UPDATED logs in ascending order (oldest first)
    const pilotLogs = this.actionLogs
      .filter(log => log.actionType === 'PILOT_UPDATED')
      .sort((a, b) => new Date(a.performedAt).getTime() - new Date(b.performedAt).getTime());

    const parsePilotName = (val: string | null | undefined): string => {
      if (!val) return 'Aucun';
      // Format is: "ID: 3, Nom: Responsable Qualite" or similar
      const match = val.match(/Nom:\s*(.*)$/i);
      return match ? match[1].trim() : val;
    };

    if (pilotLogs.length === 0) {
      // No changes. Current pilot has been active since creation.
      timeline.push({
        pilotName: this.details.process.pilotFullName || 'Aucun pilote',
        startDate: creationDate,
        endDate: null
      });
    } else {
      // The very first pilot is the oldValue of the first pilotLog
      const firstPilot = parsePilotName(pilotLogs[0].oldValue);
      
      // Add the first piloting period: from creation until the first change
      timeline.push({
        pilotName: firstPilot,
        startDate: creationDate,
        endDate: new Date(pilotLogs[0].performedAt)
      });

      // Add intermediate periods
      for (let i = 0; i < pilotLogs.length - 1; i++) {
        const currentLog = pilotLogs[i];
        const nextLog = pilotLogs[i + 1];
        timeline.push({
          pilotName: parsePilotName(currentLog.newValue),
          startDate: new Date(currentLog.performedAt),
          endDate: new Date(nextLog.performedAt)
        });
      }

      // Add the final active period: from the last change until present
      const lastLog = pilotLogs[pilotLogs.length - 1];
      timeline.push({
        pilotName: parsePilotName(lastLog.newValue),
        startDate: new Date(lastLog.performedAt),
        endDate: null
      });
    }

    return timeline;
  }

  formatPeriodDate(date: Date | string | null): string {
    if (!date) return 'Présent';
    const d = new Date(date);
    return d.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }

  getDurationText(start: Date, end: Date | null): string {
    const s = new Date(start);
    const e = end ? new Date(end) : new Date();
    const diffMs = e.getTime() - s.getTime();
    const diffDays = Math.max(1, Math.round(diffMs / (1000 * 60 * 60 * 24)));
    
    if (diffDays < 30) {
      return `${diffDays} jour${diffDays > 1 ? 's' : ''}`;
    }
    const diffMonths = Math.round(diffDays / 30.4);
    if (diffMonths < 12) {
      return `${diffMonths} mois`;
    }
    const diffYears = Math.floor(diffMonths / 12);
    const remMonths = diffMonths % 12;
    return remMonths > 0 
      ? `${diffYears} an${diffYears > 1 ? 's' : ''} et ${remMonths} mois` 
      : `${diffYears} an${diffYears > 1 ? 's' : ''}`;
  }

  @Input() inputProcessId?: number;
  readonly displayedActionLogColumns: string[] = ['action', 'user', 'date', 'comment', 'actions'];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly processService: ProcessService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
  ) {}

  ngOnInit(): void {
    if (this.inputProcessId) {
      this.processId = this.inputProcessId;
      this.loadData();
      return;
    }

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

    forkJoin({
      details: this.processService.getProcessById(this.processId),
      logs: this.processService.getActionLogs(this.processId)
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
        this.router.navigate(['/processes']);
      }
    });
  }

  get filteredActionLogs(): ProcessActionLogResponse[] {
    let filtered = this.actionLogs;

    // Filter by category
    if (this.selectedCategory !== 'ALL') {
      filtered = filtered.filter(log => {
        const type = log.actionType;
        switch (this.selectedCategory) {
          case 'PILOTAGE':
            return type.includes('PILOT') || type.includes('ACTOR');
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
        case 'PILOTAGE':
          return type.includes('PILOT') || type.includes('ACTOR');
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

  selectLog(log: ProcessActionLogResponse): void {
    this.viewLogDetails(log);
  }

  viewLogDetails(log: ProcessActionLogResponse): void {
    this.dialog.open(PrcessHistoryDetailleComponent, {
      width: '650px',
      data: { log, processId: this.processId }
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
      case 'PROCESS_CREATED':
        return 'Processus créé';
      case 'PROCESS_UPDATED':
        return 'Processus modifié';
      case 'PROCESS_DELETED':
        return 'Processus supprimé';
      case 'STATUS_TOGGLED':
        return 'Statut modifié';
      case 'PILOT_UPDATED':
        return 'Pilote mis à jour';
      case 'ACTORS_ASSIGNED':
        return 'Acteurs assignés';
      case 'ACTOR_REMOVED':
        return 'Acteur retiré';
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

    if (actionType.includes('PILOT')) {
      return 'person_outline';
    }

    if (actionType.includes('ACTOR')) {
      return 'people_outline';
    }

    return 'history';
  }

  backToProcess(): void {
    this.router.navigate(['/processes', this.processId]);
  }

}
