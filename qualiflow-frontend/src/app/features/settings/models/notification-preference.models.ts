export interface NotificationPreferenceResponse {
  notificationType: string;
  inAppEnabled: boolean;
  emailEnabled: boolean;
}

export interface UpdateNotificationPreferenceItemRequest {
  notificationType: string;
  inAppEnabled: boolean;
  emailEnabled: boolean;
}

export interface UpdateNotificationPreferencesRequest {
  items: UpdateNotificationPreferenceItemRequest[];
}
