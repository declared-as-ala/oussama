import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatListModule } from '@angular/material/list';
import { MatTabsModule } from '@angular/material/tabs';
import { NgApexchartsModule } from 'ng-apexcharts';
import { NotificationService } from '../../../core/services/notification.service';
import {
  DashboardAlertResponse,
  DashboardKpiResponse,
  DashboardChartDataPointResponse,
  DashboardMonthlyTrendPointResponse,
  SuperAdminDashboardResponse,
  TopOrganizationResponse
} from '../models/dashboard.models';
import {
  ORGANIZATION_STATUS_OPTIONS,
  ORGANIZATION_TYPE_OPTIONS,
  OrganizationListItemResponse
} from '../models/organization.models';
import { DashboardService } from '../services/dashboard.service';
import { OrganizationService } from '../services/organization.service';

@Component({
  selector: 'app-super-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatListModule,
    MatTabsModule,
    NgApexchartsModule
  ],
  templateUrl: './super-admin-dashboard.component.html',
  styleUrls: ['./super-admin-dashboard.component.scss']
})
export class SuperAdminDashboardComponent implements OnInit {
  readonly now = new Date();

  // Chart Options
  statusChartOptions: any;
  roleChartOptions: any;
  trendChartOptions: any;
  visitorChartOptions: any;
  deviceChartOptions: any;

  readonly chartColorPalette = ['#10b981', '#3b82f6', '#8b5cf6', '#f59e0b', '#ef4444', '#64748b'];
  readonly devicePalette = ['#22c55e', '#3b82f6', '#ef4444'];
  readonly locationPalette = ['#22c55e', '#ef4444', '#3b82f6', '#f59e0b', '#8b5cf6'];

  readonly periods = [
    { value: '1M', label: '1 mois' },
    { value: '3M', label: '3 mois' },
    { value: '6M', label: '6 mois' },
    { value: '12M', label: '12 mois' }
  ];

  readonly statusOptions = ORGANIZATION_STATUS_OPTIONS;
  readonly typeOptions = ORGANIZATION_TYPE_OPTIONS;

  readonly filterForm = this.fb.group({
    period: ['12M'],
    organizationId: [null as number | null],
    status: [''],
    type: ['']
  });

  loading = false;
  dashboard: SuperAdminDashboardResponse | null = null;
  organizations: OrganizationListItemResponse[] = [];
  visitorSeries: DashboardMonthlyTrendPointResponse[] = [];
  deviceBreakdown: Array<{ label: string; percent: number; color: string; count: number }> = [];
  locationBreakdown: Array<{ label: string; percent: number; color: string; count: number }> = [];
  devicesTotal = 0;
  locationTotal = 0;

  constructor(
    private readonly fb: FormBuilder,
    private readonly dashboardService: DashboardService,
    private readonly organizationService: OrganizationService,
    private readonly notificationService: NotificationService,
    private readonly router: Router
  ) { }

  ngOnInit(): void {
    this.loadOrganizations();
    this.loadDashboard();
  }

  refresh(): void {
    this.loadDashboard();
  }

  applyFilters(): void {
    this.loadDashboard();
  }

  resetFilters(): void {
    this.filterForm.reset({ period: '12M', organizationId: null, status: '', type: '' });
    this.loadDashboard();
  }

  goToCreateOrganization(): void {
    this.router.navigate(['/super-admin/organizations/create']);
  }

  goToOrganizations(): void {
    this.router.navigate(['/super-admin/organizations']);
  }

  goToSuspendedOrganizations(): void {
    this.router.navigate(['/super-admin/organizations'], {
      queryParams: { status: 'SUSPENDUE' }
    });
  }

  goToRequests(): void {
    this.router.navigate(['/super-admin/requests']);
  }

  getMaxValue(values: number[]): number {
    if (!values.length) {
      return 1;
    }

    const max = Math.max(...values);
    return max <= 0 ? 1 : max;
  }

  getDataPointMax(points: DashboardChartDataPointResponse[] | undefined): number {
    return this.getMaxValue((points ?? []).map(point => point.value));
  }

  getAlertClass(alert: DashboardAlertResponse): string {
    const severity = (alert.severity || '').toUpperCase();
    if (severity === 'HIGH') {
      return 'alert-high';
    }

    if (severity === 'MEDIUM') {
      return 'alert-medium';
    }

    if (severity === 'LOW') {
      return 'alert-low';
    }

    return 'alert-info';
  }

