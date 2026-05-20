import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';

export type UserRole = 'ADMIN_ORG' | 'RESPONSABLE_QUALITE' | 'CHEF_SERVICE' | 'UTILISATEUR';

export interface UserResponse {
  id: number;
  organizationId?: number;
  organizationName?: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  function?: string;
  department?: string;
  departmentId?: number | null;
  departmentName?: string | null;
  isActive: boolean;
  lastLoginAt?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface UserListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: UserResponse[];
}

export interface CreateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  role: UserRole;
  function?: string;
  department?: string;
  departmentId?: number | null;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  function?: string;
  department?: string;
  departmentId?: number | null;
}

export interface ChangeUserRoleRequest {
  role: UserRole;
}

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly endpoint = 'users';

  constructor(private readonly apiService: ApiService) { }

  getUsers(page = 1, pageSize = 50): Observable<UserListResponse> {
    return this.apiService.get<UserListResponse>(this.endpoint, { page, pageSize });
  }

  searchUsers(searchTerm: string, page = 1, pageSize = 50): Observable<UserListResponse> {
    return this.apiService.get<UserListResponse>(`${this.endpoint}/search`, { searchTerm, page, pageSize });
  }

  getUserById(id: number): Observable<UserResponse> {
    return this.apiService.get<UserResponse>(`${this.endpoint}/${id}`);
  }

  createUser(payload: CreateUserRequest): Observable<number> {
    return this.apiService.post<number>(this.endpoint, payload);
  }

  updateUser(id: number, payload: UpdateUserRequest): Observable<void> {
    return this.apiService.put<void>(`${this.endpoint}/${id}`, payload);
  }

  changeRole(id: number, payload: ChangeUserRoleRequest): Observable<void> {
    return this.apiService.patch<void>(`${this.endpoint}/${id}/change-role`, payload);
  }

  toggleStatus(id: number, isActive: boolean): Observable<void> {
    return this.apiService.patch<void>(`${this.endpoint}/${id}/toggle-status`, { isActive });
  }

  deleteUser(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  hardDeleteUser(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}/permanent`);
  }
}
