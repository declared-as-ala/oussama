import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
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
  INDICATOR_STATUS_OPTIONS
} from '../models/indicator.models';
import { IndicatorService } from '../services/indicator.service';
import { IndicatorValuesComponent } from '../indicator-values/indicator-values.component';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

type IndicatorTab = 'overview' | 'chart' | 'values';

@Component({
  selector: 'app-indicator-details',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule,
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
    if (tabParam === 'chart' || tabParam === 'values') {
      this.activeTab = tabParam;
    }

    this.loadData();
  }

  get canWrite(): boolean {
    return this.authService.hasRole(['ADMIN_ORG', 'RESPONSABLE_QUALITE']);
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
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Impossible de charger cet indicateur.');
        this.router.navigate(['/indicators']);
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
