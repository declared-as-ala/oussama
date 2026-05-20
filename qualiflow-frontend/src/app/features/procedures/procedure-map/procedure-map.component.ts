import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { NotificationService } from '../../../core/services/notification.service';
import { ProcessService } from '../../processes/services/process.service';
import { ProcessMapResponse } from '../../processes/models/process.models';
import { ProcedureService } from '../services/procedure.service';
import { ProcedureListItemResponse, ProcedureStatisticsResponse } from '../models/procedure.models';

@Component({
  selector: 'app-procedure-map',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './procedure-map.component.html',
  styleUrls: ['./procedure-map.component.scss']
})
export class ProcedureMapComponent implements OnInit {
  loading = false;
  map: ProcessMapResponse | null = null;
  procedures: ProcedureListItemResponse[] = [];
  stats: ProcedureStatisticsResponse | null = null;

  constructor(
    private readonly processService: ProcessService,
    private readonly procedureService: ProcedureService,
    private readonly notificationService: NotificationService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.loading = true;

    forkJoin({
      map: this.processService.getProcessMap(),
      procedures: this.procedureService.getProcedures({ pageNumber: 1, pageSize: 999999 }),
      stats: this.procedureService.getProcedureStatistics()
    }).subscribe({
      next: ({ map, procedures, stats }) => {
        this.map = map;
        this.procedures = procedures.items;
        this.stats = stats;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger la cartographie des procédures.');
      }
    });
  }

  backToList(): void {
    this.router.navigate(['/procedures']);
  }

  getProceduresForProcess(processId: number): ProcedureListItemResponse[] {
    return this.procedures.filter(p => p.processId === processId);
  }

  openProcedureDetails(procedure: ProcedureListItemResponse): void {
    this.router.navigate(['/procedures', procedure.id]);
  }
}
