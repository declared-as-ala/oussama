import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserResponse, UserService } from '../../../core/services/user.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ProcessService } from '../services/process.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import {
  AssignProcessActorItem,
  PROCESS_ACTOR_TYPE_OPTIONS,
  ProcessActorResponse,
  ProcessDetailsResponse,
  ProcessActorType
} from '../models/process.models';

@Component({
  selector: 'app-process-actors',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe
  ],
  templateUrl: './process-actors.component.html',
  styleUrls: ['./process-actors.component.scss']
})
export class ProcessActorsComponent implements OnInit {
  loading = false;
  processId!: number;
  details: ProcessDetailsResponse | null = null;
  users: UserResponse[] = [];
  
  readonly actorTypeOptions = PROCESS_ACTOR_TYPE_OPTIONS;
  readonly displayedActorColumns: string[] = ['fullName', 'email', 'function', 'actorType', 'assignedAt', 'actions'];

  get assignableActorTypeOptions() {
    return this.actorTypeOptions.filter(option => option.value !== 'PILOTE' && option.value !== 'PILOTE_PROCEDURE');
  }

  readonly actorForm = this.fb.group({
    userId: [null as number | null, Validators.required],
    actorType: ['CONTRIBUTEUR' as ProcessActorType, Validators.required]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly processService: ProcessService,
    private readonly userService: UserService,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly dialog: MatDialog
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
    this.loadDetails();
  }

  get canWrite(): boolean {
    if (this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE'])) {
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

  get availableUsers(): UserResponse[] {
    const pilotId = this.details?.process.pilotUserId;
    const existingIds = this.details?.actors.map(actor => actor.userId) ?? [];
    
    return this.users.filter(user => {
      if (user.id === pilotId) return false;
      return !existingIds.includes(user.id);
    });
  }

  loadDetails(): void {
    this.loading = true;

    forkJoin({
      details: this.processService.getProcessById(this.processId),
      users: this.userService.getAll(1, 300)
    }).subscribe({
      next: ({ details, users }) => {
        this.details = details;
        this.users = users.items;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger les acteurs du processus.');
        this.router.navigate(['/processes']);
      }
    });
  }

  addActor(): void {
    if (!this.details) {
      return;
    }

    if (this.actorForm.invalid) {
      this.actorForm.markAllAsTouched();
      return;
    }

    const raw = this.actorForm.getRawValue();
    const userId = raw.userId;
    const actorType = raw.actorType;

    if (!userId || !actorType) {
      return;
    }

    const duplicate = this.details.actors.some(actor => actor.userId === userId);
    if (duplicate) {
      this.notificationService.showWarning('Cet utilisateur est deja acteur du processus.');
      return;
    }

    const actorsPayload: AssignProcessActorItem[] = [
      ...this.details.actors.map(actor => ({
        userId: actor.userId,
        actorType: actor.actorType
      })),
      {
        userId,
        actorType
      }
    ];

    this.processService.assignActors(this.processId, { actors: actorsPayload }).subscribe({
      next: (actors) => {
        if (!this.details) {
          return;
        }

        this.details.actors = actors;
        this.actorForm.reset({ userId: null, actorType: 'CONTRIBUTEUR' });
        this.notificationService.showSuccess('Acteur ajoute au processus.');
      },
      error: () => {
        this.notificationService.showError('Ajout de l acteur impossible.');
      }
    });
  }

  removeActor(actor: ProcessActorResponse): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Retirer cet acteur',
        message: `Confirmer le retrait de ${actor.fullName} ?`,
        confirmText: 'Retirer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.processService.removeActor(this.processId, actor.userId).subscribe({
        next: () => {
          if (!this.details) {
            return;
          }

          this.details.actors = this.details.actors.filter(item => item.userId !== actor.userId);
          this.notificationService.showSuccess('Acteur retire du processus.');
        },
        error: () => {
          this.notificationService.showError('Suppression de l acteur impossible.');
        }
      });
    });
  }

  getActorTypeLabel(actorType: string): string {
    return this.actorTypeOptions.find(option => option.value === actorType)?.label ?? actorType;
  }

  backToProcess(): void {
    this.router.navigate(['/processes', this.processId]);
  }
}
