import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  NotificationPreferenceResponse,
  UpdateNotificationPreferencesRequest
} from '../models/notification-preference.models';

@Injectable({
  providedIn: 'root'
})
export class NotificationSettingsService {
  private readonly endpoint = 'notifications/preferences';

  constructor(private readonly apiService: ApiService) {}

  getPreferences(): Observable<NotificationPreferenceResponse[]> {
    return this.apiService.get<NotificationPreferenceResponse[]>(this.endpoint);
  }

  updatePreferences(payload: UpdateNotificationPreferencesRequest): Observable<NotificationPreferenceResponse[]> {
    return this.apiService.put<NotificationPreferenceResponse[]>(this.endpoint, payload);
  }
}
