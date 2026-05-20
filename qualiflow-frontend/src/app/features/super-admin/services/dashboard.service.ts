import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  DashboardAlertResponse,
  DashboardChartResponse,
  DashboardKpiResponse,
  DashboardQueryParams,
  DashboardRecentActivityResponse,
  SuperAdminDashboardResponse,
  TopOrganizationResponse
} from '../models/dashboard.models';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly baseEndpoint = 'dashboard/super-admin';

  constructor(private readonly apiService: ApiService) {}

  getSuperAdminDashboard(params: DashboardQueryParams = {}): Observable<SuperAdminDashboardResponse> {
    return this.apiService.get<SuperAdminDashboardResponse>(this.baseEndpoint, params);
  }

  getDashboardKpis(params: DashboardQueryParams = {}): Observable<DashboardKpiResponse> {
    return this.apiService.get<DashboardKpiResponse>(`${this.baseEndpoint}/kpis`, params);
  }

  getDashboardCharts(params: DashboardQueryParams = {}): Observable<DashboardChartResponse> {
    return this.apiService.get<DashboardChartResponse>(`${this.baseEndpoint}/charts`, params);
  }

  getDashboardAlerts(params: DashboardQueryParams = {}): Observable<DashboardAlertResponse[]> {
    return this.apiService.get<DashboardAlertResponse[]>(`${this.baseEndpoint}/alerts`, params);
  }

  getRecentActivities(params: DashboardQueryParams = {}): Observable<DashboardRecentActivityResponse[]> {
    return this.apiService.get<DashboardRecentActivityResponse[]>(`${this.baseEndpoint}/recent-activities`, params);
  }

  getTopOrganizations(params: DashboardQueryParams = {}): Observable<TopOrganizationResponse[]> {
    return this.apiService.get<TopOrganizationResponse[]>(`${this.baseEndpoint}/top-organizations`, params);
  }
}
