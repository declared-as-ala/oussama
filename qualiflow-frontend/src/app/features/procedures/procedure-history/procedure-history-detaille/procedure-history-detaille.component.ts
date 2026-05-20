import { Component, EventEmitter, Inject, Input, OnInit, Optional, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { ProcedureActionLogResponse } from '../../models/procedure.models';
import { ProcedureService } from '../../services/procedure.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-procedure-history-detaille',
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
  templateUrl: './procedure-history-detaille.component.html',
  styleUrls: ['./procedure-history-detaille.component.scss']
})
export class ProcedureHistoryDetailleComponent implements OnInit {
  @Input() log!: ProcedureActionLogResponse;
  @Input() procedureId!: number;
  @Output() logDeleted = new EventEmitter<number>();
  @Output() closed = new EventEmitter<void>();

  isModal = false;
  deleting = false;

  constructor(
    @Optional() private readonly dialogRef: MatDialogRef<ProcedureHistoryDetailleComponent>,
    @Optional() @Inject(MAT_DIALOG_DATA) public data: { log: ProcedureActionLogResponse; procedureId: number },
    private readonly procedureService: ProcedureService,
    private readonly notificationService: NotificationService
  ) {
    if (data?.log) {
      this.log = data.log;
      this.procedureId = data.procedureId;
      this.isModal = true;
    }
  }

  ngOnInit(): void {
    if (!this.log) {
      throw new Error('ProcedureActionLogResponse is required for ProcedureHistoryDetailleComponent.');
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
      this.procedureService.deleteActionLog(this.log.id).subscribe({
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
      case 'PROCEDURE_CREATED': return 'Procédure créée';
      case 'PROCEDURE_UPDATED': return 'Procédure modifiée';
      case 'PROCEDURE_DELETED': return 'Procédure supprimée';
      case 'STATUS_TOGGLED': return 'Statut modifié';
      case 'INSTRUCTION_ADDED': return 'Instruction ajoutée';
      case 'INSTRUCTION_UPDATED': return 'Instruction modifiée';
      case 'INSTRUCTION_DELETED': return 'Instruction supprimée';
      default: return actionType.replace(/_/g, ' ').toLowerCase();
    }
  }

  getActionIcon(actionType: string): string {
    if (actionType.includes('CREATED')) return 'add_circle_outline';
    if (actionType.includes('DELETED')) return 'delete_outline';
    if (actionType.includes('UPDATED')) return 'edit';
    if (actionType.includes('STATUS')) return 'published_with_changes';
    if (actionType.includes('INSTRUCTION')) return 'playlist_add';
    return 'history';
  }
}
