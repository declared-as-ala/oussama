import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  CreateOrganizationRequest,
  CreateOrganizationUserRequest,
  OrganizationListQueryParams,
  OrganizationResponse,
  OrganizationUsersListResponse,
  PagedOrganizationsResponse,
  ToggleOrganizationStatusRequest,
  UpdateOrganizationRequest
} from '../models/organization.models';

@Injectable({
  providedIn: 'root'
})
export class OrganizationService {
  private readonly endpoint = 'organizations';

  constructor(private readonly apiService: ApiService) { }

  getOrganizations(params: OrganizationListQueryParams = {}): Observable<PagedOrganizationsResponse> {
    return this.apiService.get<PagedOrganizationsResponse>(this.endpoint, params);
  }

  getOrganizationById(id: number): Observable<OrganizationResponse> {
    return this.apiService.get<OrganizationResponse>(`${this.endpoint}/${id}`);
  }

  createOrganization(payload: CreateOrganizationRequest): Observable<number> {
    return this.apiService.post<number>(this.endpoint, payload);
  }

  updateOrganization(id: number, payload: UpdateOrganizationRequest): Observable<void> {
    return this.apiService.put<void>(`${this.endpoint}/${id}`, payload);
  }

  toggleOrganizationStatus(id: number, payload: ToggleOrganizationStatusRequest = {}): Observable<OrganizationResponse> {
    return this.apiService.patch<OrganizationResponse>(`${this.endpoint}/${id}/toggle-status`, payload);
  }

  deleteOrganization(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  getOrganizationUsers(id: number, page = 1, pageSize = 200): Observable<OrganizationUsersListResponse> {
    return this.apiService.get<OrganizationUsersListResponse>(`${this.endpoint}/${id}/users`, { page, pageSize });
  }

  createOrganizationUser(organizationId: number, payload: CreateOrganizationUserRequest): Observable<number> {
    return this.apiService.post<number>(`${this.endpoint}/${organizationId}/users`, payload);
  }

  changeOrganizationUserRole(organizationId: number, userId: number, role: string): Observable<void> {
    return this.apiService.patch<void>(`${this.endpoint}/${organizationId}/users/${userId}/role`, { role });
  }
  getOrganizationRequests(): Observable<any[]> {
    return this.apiService.get<any[]>(`${this.endpoint}/requests`);
  }

  deleteOrganizationRequest(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/requests/${id}`);
  }
}
