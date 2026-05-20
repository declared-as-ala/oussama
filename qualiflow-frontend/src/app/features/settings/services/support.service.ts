import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  SubmitSupportTicketRequest,
  SubmitSupportTicketResponse,
  SupportContactInfoResponse
} from '../models/support.models';

@Injectable({
  providedIn: 'root'
})
export class SupportService {
  private readonly endpoint = 'support';

  constructor(private readonly apiService: ApiService) {}

  getContactInfo(): Observable<SupportContactInfoResponse> {
    return this.apiService.get<SupportContactInfoResponse>(`${this.endpoint}/contact-info`);
  }

  submitTicket(payload: SubmitSupportTicketRequest): Observable<SubmitSupportTicketResponse> {
    return this.apiService.post<SubmitSupportTicketResponse>(`${this.endpoint}/ticket`, payload);
  }
}
