import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { environment } from '../../../../environments/environment';
import {
  CorrectiveActionAttachmentResponse,
  CorrectiveActionDetailsResponse,
  CorrectiveActionActionLogResponse,
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
  private readonly apiBase = `${environment.apiUrl}/api`;

  constructor(
    private readonly apiService: ApiService,
    private readonly http: HttpClient
  ) {}

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

  notifyCompletion(id: number): Observable<CorrectiveActionResponse> {
    return this.apiService.post<CorrectiveActionResponse>(`${this.endpoint}/${id}/completion-notification`, {});
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

  getCorrectiveActionHistory(id: number): Observable<CorrectiveActionActionLogResponse[]> {
    return this.apiService.get<CorrectiveActionActionLogResponse[]>(`${this.endpoint}/${id}/action-logs`);
  }

  deleteCorrectiveActionActionLog(actionId: number, logId: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${actionId}/action-logs/${logId}`);
  }

  uploadAttachment(actionId: number, file: File): Observable<CorrectiveActionAttachmentResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<CorrectiveActionAttachmentResponse>(
      `${this.apiBase}/${this.endpoint}/${actionId}/attachments`,
      formData
    );
  }

  downloadAttachment(attachmentId: number): Observable<Blob> {
    return this.http.get(`${this.apiBase}/${this.endpoint}/attachments/${attachmentId}`, {
      responseType: 'blob'
    });
  }

  deleteAttachment(attachmentId: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/attachments/${attachmentId}`);
  }
}
