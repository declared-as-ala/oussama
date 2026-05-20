import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  AssignManagerRequest,
  AssignUsersRequest,
  CreateDepartmentRequest,
  DepartmentDetailsResponse,
  DepartmentDocumentResponse,
  DepartmentQueryParams,
  DepartmentResponse,
  DepartmentStatisticsResponse,
  DepartmentUserResponse,
  LinkDepartmentDocumentsRequest,
  PagedDepartmentResponse,
  UpdateDepartmentRequest
} from '../models/department.models';

@Injectable({
  providedIn: 'root'
})
export class DepartmentService {
  private readonly endpoint = 'departments';

  constructor(private readonly apiService: ApiService) {}

  getDepartments(params: DepartmentQueryParams = {}): Observable<PagedDepartmentResponse> {
    return this.apiService.get<PagedDepartmentResponse>(this.endpoint, params);
  }

  getDepartmentById(id: number): Observable<DepartmentDetailsResponse> {
    return this.apiService.get<DepartmentDetailsResponse>(`${this.endpoint}/${id}`);
  }

  createDepartment(payload: CreateDepartmentRequest): Observable<DepartmentResponse> {
    return this.apiService.post<DepartmentResponse>(this.endpoint, payload);
  }

  updateDepartment(id: number, payload: UpdateDepartmentRequest): Observable<DepartmentResponse> {
    return this.apiService.put<DepartmentResponse>(`${this.endpoint}/${id}`, payload);
  }

  deleteDepartment(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  toggleStatus(id: number): Observable<DepartmentResponse> {
    return this.apiService.patch<DepartmentResponse>(`${this.endpoint}/${id}/toggle-status`, {});
  }

  assignManager(id: number, payload: AssignManagerRequest): Observable<DepartmentResponse> {
    return this.apiService.patch<DepartmentResponse>(`${this.endpoint}/${id}/manager`, payload);
  }

  assignUsers(id: number, payload: AssignUsersRequest): Observable<void> {
    return this.apiService.patch<void>(`${this.endpoint}/${id}/assign-users`, payload);
  }

  assignDocuments(id: number, payload: LinkDepartmentDocumentsRequest): Observable<void> {
    return this.apiService.patch<void>(`${this.endpoint}/${id}/assign-documents`, payload);
  }

  getUsers(id: number): Observable<DepartmentUserResponse[]> {
    return this.apiService.get<DepartmentUserResponse[]>(`${this.endpoint}/${id}/users`);
  }

  getDocuments(id: number): Observable<DepartmentDocumentResponse[]> {
    return this.apiService.get<DepartmentDocumentResponse[]>(`${this.endpoint}/${id}/documents`);
  }

  getStatistics(): Observable<DepartmentStatisticsResponse> {
    return this.apiService.get<DepartmentStatisticsResponse>(`${this.endpoint}/statistics`);
  }
}
