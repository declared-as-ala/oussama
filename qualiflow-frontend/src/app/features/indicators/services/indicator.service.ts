import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  CreateIndicatorRequest,
  CreateIndicatorValueRequest,
  GetIndicatorsQueryRequest,
  IndicatorAlertResponse,
  IndicatorChartResponse,
  IndicatorDetailsResponse,
  IndicatorListItemResponse,
  IndicatorResponse,
  IndicatorStatisticsResponse,
  IndicatorValueResponse,
  PagedIndicatorResponse,
  UpdateIndicatorRequest,
  UpdateIndicatorValueRequest
} from '../models/indicator.models';

@Injectable({
  providedIn: 'root'
})
export class IndicatorService {
  private readonly endpoint = 'indicators';

  constructor(private readonly apiService: ApiService) {}

  getIndicators(params: GetIndicatorsQueryRequest = {}): Observable<PagedIndicatorResponse> {
    return this.apiService.get<PagedIndicatorResponse>(this.endpoint, params);
  }

  getIndicatorById(id: number): Observable<IndicatorDetailsResponse> {
    return this.apiService.get<IndicatorDetailsResponse>(`${this.endpoint}/${id}`);
  }

  createIndicator(payload: CreateIndicatorRequest): Observable<IndicatorResponse> {
    return this.apiService.post<IndicatorResponse>(this.endpoint, payload);
  }

  updateIndicator(id: number, payload: UpdateIndicatorRequest): Observable<IndicatorResponse> {
    return this.apiService.put<IndicatorResponse>(`${this.endpoint}/${id}`, payload);
  }

  deleteIndicator(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  toggleIndicatorStatus(id: number): Observable<IndicatorResponse> {
    return this.apiService.patch<IndicatorResponse>(`${this.endpoint}/${id}/toggle-status`, {});
  }

  getIndicatorStatistics(): Observable<IndicatorStatisticsResponse> {
    return this.apiService.get<IndicatorStatisticsResponse>(`${this.endpoint}/statistics`);
  }

  getIndicatorsByProcess(processId: number): Observable<IndicatorListItemResponse[]> {
    return this.apiService.get<IndicatorListItemResponse[]>(`${this.endpoint}/by-process/${processId}`);
  }

  getIndicatorChart(id: number): Observable<IndicatorChartResponse> {
    return this.apiService.get<IndicatorChartResponse>(`${this.endpoint}/${id}/chart`);
  }

  getIndicatorAlerts(): Observable<IndicatorAlertResponse[]> {
    return this.apiService.get<IndicatorAlertResponse[]>(`${this.endpoint}/alerts`);
  }

  getValues(indicatorId: number): Observable<IndicatorValueResponse[]> {
    return this.apiService.get<IndicatorValueResponse[]>(`${this.endpoint}/${indicatorId}/values`);
  }

  createValue(indicatorId: number, payload: CreateIndicatorValueRequest): Observable<IndicatorValueResponse> {
    return this.apiService.post<IndicatorValueResponse>(`${this.endpoint}/${indicatorId}/values`, payload);
  }

  updateValue(indicatorId: number, valueId: number, payload: UpdateIndicatorValueRequest): Observable<IndicatorValueResponse> {
    return this.apiService.put<IndicatorValueResponse>(`${this.endpoint}/${indicatorId}/values/${valueId}`, payload);
  }

  deleteValue(indicatorId: number, valueId: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${indicatorId}/values/${valueId}`);
  }
}