  private loadOrganizations(): void {
    this.organizationService.getOrganizations({ pageNumber: 1, pageSize: 200, sortBy: 'Name', sortDirection: 'ASC' }).subscribe({
      next: (response) => {
        this.organizations = response.items;
      },
      error: () => {
        this.notificationService.showError('Impossible de charger les organisations pour les filtres.');
      }
    });
  }

  private loadDashboard(): void {
    this.loading = true;
    const raw = this.filterForm.getRawValue();

    this.dashboardService.getSuperAdminDashboard({
      period: raw.period || '12M',
      organizationId: raw.organizationId,
      status: raw.status || null,
      type: raw.type || null
    }).subscribe({
      next: (response) => {
        this.dashboard = response;
        this.initCharts(response);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.notificationService.showError('Chargement du dashboard super admin impossible.');
      }
    });
  }

  asDataPoints(values: DashboardChartDataPointResponse[] | undefined): DashboardChartDataPointResponse[] {
    return values ?? [];
  }

  asTrendPoints(values: DashboardMonthlyTrendPointResponse[] | undefined): DashboardMonthlyTrendPointResponse[] {
    return values ?? [];
  }

  asTopOrganizations(values: TopOrganizationResponse[] | undefined): TopOrganizationResponse[] {
    return values ?? [];
  }

  getKpiCards(kpis: DashboardKpiResponse): Array<{ title: string; value: number; icon: string; tone: string; hint: string }> {
    return [
      {
        title: 'Organisations',
        value: kpis.totalOrganizations,
        icon: 'business',
        tone: 'tone-info',
        hint: 'Total plateformes actives et suspendues'
      },
      {
        title: 'Organisations actives',
        value: kpis.activeOrganizations,
        icon: 'check_circle',
        tone: 'tone-success',
        hint: 'Actuellement operationnelles'
      },
      {
        title: 'Organisations suspendues',
        value: kpis.suspendedOrganizations,
        icon: 'block',
        tone: 'tone-danger',
        hint: 'A verifier prioritairement'
      },
      {
        title: 'Utilisateurs',
        value: kpis.totalUsers,
        icon: 'groups',
        tone: 'tone-info',
        hint: 'Comptes utilisateurs globaux'
      },
      {
        title: 'Admins organisation',
        value: kpis.totalOrganizationAdmins,
        icon: 'admin_panel_settings',
        tone: 'tone-accent',
        hint: 'Admins locaux actifs'
      },
      {
        title: 'Processus',
        value: kpis.totalProcesses,
        icon: 'account_tree',
        tone: 'tone-info',
        hint: 'Volume total des processus'
      },
      {
        title: 'Documents',
        value: kpis.totalDocuments,
        icon: 'description',
        tone: 'tone-info',
        hint: 'Base documentaire globale'
      },
      {
        title: 'NC ouvertes',
        value: kpis.openNonConformities,
        icon: 'report_problem',
        tone: 'tone-warning',
        hint: 'Non-conformites non cloturees'
      },
      {
        title: 'Actions en retard',
        value: kpis.overdueCorrectiveActions,
        icon: 'event_busy',
        tone: 'tone-danger',
        hint: 'Actions correctives en depassement'
      },
      {
        title: 'Indicateurs en alerte',
        value: kpis.alertIndicators,
        icon: 'monitor_heart',
        tone: 'tone-warning',
        hint: 'KPI sous seuil'
      }
    ];
  }

  getProgressWidth(value: number, max: number): number {
    if (max <= 0) {
      return 0;
    }

    return Math.max(4, (value / max) * 100);
  }

  getOrgValue(org: TopOrganizationResponse, key: string): number {
    return (org as any)[key] || 0;
  }

  getTrendPointHeight(value: number, points: DashboardMonthlyTrendPointResponse[] | undefined): number {
    const max = this.getTrendMax(points);
    return this.getProgressWidth(value, max);
  }

  getTrendMax(points: DashboardMonthlyTrendPointResponse[] | undefined): number {
    return this.getMaxValue((points ?? []).map(point => point.value));
  }

  getSeverityIcon(alert: DashboardAlertResponse): string {
    const severity = (alert.severity || '').toUpperCase();
    if (severity === 'HIGH') {
      return 'error';
    }

    if (severity === 'MEDIUM') {
      return 'warning';
    }

    if (severity === 'LOW') {
      return 'info';
    }

    return 'notifications_active';
  }

  getSeverityLabel(alert: DashboardAlertResponse): string {
    const severity = (alert.severity || '').toUpperCase();
    if (severity === 'HIGH') {
      return 'Haute';
    }

    if (severity === 'MEDIUM') {
      return 'Moyenne';
    }

    if (severity === 'LOW') {
      return 'Basse';
    }

    return 'Info';
  }

