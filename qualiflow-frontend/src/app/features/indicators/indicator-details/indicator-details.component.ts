import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { NgApexchartsModule } from 'ng-apexcharts';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  IndicatorChartResponse,
  IndicatorDetailsResponse,
  IndicatorStatus,
  INDICATOR_FREQUENCY_OPTIONS,
  INDICATOR_STATUS_OPTIONS,
  IndicatorActionLogResponse
} from '../models/indicator.models';
import { IndicatorService } from '../services/indicator.service';
import { IndicatorValuesComponent } from '../indicator-values/indicator-values.component';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

type IndicatorTab = 'overview' | 'chart' | 'values' | 'logs';

@Component({
  selector: 'app-indicator-details',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule,
    MatTableModule,
    IndicatorValuesComponent,
    TranslatePipe,
    NgApexchartsModule
  ],
  templateUrl: './indicator-details.component.html',
  styleUrls: ['./indicator-details.component.scss']
})
export class IndicatorDetailsComponent implements OnInit {
  readonly statusOptions = INDICATOR_STATUS_OPTIONS;
  readonly frequencyOptions = INDICATOR_FREQUENCY_OPTIONS;

  loading = false;
  indicatorId!: number;
  details: IndicatorDetailsResponse | null = null;
  chart: IndicatorChartResponse | null = null;
  activeTab: IndicatorTab = 'overview';
  actionLogs: IndicatorActionLogResponse[] = [];
  filteredActionLogs: IndicatorActionLogResponse[] = [];
  selectedCategory: string = 'ALL';
  searchText: string = '';
  displayedActionLogColumns: string[] = ['action', 'user', 'date', 'comment', 'actions'];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly dialog: MatDialog,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly indicatorService: IndicatorService
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const parsedId = idParam ? Number(idParam) : Number.NaN;

    if (Number.isNaN(parsedId)) {
      this.notificationService.showError('Identifiant indicateur invalide.');
      this.router.navigate(['/indicators']);
      return;
    }

    this.indicatorId = parsedId;
    const tabParam = (this.route.snapshot.queryParamMap.get('tab') || '').toLowerCase();
    if (tabParam === 'chart' || tabParam === 'values' || tabParam === 'logs') {
      this.activeTab = tabParam as IndicatorTab;
    }

