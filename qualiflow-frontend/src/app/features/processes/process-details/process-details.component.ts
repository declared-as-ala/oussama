import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UserResponse, UserService } from '../../../core/services/user.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  PROCESS_ACTOR_TYPE_OPTIONS,
  PROCESS_STATUS_OPTIONS,
  PROCESS_TYPE_OPTIONS,
  ProcessDetailsResponse,
  ProcessType,
  ProcessActorResponse
} from '../models/process.models';
import { ProcessService } from '../services/process.service';
import { PagedProcedureResponse, ProcedureListItemResponse } from '../../procedures/models/procedure.models';
import { ProcedureService } from '../../procedures/services/procedure.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { ProcessActorsComponent } from '../process-actors/process-actors.component';
import { ProcessHistoryComponent } from '../process-history/process-history.component';
import { ProcessDocumentsComponent } from '../process-documents/process-documents.component';
import { IndicatorService } from '../../indicators/services/indicator.service';
import { IndicatorListItemResponse } from '../../indicators/models/indicator.models';

@Component({
  selector: 'app-process-details',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatTooltipModule,
    MatMenuModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
    ProcessActorsComponent,
    ProcessDocumentsComponent
  ],
  templateUrl: './process-details.component.html',
  styleUrls: ['./process-details.component.scss']
})
export class ProcessDetailsComponent implements OnInit {
  loading = false;
  activeTab = 0;
  processId!: number;
  details: ProcessDetailsResponse | null = null;
  users: UserResponse[] = [];
  procedures: ProcedureListItemResponse[] = [];
  indicators: IndicatorListItemResponse[] = [];

  // Procedure Popper state
  allAvailableProcedures: ProcedureListItemResponse[] = [];
  procedureSearchTerm = '';
  selectedProcedureToLink: number | null = null;
  linkingProcedure = false;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly processService: ProcessService,
    private readonly procedureService: ProcedureService,
    private readonly indicatorService: IndicatorService,
    private readonly userService: UserService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
  ) { }

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
      (actor: ProcessActorResponse) => actor.userId === currentUserId &&
        (actor.actorType === 'PILOTE' || actor.actorType === 'COPILOTE')
    );
  }

  get hasData(): boolean {
    return this.details !== null;
  }



  loadDetails(): void {
    this.loading = true;

    forkJoin({
      details: this.processService.getProcessById(this.processId),
      users: this.userService.getAll(1, 300),
      procedures: this.procedureService.getProceduresByProcess(this.processId),
      indicators: this.indicatorService.getIndicatorsByProcess(this.processId)
    }).subscribe({
      next: ({ details, users, procedures, indicators }) => {
        this.details = details;
        this.users = users.items;
        this.procedures = procedures;
        this.indicators = indicators;
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

  deleteProcess(): void {
    if (!this.details) {
      return;
    }

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer le processus',
        message: `Confirmer la suppression de ${this.details.process.code} - ${this.details.process.name} ?`,
        confirmText: 'Supprimer',
        cancelText: 'Annuler',
        type: 'danger'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.processService.deleteProcess(this.processId).subscribe({
        next: () => {
          this.notificationService.showSuccess('Processus supprime avec succes.');
          this.router.navigate(['/processes']);
        },
        error: () => {
          this.notificationService.showError('Suppression impossible.');
        }
      });
    });
  }

  loadAllAvailableProcedures(): void {
    if (this.allAvailableProcedures.length > 0) return;
    this.procedureService.getProcedures({ pageSize: 200, pageNumber: 1 }).subscribe({
      next: (res: PagedProcedureResponse) => {
        this.allAvailableProcedures = res.items;
      }
    });
  }

  get filteredAvailableProcedures(): ProcedureListItemResponse[] {
    const term = this.procedureSearchTerm.toLowerCase().trim();
    const linkedIds = new Set(this.procedures.map(p => p.id));
    return this.allAvailableProcedures.filter(p =>
      !linkedIds.has(p.id) &&
      (!term || p.title.toLowerCase().includes(term) || p.code.toLowerCase().includes(term))
    );
  }

  getSelectedProcedureCode(): string {
    return this.allAvailableProcedures.find(p => p.id === this.selectedProcedureToLink)?.code || '';
  }

  getSelectedProcedureTitle(): string {
    return this.allAvailableProcedures.find(p => p.id === this.selectedProcedureToLink)?.title || '';
  }

  addProcedureLink(): void {
    if (!this.selectedProcedureToLink) return;

    const code = this.getSelectedProcedureCode();
    const title = this.getSelectedProcedureTitle();
    const confirmed = window.confirm(
      `Confirmer la liaison ?\n\nProcédure : [${code}] ${title}\n\nCette procédure sera associée à ce processus.`
    );
    if (!confirmed) return;

    this.linkingProcedure = true;
    this.procedureService.addProcessLink(this.processId, this.selectedProcedureToLink).subscribe({
      next: () => {
        this.notificationService.showSuccess(`Procédure [${code}] liée avec succès.`);
        this.selectedProcedureToLink = null;
        this.procedureSearchTerm = '';
        this.loadDetails();
        this.linkingProcedure = false;
      },
      error: () => {
        this.notificationService.showError('Impossible de lier la procédure.');
        this.linkingProcedure = false;
      }
    });
  }

  removeProcedureLink(procedureId: number): void {
    const procedure = this.procedures.find(p => p.id === procedureId);
    const label = procedure ? `[${procedure.code}] ${procedure.title}` : `#${procedureId}`;
    const confirmed = window.confirm(
      `Confirmer la déliaison ?\n\nProcédure : ${label}\n\nCette procédure ne sera plus associée à ce processus.`
    );
    if (!confirmed) return;

    this.procedureService.removeProcessLink(this.processId, procedureId).subscribe({
      next: () => {
        this.notificationService.showSuccess(`Procédure ${label} déliée avec succès.`);
        this.loadDetails();
      },
      error: () => {
        this.notificationService.showError('Impossible de délier la procédure.');
      }
    });
  }

  backToList(): void {
    this.router.navigate(['/processes']);
  }

  viewHistory(): void {
    this.router.navigate(['/processes', this.processId, 'history']);
  }

  setActiveTab(index: number): void {
    this.activeTab = index;
  }

  goToActors(): void {
    this.activeTab = 2; // changed from 3
  }

  goToHistory(): void {
    this.viewHistory();
  }

  goToDocuments(): void {
    this.activeTab = 1;
  }

  formatList(values: string[]): string {
    if (!values.length) {
      return 'Aucune donnee.';
    }

    return values.join(' | ');
  }

  getTypeLabel(type: ProcessType): string {
    return PROCESS_TYPE_OPTIONS.find((option: any) => option.value === type)?.label ?? type;
  }

  getStatusLabel(status: string): string {
    return PROCESS_STATUS_OPTIONS.find((option: any) => option.value === status)?.label ?? status;
  }

  getActorTypeLabel(actorType: string): string {
    return PROCESS_ACTOR_TYPE_OPTIONS.find((option: any) => option.value === actorType)?.label ?? actorType;
  }
}
