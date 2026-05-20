import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  AssignProcessActorsRequest,
  PagedProcessResponse,
  ProcessActorResponse,
  ProcessDetailsResponse,
  ProcessMapResponse,
  ProcessQueryParams,
  ProcessResponse,
  ProcessStatisticsResponse,
  CreateProcessRequest,
  UpdateProcessRequest,
  UpdateProcessPilotRequest
} from '../models/process.models';

@Injectable({
  providedIn: 'root'
})
export class ProcessService {
  private readonly endpoint = 'processes';

  constructor(private readonly apiService: ApiService) {}

  getProcesses(params: ProcessQueryParams = {}): Observable<PagedProcessResponse> {
    return this.apiService.get<PagedProcessResponse>(this.endpoint, params);
  }

  getProcessById(id: number): Observable<ProcessDetailsResponse> {
    return this.apiService.get<ProcessDetailsResponse>(`${this.endpoint}/${id}`);
  }

  createProcess(payload: CreateProcessRequest, organizationId?: number): Observable<ProcessResponse> {
    const params = organizationId ? `?organizationId=${organizationId}` : '';
    return this.apiService.post<ProcessResponse>(`${this.endpoint}${params}`, payload);
  }

  updateProcess(id: number, payload: UpdateProcessRequest): Observable<ProcessResponse> {
    return this.apiService.put<ProcessResponse>(`${this.endpoint}/${id}`, payload);
  }

  deleteProcess(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  toggleProcessStatus(id: number): Observable<ProcessResponse> {
    return this.apiService.patch<ProcessResponse>(`${this.endpoint}/${id}/toggle-status`, {});
  }

  updatePilot(id: number, payload: UpdateProcessPilotRequest): Observable<ProcessResponse> {
    return this.apiService.patch<ProcessResponse>(`${this.endpoint}/${id}/pilot`, payload);
  }

  getActors(id: number): Observable<ProcessActorResponse[]> {
    return this.apiService.get<ProcessActorResponse[]>(`${this.endpoint}/${id}/actors`);
  }

  assignActors(id: number, payload: AssignProcessActorsRequest): Observable<ProcessActorResponse[]> {
    return this.apiService.post<ProcessActorResponse[]>(`${this.endpoint}/${id}/actors`, payload);
  }

  removeActor(processId: number, userId: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${processId}/actors/${userId}`);
  }

  getProcessMap(): Observable<ProcessMapResponse> {
    return this.apiService.get<ProcessMapResponse>(`${this.endpoint}/map`);
  }

  getProcessStatistics(): Observable<ProcessStatisticsResponse> {
    return this.apiService.get<ProcessStatisticsResponse>(`${this.endpoint}/statistics`);
  }
}
