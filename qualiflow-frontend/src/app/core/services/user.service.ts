import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

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
  organizationId?: number;
  role: string;
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
  role: string;
}

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private apiUrl = `${environment.apiUrl}/api/users`;

  constructor(private http: HttpClient) { }

  getAll(page: number = 1, pageSize: number = 10): Observable<UserListResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<UserListResponse>(this.apiUrl, { params });
  }

  search(searchTerm?: string, page: number = 1, pageSize: number = 10): Observable<UserListResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }

    return this.http.get<UserListResponse>(`${this.apiUrl}/search`, { params });
  }

  getById(id: number): Observable<UserResponse> {
    return this.http.get<UserResponse>(`${this.apiUrl}/${id}`);
  }

  create(request: CreateUserRequest): Observable<number> {
    return this.http.post<number>(this.apiUrl, request);
  }

  update(id: number, request: UpdateUserRequest): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}`, request);
  }

  toggleStatus(id: number, isActive: boolean): Observable<any> {
    return this.http.patch(`${this.apiUrl}/${id}/toggle-status`, { isActive });
  }

  changeRole(id: number, request: ChangeUserRoleRequest): Observable<any> {
    return this.http.patch(`${this.apiUrl}/${id}/change-role`, request);
  }

  delete(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }
}