  trackByLabel(_index: number, item: DashboardChartDataPointResponse): string {
    return item.label;
  }

  trackByPeriod(_index: number, item: DashboardMonthlyTrendPointResponse): string {
    return item.period;
  }

  trackByOrganization(_index: number, item: TopOrganizationResponse): number {
    return item.organizationId;
  }

  getVisitorYLabels(): number[] {
    if (!this.visitorSeries.length) {
      return [100, 80, 60, 40, 20, 0];
    }

    const max = this.getTrendMax(this.visitorSeries);
    const step = max > 0 ? max / 5 : 1;
    return Array.from({ length: 6 }, (_value, index) => Math.round(max - step * index));
  }

  getVisitorPath(width = 860, height = 240, padding = 16): string {
    if (!this.visitorSeries.length) {
      return '';
    }

    const values = this.visitorSeries.map(point => point.value);
    const max = this.getMaxValue(values);
    const innerWidth = width - padding * 2;
    const innerHeight = height - padding * 2;
    const denominator = Math.max(this.visitorSeries.length - 1, 1);

    return this.visitorSeries
      .map((point, index) => {
        const x = padding + (innerWidth * index) / denominator;
        const y = padding + innerHeight - (point.value / max) * innerHeight;
        return `${index === 0 ? 'M' : 'L'} ${x} ${y}`;
      })
      .join(' ');
  }

  getVisitorAreaPath(width = 860, height = 240, padding = 16): string {
    const linePath = this.getVisitorPath(width, height, padding);
    if (!linePath) {
      return '';
    }

    const innerWidth = width - padding * 2;
    const baseY = height - padding;
    return `${linePath} L ${padding + innerWidth} ${baseY} L ${padding} ${baseY} Z`;
  }

  trackByBreakdownLabel(_index: number, item: { label: string }): string {
    return item.label;
  }

  private initCharts(response: SuperAdminDashboardResponse): void {
    // 1. Organizations by Status (Donut)
    const statusData = this.asDataPoints(response.charts.organizationsByStatus);
    this.statusChartOptions = {
      series: statusData.map(d => d.value),
      labels: statusData.map(d => d.label),
      chart: { type: 'donut', height: 280, animations: { enabled: true, easing: 'easeinout', speed: 800 } },
      colors: this.chartColorPalette,
      stroke: { show: false },
      dataLabels: { enabled: false },
      legend: { position: 'bottom', labels: { useSeriesColors: true } },
      plotOptions: {
        pie: {
          donut: {
            size: '75%',
            labels: {
              show: true,
              name: { show: true, fontSize: '14px', fontWeight: 600 },
              value: { show: true, fontSize: '20px', fontWeight: 800, formatter: (val: any) => val },
              total: { show: true, label: 'Total', formatter: () => response.kpis.totalOrganizations.toString() }
            }
          }
        }
      }
    };

    // 2. Users by Role (RadialBar)
    const roleData = this.asDataPoints(response.charts.usersByRole);
    this.roleChartOptions = {
      series: roleData.map(d => Math.round((d.value / Math.max(1, response.kpis.totalUsers)) * 100)),
      labels: roleData.map(d => d.label),
      chart: { type: 'radialBar', height: 280 },
      plotOptions: {
        radialBar: {
          offsetY: 0,
          startAngle: 0,
          endAngle: 270,
          hollow: { margin: 5, size: '30%', background: 'transparent' },
          dataLabels: { name: { show: false }, value: { show: false } }
        }
      },
      colors: this.chartColorPalette,
      legend: {
        show: true,
        floating: true,
        fontSize: '12px',
        position: 'left',
        offsetX: 0,
        offsetY: 10,
        labels: { useSeriesColors: true },
        formatter: (seriesName: string, opts: any) => seriesName + ":  " + opts.w.globals.series[opts.seriesIndex] + "%",
        itemMargin: { vertical: 3 }
      }
    };

    // 3. Trends (Combined Area/Column)
    const orgTrends = this.asTrendPoints(response.charts.monthlyOrganizationsCreated);
    const userTrends = this.asTrendPoints(response.charts.monthlyUsersCreated);
    const ncTrends = this.asTrendPoints(response.charts.monthlyNonConformities);

    this.trendChartOptions = {
      series: [
        { name: 'Organisations', type: 'column', data: orgTrends.map(d => d.value) },
        { name: 'Utilisateurs', type: 'area', data: userTrends.map(d => d.value) },
        { name: 'Non-conformités', type: 'line', data: ncTrends.map(d => d.value) }
      ],
      chart: { height: 350, type: 'line', stacked: false, toolbar: { show: false } },
      stroke: { width: [0, 2, 3], curve: 'smooth' },
      plotOptions: { bar: { columnWidth: '50%' } },
      fill: {
        opacity: [0.85, 0.25, 1],
        gradient: {
          inverseColors: false,
          shade: 'light',
          type: "vertical",
          opacityFrom: 0.85,
          opacityTo: 0.55,
          stops: [0, 100, 100, 100]
        }
      },
      labels: orgTrends.map(d => d.period),
      markers: { size: 0 },
      xaxis: { type: 'category' },
      yaxis: { min: 0 },
      tooltip: { shared: true, intersect: false },
      colors: ['#10b981', '#3b82f6', '#ef4444']
    };

    // 4. Visitors (Area Spline)
    this.visitorChartOptions = {
      series: [{ name: 'Nouveaux Utilisateurs', data: userTrends.map(d => d.value) }],
      chart: { height: 280, type: 'area', toolbar: { show: false }, sparkline: { enabled: false } },
      dataLabels: { enabled: false },
      stroke: { curve: 'smooth', width: 3 },
      fill: {
        type: 'gradient',
        gradient: { shadeIntensity: 1, opacityFrom: 0.45, opacityTo: 0.05, stops: [20, 100, 100, 100] }
      },
      xaxis: { categories: userTrends.map(d => d.period), labels: { style: { colors: '#64748b' } } },
      yaxis: { labels: { style: { colors: '#64748b' } } },
      colors: ['#3b82f6'],
      grid: { borderColor: 'rgba(0,0,0,0.05)', strokeDashArray: 4 }
    };

    // 5. Device Breakdown (Donut)
    this.buildTrafficWidgets(response);
    this.deviceChartOptions = {
      series: this.deviceBreakdown.map(d => d.count),
      labels: this.deviceBreakdown.map(d => d.label),
      chart: { type: 'donut', height: 180, sparkline: { enabled: true } },
      colors: this.devicePalette,
      stroke: { show: false },
      plotOptions: { pie: { donut: { size: '70%' } } },
      tooltip: { enabled: true }
    };
  }

