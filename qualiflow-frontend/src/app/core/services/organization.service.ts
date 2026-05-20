import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface OrganizationResponse {
  id: number;
  name: string;
  code: string;
  description?: string | null;
  type?: string | null;
  address?: string | null;
  email?: string | null;
  phone?: string | null;
  fax?: string | null;
  website?: string | null;
  logoPath?: string | null;
  status: string;
  subscriptionDaysRemaining: number;
  subscriptionMonitorEnabled: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface OrganizationListResponse {
  total: number;
  items: OrganizationResponse[];
}

export interface CreateOrganizationRequest {
  name: string;
  code: string;
  description?: string | null;
  type?: string | null;
  address?: string | null;
  email?: string | null;
  phone?: string | null;
  fax?: string | null;
  website?: string | null;
}

export interface UpdateOrganizationRequest {
  name: string;
  description?: string | null;
  type?: string | null;
  address?: string | null;
  email?: string | null;
  phone?: string | null;
  fax?: string | null;
  website?: string | null;
  status: string;
  subscriptionDaysRemaining?: number | null;
  subscriptionMonitorEnabled?: boolean | null;
}

export interface OrganizationLogoResponse {
  organizationId: number;
  logoPath?: string | null;
  updatedAt?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class OrganizationService {
  private readonly apiUrl = `${environment.apiUrl}/api/organizations`;
  private readonly logoRefreshSubject = new Subject<void>();
  readonly logoRefresh$ = this.logoRefreshSubject.asObservable();

  constructor(private readonly http: HttpClient) {}

  getAll(page: number = 1, pageSize: number = 10): Observable<OrganizationListResponse> {
    const params = new HttpParams()
      .set('pageNumber', page.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<OrganizationListResponse>(this.apiUrl, { params });
  }

  getById(id: number): Observable<OrganizationResponse> {
    return this.http.get<OrganizationResponse>(`${this.apiUrl}/${id}`);
  }

  create(request: CreateOrganizationRequest): Observable<number> {
    return this.http.post<number>(this.apiUrl, request);
  }

  update(id: number, request: UpdateOrganizationRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  toggleStatus(id: number, status: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/toggle-status`, { status });
  }

  getMyOrganization(): Observable<OrganizationResponse> {
    return this.http.get<OrganizationResponse>(`${this.apiUrl}/my`);
  }

  updateMyOrganization(request: UpdateOrganizationRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/my`, request);
  }

  uploadMyOrganizationLogo(file: File): Observable<OrganizationLogoResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<OrganizationLogoResponse>(`${this.apiUrl}/my/logo`, formData);
  }

  downloadMyOrganizationLogo(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/my/logo`, { responseType: 'blob' });
  }

  notifyLogoUpdated(): void {
    this.logoRefreshSubject.next();
  }
}
