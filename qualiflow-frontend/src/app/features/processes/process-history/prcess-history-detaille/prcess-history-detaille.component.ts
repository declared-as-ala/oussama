import { Component, EventEmitter, Inject, Input, OnInit, Optional, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { ProcessActionLogResponse } from '../../models/process.models';
import { ProcessService } from '../../services/process.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-prcess-history-detaille',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    MatCardModule,
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe
  ],
  templateUrl: './prcess-history-detaille.component.html',
  styleUrls: ['./prcess-history-detaille.component.scss']
})
export class PrcessHistoryDetailleComponent implements OnInit {
  @Input() log!: ProcessActionLogResponse;
  @Input() processId!: number;
  @Output() logDeleted = new EventEmitter<number>();
  @Output() closed = new EventEmitter<void>();

  isModal = false;
  deleting = false;

  constructor(
    @Optional() private readonly dialogRef: MatDialogRef<PrcessHistoryDetailleComponent>,
    @Optional() @Inject(MAT_DIALOG_DATA) public data: { log: ProcessActionLogResponse; processId: number },
    private readonly processService: ProcessService,
    private readonly notificationService: NotificationService
  ) {
    if (data?.log) {
      this.log = data.log;
      this.processId = data.processId;
      this.isModal = true;
    }
  }

  ngOnInit(): void {
    if (!this.log) {
      throw new Error('ProcessActionLogResponse is required for PrcessHistoryDetailleComponent.');
    }
  }

  close(): void {
    if (this.isModal && this.dialogRef) {
      this.dialogRef.close();
    } else {
      this.closed.emit();
    }
  }

  deleteLog(): void {
    if (confirm('Êtes-vous sûr de vouloir supprimer définitivement cette entrée d\'audit ? Cette action est irréversible.')) {
      this.deleting = true;
      this.processService.deleteActionLog(this.processId, this.log.id).subscribe({
        next: () => {
          this.deleting = false;
          this.notificationService.showSuccess('Journal d\'actions supprimé de l\'historique avec succès.');
          this.logDeleted.emit(this.log.id);
          this.close();
        },
        error: () => {
          this.deleting = false;
          this.notificationService.showError('Échec de la suppression de l\'audit log.');
        }
      });
    }
  }

  // Helper functions
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
      case 'PROCESS_CREATED': return 'Processus créé';
      case 'PROCESS_UPDATED': return 'Processus modifié';
      case 'PROCESS_DELETED': return 'Processus supprimé';
      case 'STATUS_TOGGLED': return 'Statut modifié';
      case 'PILOT_UPDATED': return 'Pilote mis à jour';
      case 'ACTORS_ASSIGNED': return 'Acteurs assignés';
      case 'ACTOR_REMOVED': return 'Acteur retiré';
      default: return actionType.replace(/_/g, ' ').toLowerCase();
    }
  }

  getActionIcon(actionType: string): string {
    if (actionType.includes('CREATED')) return 'add_circle_outline';
    if (actionType.includes('DELETED')) return 'delete_outline';
    if (actionType.includes('UPDATED')) return 'edit';
    if (actionType.includes('STATUS')) return 'published_with_changes';
    if (actionType.includes('PILOT')) return 'person_outline';
    if (actionType.includes('ACTOR')) return 'people_outline';
    return 'history';
  }
}
