import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';

export interface OrganizationActivityResponse {
  type: string;
  actionType: string;
  title: string;
  description: string;
  createdAt: string;
  actorName?: string | null;
  module?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class OrganizationAuditService {
  private readonly endpoint = 'dashboard/organization/recent-activities';

  constructor(private readonly apiService: ApiService) {}

  getRecentActivities(params: any = {}): Observable<OrganizationActivityResponse[]> {
    return this.apiService.get<OrganizationActivityResponse[]>(this.endpoint, params);
  }
}