  private buildTrafficWidgets(response: SuperAdminDashboardResponse): void {
    this.visitorSeries = this.asTrendPoints(response.charts.monthlyUsersCreated);
    const roleCounts = this.asDataPoints(response.charts.usersByRole).map(item => item.value);
    const fallbackCounts = [Math.max(1, response.kpis.totalUsers), 5, 2];
    const rawDeviceCounts = [0, 1, 2].map(index => roleCounts[index] ?? fallbackCounts[index]);
    const devicePercents = this.toPercentages(rawDeviceCounts);

    this.deviceBreakdown = ['Desktop', 'Tablet', 'Mobile'].map((label, index) => ({
      label,
      percent: devicePercents[index],
      color: this.devicePalette[index],
      count: rawDeviceCounts[index]
    }));
    this.devicesTotal = rawDeviceCounts.reduce((t, c) => t + c, 0);

    const top = this.asTopOrganizations(response.topOrganizations).slice(0, 5);
    const locations = top.map(item => ({ label: item.organizationName, value: item.usersCount }));
    const locationValues = locations.map(item => item.value);
    const locationPercents = this.toPercentages(locationValues);

    this.locationBreakdown = locations.map((item, index) => ({
      label: item.label,
      percent: locationPercents[index],
      color: this.locationPalette[index % this.locationPalette.length],
      count: item.value
    }));
    this.locationTotal = locationValues.reduce((t, c) => t + c, 0);
  }

  private toPercentages(values: number[]): number[] {
    if (!values.length) {
      return [];
    }

    const safeValues = values.map(value => Math.max(0, value));
    const total = safeValues.reduce((sum, value) => sum + value, 0);
    if (total <= 0) {
      return safeValues.map(() => 0);
    }

    const raw = safeValues.map(value => (value / total) * 100);
    const rounded = raw.map(value => Math.round(value));
    const diff = 100 - rounded.reduce((sum, value) => sum + value, 0);
    if (diff !== 0 && rounded.length > 0) {
      let bestIndex = 0;
      for (let i = 1; i < raw.length; i += 1) {
        if (raw[i] > raw[bestIndex]) {
          bestIndex = i;
        }
      }
      rounded[bestIndex] += diff;
    }

    return rounded;
  }
}
