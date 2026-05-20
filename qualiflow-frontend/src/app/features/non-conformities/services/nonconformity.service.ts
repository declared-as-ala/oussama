import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { Observable, map } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  CreateCorrectiveActionRequest,
  NonConformityAttachmentResponse,
  CreateNonConformityRequest,
  CorrectiveActionResponse,
  NonConformityDetailsResponse,
  NonConformityQueryParams,
  NonConformityResponse,
  NonConformityStatisticsResponse,
  PagedNonConformityResponse,
  UpdateCorrectiveActionRequest,
  UpdateNonConformityRequest,
  UpdateNonConformityStatusRequest,
  ValidateNonConformityRequest
} from '../models/nonconformity.models';

@Injectable({
  providedIn: 'root'
})
export class NonConformityService {
  private readonly endpoint = 'nonconformities';

  private readonly apiBase = `${environment.apiUrl}/api`;

  constructor(
    private readonly apiService: ApiService,
    private readonly http: HttpClient
  ) {}

  getNonConformities(params: NonConformityQueryParams = {}): Observable<PagedNonConformityResponse> {
    return this.apiService.get<PagedNonConformityResponse>(this.endpoint, params);
  }

  getAwaitingValidation(params: NonConformityQueryParams = {}): Observable<PagedNonConformityResponse> {
    return this.apiService.get<PagedNonConformityResponse>(`${this.endpoint}/awaiting-validation`, params);
  }

  getNonConformityById(id: number): Observable<NonConformityDetailsResponse> {
    return this.apiService.get<NonConformityDetailsResponse>(`${this.endpoint}/${id}`);
  }

  createNonConformity(payload: CreateNonConformityRequest): Observable<NonConformityResponse> {
    return this.apiService.post<NonConformityResponse>(this.endpoint, payload);
  }

  updateNonConformity(id: number, payload: UpdateNonConformityRequest): Observable<NonConformityResponse> {
    return this.apiService.put<NonConformityResponse>(`${this.endpoint}/${id}`, payload);
  }

  deleteNonConformity(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  updateNonConformityStatus(id: number, payload: UpdateNonConformityStatusRequest): Observable<NonConformityResponse> {
    return this.apiService.patch<NonConformityResponse>(`${this.endpoint}/${id}/status`, payload);
  }

  validateNonConformity(id: number, payload: ValidateNonConformityRequest): Observable<NonConformityResponse> {
    return this.apiService.patch<NonConformityResponse>(`${this.endpoint}/${id}/validate`, payload);
  }

  getStatistics(): Observable<NonConformityStatisticsResponse> {
    return this.apiService.get<NonConformityStatisticsResponse>(`${this.endpoint}/statistics`);
  }

  getActions(nonConformityId: number): Observable<CorrectiveActionResponse[]> {
    return this.apiService
      .get<any[]>(`corrective-actions/by-nonconformity/${nonConformityId}`)
      .pipe(map(items => items.map(item => this.mapActionResponse(item, nonConformityId))));
  }

  createAction(nonConformityId: number, payload: CreateCorrectiveActionRequest): Observable<CorrectiveActionResponse> {
    return this.apiService
      .post<any>('corrective-actions', {
        nonConformityId,
        type: 'CORRECTIVE',
        title: payload.title,
        description: payload.description,
        responsibleUserId: payload.responsibleUserId,
        dueDate: payload.dueDate,
        status: this.mapLegacyStatusToNew(payload.status),
        proofRecordId: null
      })
      .pipe(map(item => this.mapActionResponse(item, nonConformityId)));
  }

  updateAction(nonConformityId: number, actionId: number, payload: UpdateCorrectiveActionRequest): Observable<CorrectiveActionResponse> {
    return this.apiService
      .put<any>(`corrective-actions/${actionId}`, {
        nonConformityId,
        type: 'CORRECTIVE',
        title: payload.title,
        description: payload.description,
        responsibleUserId: payload.responsibleUserId,
        dueDate: payload.dueDate,
        status: this.mapLegacyStatusToNew(payload.status),
        proofRecordId: null,
        completionDate: payload.completionDate ?? null
      })
      .pipe(map(item => this.mapActionResponse(item, nonConformityId)));
  }

  deleteAction(nonConformityId: number, actionId: number): Observable<void> {
    return this.apiService.delete<void>(`corrective-actions/${actionId}`);
  }

  private mapActionResponse(item: any, fallbackNonConformityId: number): CorrectiveActionResponse {
    return {
      id: item.id,
      organizationId: item.organizationId ?? 0,
      nonConformityId: item.nonConformityId ?? fallbackNonConformityId,
      title: item.title,
      description: item.description ?? null,
      responsibleUserId: item.responsibleUserId,
      responsibleFullName: item.responsibleFullName ?? null,
      dueDate: item.dueDate,
      completionDate: item.completionDate ?? null,
      status: this.mapNewStatusToLegacy(item.status),
      isOverdue: !!item.isOverdue,
      createdAt: item.createdAt,
      updatedAt: item.updatedAt ?? null
    };
  }

  private mapLegacyStatusToNew(status: CorrectiveActionResponse['status']): string {
    switch (status) {
      case 'A_FAIRE':
        return 'PLANIFIEE';
      case 'TERMINEE':
        return 'REALISEE';
      case 'EN_RETARD':
        return 'EN_COURS';
      case 'EN_COURS':
      default:
        return 'EN_COURS';
    }
  }

  uploadAttachment(nonConformityId: number, file: File): Observable<NonConformityAttachmentResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<NonConformityAttachmentResponse>(
      `${this.apiBase}/${this.endpoint}/${nonConformityId}/attachments`,
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

  private mapNewStatusToLegacy(status: string): CorrectiveActionResponse['status'] {
    switch ((status || '').toUpperCase()) {
      case 'PLANIFIEE':
        return 'A_FAIRE';
      case 'REALISEE':
      case 'VERIFIEE':
        return 'TERMINEE';
      case 'EN_COURS':
      default:
        return 'EN_COURS';
    }
  }
}
