import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { catchError, forkJoin, of } from 'rxjs';
import { NgApexchartsModule } from 'ng-apexcharts';
import {
  IndicatorAlertResponse,
  IndicatorChartResponse,
  IndicatorListItemResponse,
  IndicatorStatisticsResponse
} from '../models/indicator.models';
import { IndicatorService } from '../services/indicator.service';

interface IndicatorTrendViewModel {
  indicator: IndicatorListItemResponse;
  chart: IndicatorChartResponse;
  chartOptions: any;
}

@Component({
  selector: 'app-indicator-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    NgApexchartsModule
  ],
  templateUrl: './indicator-dashboard.component.html',
  styleUrls: ['./indicator-dashboard.component.scss']
})
export class IndicatorDashboardComponent implements OnInit {
  loading = false;
  statistics: IndicatorStatisticsResponse | null = null;
  alerts: IndicatorAlertResponse[] = [];
  trends: IndicatorTrendViewModel[] = [];

  constructor(
    private readonly router: Router,
    private readonly indicatorService: IndicatorService
  ) { }

  ngOnInit(): void {
    this.loadDashboard();
  }

  goToList(): void {
    this.router.navigate(['/indicators']);
  }

  openIndicator(indicatorId: number): void {
    this.router.navigate(['/indicators', indicatorId]);
  }

  private getSparklineOptions(values: number[] | undefined, isAlert: boolean): any {
    const data = (values || []).map(v => Number(v));
    return {
      series: [{
        name: 'Valeur',
        data: data
      }],
      chart: {
        type: 'area',
        height: 40,
        sparkline: {
          enabled: true
        },
        animations: {
          enabled: true,
          easing: 'easeinout',
          speed: 800
        }
      },
      stroke: {
        curve: 'smooth',
        width: 2,
        colors: [isAlert ? '#ef4444' : '#22c55e']
      },
      fill: {
        type: 'gradient',
        gradient: {
          shadeIntensity: 1,
          opacityFrom: 0.45,
          opacityTo: 0.05,
          stops: [20, 100],
          colorStops: [
            {
              offset: 0,
              color: isAlert ? '#ef4444' : '#22c55e',
              opacity: 0.4
            },
            {
              offset: 100,
              color: isAlert ? '#ef4444' : '#22c55e',
              opacity: 0
            }
          ]
        }
      },
      tooltip: {
        enabled: false
      }
    };
  }

  private loadDashboard(): void {
    this.loading = true;

    forkJoin({
      stats: this.indicatorService.getIndicatorStatistics().pipe(catchError(() => of(null))),
      alerts: this.indicatorService.getIndicatorAlerts().pipe(catchError(() => of([] as IndicatorAlertResponse[]))),
      inAlertPage: this.indicatorService
        .getIndicators({ pageNumber: 1, pageSize: 8, isInAlert: true })
        .pipe(catchError(() => of({ total: 0, pageNumber: 1, pageSize: 8, items: [] as IndicatorListItemResponse[] }))),
      recentPage: this.indicatorService
        .getIndicators({ pageNumber: 1, pageSize: 8 })
        .pipe(catchError(() => of({ total: 0, pageNumber: 1, pageSize: 8, items: [] as IndicatorListItemResponse[] })))
    }).subscribe({
      next: ({ stats, alerts, inAlertPage, recentPage }) => {
        this.statistics = stats;
        this.alerts = alerts;

        const selected = [...inAlertPage.items];
        for (const indicator of recentPage.items) {
          if (selected.length >= 3) {
            break;
          }

          if (!selected.some(item => item.id === indicator.id)) {
            selected.push(indicator);
          }
        }

        if (selected.length === 0) {
          this.trends = [];
          this.loading = false;
          return;
        }

        forkJoin(
          selected.map(indicator =>
            this.indicatorService.getIndicatorChart(indicator.id).pipe(
              catchError(() =>
                of({
                  labels: [],
                  values: [],
                  targetValue: indicator.targetValue,
                  thresholdValue: indicator.alertThreshold
                } as IndicatorChartResponse)
              )
            )
          )
        ).subscribe({
          next: charts => {
            this.trends = selected.map((indicator, index) => ({
              indicator,
              chart: charts[index],
              chartOptions: this.getSparklineOptions(charts[index].values, indicator.isInAlert)
            }));
            this.loading = false;
          },
          error: () => {
            this.trends = [];
            this.loading = false;
          }
        });
      },
      error: () => {
        this.loading = false;
      }
    });
  }
}
