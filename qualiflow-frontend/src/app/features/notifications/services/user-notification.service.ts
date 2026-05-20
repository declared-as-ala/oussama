import { Injectable } from '@angular/core';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from './notification.service';

@Injectable({
  providedIn: 'root'
})
export class UserNotificationService extends NotificationService {
  constructor(apiService: ApiService) {
    super(apiService);
  }
}