    this.loadData();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
  }

  get isResponsible(): boolean {
    const userId = this.authService.getCurrentUserId();
    if (!userId || !this.details) {
      return false;
    }
    return userId === this.details.responsible.id;
  }

  get statusLabel(): string {
    if (!this.details) {
      return '';
    }

    return this.statusOptions.find(option => option.value === this.details!.indicator.status)?.label ?? this.details.indicator.status;
  }

  get frequencyLabel(): string {
    if (!this.details) {
      return '';
    }

    return this.frequencyOptions.find(option => option.value === this.details!.indicator.measurementFrequency)?.label
      ?? this.details.indicator.measurementFrequency;
  }

  chartOptions: any = null;

  goBack(): void {
    this.router.navigate(['/indicators']);
  }

  edit(): void {
    this.router.navigate(['/indicators', this.indicatorId, 'edit']);
  }

  delete(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Supprimer indicateur',
        message: 'Confirmer la suppression de cet indicateur ?',
        confirmText: 'Supprimer',
        cancelText: 'Annuler'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }

      this.indicatorService.deleteIndicator(this.indicatorId).subscribe({
        next: () => {
          this.notificationService.showSuccess('Indicateur supprime.');
          this.router.navigate(['/indicators']);
        },
        error: () => this.notificationService.showError('Suppression impossible.')
      });
    });
  }

  toggleStatus(): void {
    this.indicatorService.toggleIndicatorStatus(this.indicatorId).subscribe({
      next: () => {
        this.notificationService.showSuccess('Statut indicateur mis a jour.');
        this.loadData();
      },
      error: () => this.notificationService.showError('Mise a jour du statut impossible.')
    });
  }

  selectTab(tab: IndicatorTab): void {
    this.activeTab = tab;
  }

  onValuesChanged(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading = true;

    forkJoin({
      details: this.indicatorService.getIndicatorById(this.indicatorId),
      chart: this.indicatorService.getIndicatorChart(this.indicatorId)
    }).subscribe({
      next: ({ details, chart }) => {
        this.details = details;
        this.chart = chart;
        this.updateChartOptions();
        
        if (this.canWrite || this.isResponsible) {
          this.loadActionLogs();
        } else {
          this.loading = false;
        }
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger cet indicateur.');
        this.router.navigate(['/indicators']);
      }
    });
  }

  loadActionLogs(): void {
    this.indicatorService.getIndicatorActionLogs(this.indicatorId).subscribe({
      next: (logs) => {
        this.actionLogs = logs || [];
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError("Impossible de charger le journal d'actions.");
      }
    });
  }

  applyFilters(): void {
    let filtered = [...this.actionLogs];

    if (this.selectedCategory !== 'ALL') {
      filtered = filtered.filter(log => {
        const type = log.actionType.toUpperCase();
        if (this.selectedCategory === 'PILOTAGE') {
          return type.includes('RESPONSIBLE') || type.includes('PILOT');
        }
        if (this.selectedCategory === 'MODIFICATION') {
          return type.includes('UPDATED') || type.includes('VALUE');
        }
        if (this.selectedCategory === 'CREATION') {
          return type.includes('CREATED');
        }
        if (this.selectedCategory === 'STATUS') {
          return type.includes('STATUS') || type.includes('TOGGLED');
        }
        return true;
      });
    }

    if (this.searchText.trim()) {
      const search = this.searchText.toLowerCase().trim();
      filtered = filtered.filter(log => 
        (log.comment && log.comment.toLowerCase().includes(search)) ||
        (log.performedByFullName && log.performedByFullName.toLowerCase().includes(search)) ||
        log.actionType.toLowerCase().includes(search)
      );
    }

    this.filteredActionLogs = filtered;
  }

  getCategoryCount(category: string): number {
    if (category === 'ALL') {
      return this.actionLogs.length;
    }
    return this.actionLogs.filter(log => {
      const type = log.actionType.toUpperCase();
      if (category === 'PILOTAGE') {
        return type.includes('RESPONSIBLE') || type.includes('PILOT');
      }
      if (category === 'MODIFICATION') {
        return type.includes('UPDATED') || type.includes('VALUE');
      }
      if (category === 'CREATION') {
        return type.includes('CREATED');
      }
      if (category === 'STATUS') {
        return type.includes('STATUS') || type.includes('TOGGLED');
      }
      return true;
    }).length;
  }

  getActionIcon(actionType: string): string {
    const type = actionType.toUpperCase();
    if (type.includes('CREATED')) return 'add_circle';
    if (type.includes('UPDATED')) return 'edit';
    if (type.includes('TOGGLED') || type.includes('STATUS')) return 'published_with_changes';
    if (type.includes('ADDED') || type.includes('VALUE_ADDED')) return 'playlist_add';
    if (type.includes('DELETED')) return 'delete_forever';
    return 'info';
  }

  getActionLabel(actionType: string): string {
    const type = actionType.toUpperCase();
    if (type === 'INDICATOR_CREATED') return 'Création Indicateur';
    if (type === 'INDICATOR_UPDATED') return 'Configuration Modifiée';
    if (type === 'STATUS_TOGGLED') return 'Statut Modifié';
    if (type === 'VALUE_ADDED') return 'Valeur Enregistrée';
    if (type === 'VALUE_UPDATED') return 'Valeur Modifiée';
    if (type === 'VALUE_DELETED') return 'Valeur Supprimée';
    return actionType;
  }

  selectLog(log: any): void {
    const detailsList = [
      `Action : ${this.getActionLabel(log.actionType)}`,
      `Effectuée par : ${log.performedByFullName || 'Système'}`,
      `Le : ${new Date(log.performedAt).toLocaleString()}`,
      `Commentaire : ${log.comment || 'Aucun commentaire'}`
    ];
    if (log.oldValue) detailsList.push(`Ancienne valeur : ${log.oldValue}`);
    if (log.newValue) detailsList.push(`Nouvelle valeur : ${log.newValue}`);

    this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: "Détails de l'action",
        message: detailsList.join('\n'),
        confirmText: 'Fermer',
        cancelText: ''
      }
    });
  }

  private updateChartOptions(): void {
    if (!this.chart) {
      this.chartOptions = null;
      return;
    }

    const isAlert = this.details?.indicator.isInAlert;
    const seriesData = (this.chart.values || []).map(v => Number(v));

    this.chartOptions = {
      series: [{ name: 'Mesure', data: seriesData }],
      chart: {
        type: 'area',
        height: 350,
        toolbar: { show: false },
        zoom: { enabled: false },
        fontFamily: 'inherit',
        animations: { enabled: true, easing: 'easeinout', speed: 800 }
      },
      dataLabels: { enabled: false },
      stroke: { curve: 'smooth', width: 3, colors: [isAlert ? '#ef4444' : '#22c55e'] },
      fill: {
        type: 'gradient',
        gradient: {
          shadeIntensity: 1,
          opacityFrom: 0.45, opacityTo: 0.05, stops: [20, 100],
          colorStops: [
            { offset: 0, color: isAlert ? '#ef4444' : '#22c55e', opacity: 0.4 },
            { offset: 100, color: isAlert ? '#ef4444' : '#22c55e', opacity: 0 }
          ]
        }
      },
      markers: {
        size: 5,
        colors: [isAlert ? '#ef4444' : '#22c55e'],
        strokeColors: '#fff', strokeWidth: 2,
        hover: { size: 7 }
      },
      xaxis: {
        categories: this.chart.labels,
        axisBorder: { show: false },
        axisTicks: { show: false },
        labels: { style: { colors: '#94a3b8', fontSize: '12px' } }
      },
      yaxis: {
        labels: { style: { colors: '#94a3b8', fontSize: '12px' } }
      },
      grid: {
        borderColor: 'rgba(0,0,0,0.05)',
        strokeDashArray: 4,
        padding: { top: 0, right: 0, bottom: 0, left: 10 }
      },
      annotations: {
        yaxis: [
          {
            y: this.chart.targetValue,
            borderColor: '#3b82f6',
            label: {
              borderColor: '#3b82f6',
              style: { color: '#fff', background: '#3b82f6' },
              text: 'Cible: ' + this.chart.targetValue
            }
          },
          {
            y: this.chart.thresholdValue,
            borderColor: '#f59e0b',
            borderDashArray: 5,
            label: {
              borderColor: '#f59e0b',
              style: { color: '#fff', background: '#f59e0b' },
              text: 'Alerte: ' + this.chart.thresholdValue
            }
          }
        ]
      },
      tooltip: { theme: 'light', x: { show: true } }
    };
  }


  getStatusLabel(status: IndicatorStatus): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? status;
  }
}
