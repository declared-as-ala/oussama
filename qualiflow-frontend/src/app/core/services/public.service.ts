import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface OrganizationRequest {
  fullName: string;
  email: string;
  phone: string;
  country: string;
  jobTitle: string;
  organizationName: string;
  organizationType: string;
  message: string;
  validationCode: string;
}

export interface OrganizationRequestResponse {
  success: boolean;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class PublicService {
  private readonly apiUrl = `${environment.apiUrl}/api/public`;

  constructor(private readonly http: HttpClient) { }

  sendVerificationCode(email: string): Observable<OrganizationRequestResponse> {
    return this.http.post<OrganizationRequestResponse>(`${this.apiUrl}/send-verification-code`, { email });
  }

  verifyCode(email: string, code: string): Observable<OrganizationRequestResponse> {
    return this.http.post<OrganizationRequestResponse>(`${this.apiUrl}/verify-code`, { email, code });
  }

  submitOrganizationRequest(request: OrganizationRequest): Observable<OrganizationRequestResponse> {
    return this.http.post<OrganizationRequestResponse>(`${this.apiUrl}/organization-request`, request);
  }
}
