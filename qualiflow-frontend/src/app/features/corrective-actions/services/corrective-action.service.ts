import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  CorrectiveActionDetailsResponse,
  CorrectiveActionHistoryResponse,
  CorrectiveActionListItemResponse,
  CorrectiveActionResponse,
  CorrectiveActionStatisticsResponse,
  CreateCorrectiveActionRequest,
  GetCorrectiveActionsQueryRequest,
  PagedCorrectiveActionResponse,
  UpdateCorrectiveActionRequest,
  UpdateCorrectiveActionStatusRequest,
  VerifyCorrectiveActionEffectivenessRequest
} from '../models/corrective-action.models';

@Injectable({
  providedIn: 'root'
})
export class CorrectiveActionService {
  private readonly endpoint = 'corrective-actions';

  constructor(private readonly apiService: ApiService) {}

  getCorrectiveActions(params: GetCorrectiveActionsQueryRequest = {}): Observable<PagedCorrectiveActionResponse> {
    return this.apiService.get<PagedCorrectiveActionResponse>(this.endpoint, params);
  }

  getCorrectiveActionById(id: number): Observable<CorrectiveActionDetailsResponse> {
    return this.apiService.get<CorrectiveActionDetailsResponse>(`${this.endpoint}/${id}`);
  }

  createCorrectiveAction(payload: CreateCorrectiveActionRequest): Observable<CorrectiveActionResponse> {
    return this.apiService.post<CorrectiveActionResponse>(this.endpoint, payload);
  }

  updateCorrectiveAction(id: number, payload: UpdateCorrectiveActionRequest): Observable<CorrectiveActionResponse> {
    return this.apiService.put<CorrectiveActionResponse>(`${this.endpoint}/${id}`, payload);
  }

  deleteCorrectiveAction(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  updateCorrectiveActionStatus(id: number, payload: UpdateCorrectiveActionStatusRequest): Observable<CorrectiveActionResponse> {
    return this.apiService.patch<CorrectiveActionResponse>(`${this.endpoint}/${id}/status`, payload);
  }

  verifyEffectiveness(id: number, payload: VerifyCorrectiveActionEffectivenessRequest): Observable<CorrectiveActionResponse> {
    return this.apiService.patch<CorrectiveActionResponse>(`${this.endpoint}/${id}/verify-effectiveness`, payload);
  }

  getCorrectiveActionStatistics(): Observable<CorrectiveActionStatisticsResponse> {
    return this.apiService.get<CorrectiveActionStatisticsResponse>(`${this.endpoint}/statistics`);
  }

  getCorrectiveActionsByNonConformity(nonConformityId: number): Observable<CorrectiveActionListItemResponse[]> {
    return this.apiService.get<CorrectiveActionListItemResponse[]>(`${this.endpoint}/by-nonconformity/${nonConformityId}`);
  }

  getCorrectiveActionHistory(id: number): Observable<CorrectiveActionHistoryResponse[]> {
    return this.apiService.get<CorrectiveActionHistoryResponse[]>(`${this.endpoint}/${id}/history`);
  }
}
