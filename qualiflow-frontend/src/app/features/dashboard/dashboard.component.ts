import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { DocumentStatisticsResponse } from '../documents/models/document.models';
import { DocumentService } from '../documents/services/document.service';
import {
  NonConformityListItemResponse,
  NonConformityStatisticsResponse
} from '../non-conformities/models/nonconformity.models';
import { NonConformityService } from '../non-conformities/services/nonconformity.service';
import {
  ProcessListItemResponse,
  ProcessStatisticsResponse
} from '../processes/models/process.models';
import { ProcessService } from '../processes/services/process.service';
import {
  ProcedureListItemResponse,
  ProcedureStatisticsResponse
} from '../procedures/models/procedure.models';
import { ProcedureService } from '../procedures/services/procedure.service';
import { NgApexchartsModule } from 'ng-apexcharts';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NgApexchartsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatTooltipModule
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  loading = true;

  // Chart Options
  qualityStatsChart: any;
  processTypeChart: any;
  ncSeverityChart: any;

  canReadProcesses = false;
  canReadProcedures = false;
  canReadNonConformities = false;
  canReadDocuments = false;
  canWriteQuality = false;

  processes: ProcessListItemResponse[] = [];
  procedures: ProcedureListItemResponse[] = [];
  nonConformities: NonConformityListItemResponse[] = [];

  processStats: ProcessStatisticsResponse | null = null;
  procedureStats: ProcedureStatisticsResponse | null = null;
  nonConformityStats: NonConformityStatisticsResponse | null = null;
  documentStats: DocumentStatisticsResponse | null = null;

  constructor(
    private readonly authService: AuthService,
    private readonly processService: ProcessService,
    private readonly procedureService: ProcedureService,
    private readonly nonConformityService: NonConformityService,
    private readonly documentService: DocumentService
  ) {}

  ngOnInit(): void {
    this.resolvePermissions();
    this.loadDashboardData();
  }

  reload(): void {
    this.loadDashboardData();
  }

  private resolvePermissions(): void {
    this.canReadProcesses = this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'CHEF_SERVICE',
      'UTILISATEUR'
    ]);
    this.canReadProcedures = this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'CHEF_SERVICE',
      'UTILISATEUR'
    ]);
    this.canReadNonConformities = this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'CHEF_SERVICE',
      'UTILISATEUR'
    ]);
    this.canReadDocuments = this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE',
      'CHEF_SERVICE',
      'UTILISATEUR'
    ]);
    this.canWriteQuality = this.authService.hasRole([
      'ADMIN_ORG',
      'RESPONSABLE_QUALITE'
    ]);
  }

  private loadDashboardData(): void {
    this.loading = true;

    const emptyProcessPage = { total: 0, pageNumber: 1, pageSize: 5, items: [] as ProcessListItemResponse[] };
    const emptyProcedurePage = { total: 0, pageNumber: 1, pageSize: 5, items: [] as ProcedureListItemResponse[] };
    const emptyNonConformityPage = { total: 0, pageNumber: 1, pageSize: 5, items: [] as NonConformityListItemResponse[] };

    const processPage$ = this.canReadProcesses
      ? this.processService.getProcesses({ pageNumber: 1, pageSize: 5 }).pipe(catchError(() => of(emptyProcessPage)))
      : of(emptyProcessPage);

    const processStats$ = this.canReadProcesses
      ? this.processService.getProcessStatistics().pipe(catchError(() => of(null)))
      : of(null);

    const procedurePage$ = this.canReadProcedures
      ? this.procedureService.getProcedures({ pageNumber: 1, pageSize: 5 }).pipe(catchError(() => of(emptyProcedurePage)))
      : of(emptyProcedurePage);

    const procedureStats$ = this.canReadProcedures
      ? this.procedureService.getProcedureStatistics().pipe(catchError(() => of(null)))
      : of(null);

    const nonConformityPage$ = this.canReadNonConformities
      ? this.nonConformityService
          .getNonConformities({ pageNumber: 1, pageSize: 5, status: 'OUVERTE' })
          .pipe(catchError(() => of(emptyNonConformityPage)))
      : of(emptyNonConformityPage);

    const nonConformityStats$ = this.canReadNonConformities
      ? this.nonConformityService.getStatistics().pipe(catchError(() => of(null)))
      : of(null);

    const documentStats$ = this.canReadDocuments
      ? this.documentService.getDocumentStatistics().pipe(catchError(() => of(null)))
      : of(null);

    forkJoin({
      processPage: processPage$,
      processStats: processStats$,
      procedurePage: procedurePage$,
      procedureStats: procedureStats$,
      nonConformityPage: nonConformityPage$,
      nonConformityStats: nonConformityStats$,
      documentStats: documentStats$
    }).subscribe({
      next: (result) => {
        this.processes = result.processPage.items;
        this.procedures = result.procedurePage.items;
        this.nonConformities = result.nonConformityPage.items;
        this.processStats = result.processStats;
        this.procedureStats = result.procedureStats;
        this.nonConformityStats = result.nonConformityStats;
        this.documentStats = result.documentStats;
        this.initCharts();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  private initCharts(): void {
    const palette = ['#10b981', '#3b82f6', '#f59e0b', '#ef4444', '#8b5cf6'];

    // 1. Quality Overview (Bar)
    this.qualityStatsChart = {
      series: [{
        name: 'Quantité',
        data: [
          this.processStats?.total ?? 0,
          this.procedureStats?.total ?? 0,
          this.nonConformityStats?.opened ?? 0,
          this.documentStats?.total ?? 0
        ]
      }],
      chart: { type: 'bar', height: 250, toolbar: { show: false } },
      plotOptions: { bar: { borderRadius: 8, distributed: true, columnWidth: '50%' } },
      colors: palette,
      xaxis: { categories: ['Processus', 'Procédures', 'NC Ouvertes', 'Documents'] },
      legend: { show: false },
      dataLabels: { enabled: false }
    };

    // 2. Non-Conformity Severity (Donut)
    this.ncSeverityChart = {
      series: [
        this.nonConformityStats?.critical ?? 0,
        this.nonConformityStats?.bySeverity?.['MAJEURE'] ?? 0,
        this.nonConformityStats?.bySeverity?.['MINEURE'] ?? 0
      ],
      labels: ['Critique', 'Majeure', 'Mineure'],
      chart: { type: 'donut', height: 250 },
      colors: ['#ef4444', '#f59e0b', '#64748b'],
      stroke: { show: false },
      legend: { position: 'bottom' },
      plotOptions: { pie: { donut: { size: '75%' } } }
    };

    // 3. Process Type Breakdown
    this.processTypeChart = {
      series: [
        this.processStats?.active ?? 0,
        (this.processStats?.total ?? 0) - (this.processStats?.active ?? 0)
      ],
      labels: ['Actifs', 'Inactifs'],
      chart: { type: 'pie', height: 250 },
      colors: ['#10b981', '#94a3b8'],
      stroke: { show: false },
      legend: { position: 'bottom' }
    };
  }

  getProcessTypeLabel(type: string): string {
    switch (type) {
      case 'PILOTAGE': return 'Pilotage';
      case 'REALISATION': return 'Realisation';
      case 'SUPPORT': return 'Support';
      default: return type;
    }
  }

  getProcessStatusLabel(status: string): string {
    return status === 'ACTIF' ? 'Actif' : 'Inactif';
  }

  getProcessStatusClass(status: string): string {
    return status === 'ACTIF' ? 'status-ok' : 'status-muted';
  }

  getProcedureStatusLabel(status: string): string {
    return status === 'ACTIF' ? 'Active' : 'Inactive';
  }

  getProcedureStatusClass(status: string): string {
    return status === 'ACTIF' ? 'status-ok' : 'status-muted';
  }

  getNonConformityStatusLabel(status: string): string {
    switch (status) {
      case 'OUVERTE': return 'Ouverte';
      case 'EN_COURS': return 'En cours';
      case 'CLOTUREE': return 'Cloturee';
      default: return status;
    }
  }

  getNonConformityStatusClass(status: string): string {
    switch (status) {
      case 'CLOTUREE': return 'status-ok';
      case 'EN_COURS': return 'status-warning';
      default: return 'status-danger';
    }
  }

  getSeverityLabel(severity: string): string {
    switch (severity) {
      case 'MINEURE': return 'Mineure';
      case 'MAJEURE': return 'Majeure';
      case 'CRITIQUE': return 'Critique';
      default: return severity;
    }
  }

  getSeverityClass(severity: string): string {
    switch (severity) {
      case 'CRITIQUE': return 'status-danger';
      case 'MAJEURE': return 'status-warning';
      default: return 'status-muted';
    }
  }

  formatDate(value?: string | null): string {
    if (!value) return '-';
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return '-';
    return parsed.toLocaleDateString('fr-FR');
  }

  trackById(_index: number, item: { id: number }): number {
    return item.id;
  }
}
