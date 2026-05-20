import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  CreateInstructionRequest,
  CreateProcedureRequest,
  InstructionResponse,
  PagedProcedureResponse,
  ProcedureDetailsResponse,
  ProcedureListItemResponse,
  ProcedureQueryParams,
  ProcedureResponse,
  ProcedureStatisticsResponse,
  UpdateInstructionRequest,
  UpdateProcedureRequest,
  ProcedureActionLogResponse
} from '../models/procedure.models';

@Injectable({
  providedIn: 'root'
})
export class ProcedureService {
  private readonly endpoint = 'procedures';

  constructor(private readonly apiService: ApiService) {}

  getProcedures(params: ProcedureQueryParams = {}): Observable<PagedProcedureResponse> {
    return this.apiService.get<PagedProcedureResponse>(this.endpoint, params);
  }

  getProcedureById(id: number): Observable<ProcedureDetailsResponse> {
    return this.apiService.get<ProcedureDetailsResponse>(`${this.endpoint}/${id}`);
  }

  getProceduresByProcess(processId: number): Observable<ProcedureListItemResponse[]> {
    return this.apiService.get<ProcedureListItemResponse[]>(`${this.endpoint}/by-process/${processId}`);
  }

  createProcedure(payload: CreateProcedureRequest): Observable<ProcedureResponse> {
    return this.apiService.post<ProcedureResponse>(this.endpoint, payload);
  }

  updateProcedure(id: number, payload: UpdateProcedureRequest): Observable<ProcedureResponse> {
    return this.apiService.put<ProcedureResponse>(`${this.endpoint}/${id}`, payload);
  }

  deleteProcedure(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  toggleProcedureStatus(id: number): Observable<ProcedureResponse> {
    return this.apiService.patch<ProcedureResponse>(`${this.endpoint}/${id}/toggle-status`, {});
  }

  getProcedureStatistics(): Observable<ProcedureStatisticsResponse> {
    return this.apiService.get<ProcedureStatisticsResponse>(`${this.endpoint}/statistics`);
  }

  getInstructions(procedureId: number): Observable<InstructionResponse[]> {
    return this.apiService.get<InstructionResponse[]>(`${this.endpoint}/${procedureId}/instructions`);
  }

  createInstruction(procedureId: number, payload: CreateInstructionRequest): Observable<InstructionResponse> {
    return this.apiService.post<InstructionResponse>(`${this.endpoint}/${procedureId}/instructions`, payload);
  }

  updateInstruction(procedureId: number, instructionId: number, payload: UpdateInstructionRequest): Observable<InstructionResponse> {
    return this.apiService.put<InstructionResponse>(`${this.endpoint}/${procedureId}/instructions/${instructionId}`, payload);
  }

  deleteInstruction(procedureId: number, instructionId: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${procedureId}/instructions/${instructionId}`);
  }

  getActionLogs(procedureId: number): Observable<ProcedureActionLogResponse[]> {
    return this.apiService.get<ProcedureActionLogResponse[]>(`${this.endpoint}/${procedureId}/action-logs`);
  }

  deleteActionLog(logId: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/action-logs/${logId}`);
  }

  addProcessLink(processId: number, procedureId: number): Observable<{ message: string }> {
    return this.apiService.post<{ message: string }>(`${this.endpoint}/by-process/${processId}/link/${procedureId}`, {});
  }

  removeProcessLink(processId: number, procedureId: number): Observable<{ message: string }> {
    return this.apiService.delete<{ message: string }>(`${this.endpoint}/by-process/${processId}/link/${procedureId}`);
  }
}
