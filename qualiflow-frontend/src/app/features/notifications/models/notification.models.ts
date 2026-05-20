export type NotificationCategory = 'INFO' | 'SUCCESS' | 'WARNING' | 'ERROR';

export type NotificationPriority = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';

export type NotificationType =
  | 'DocumentCreated'
  | 'DocumentSubmitted'
  | 'DocumentApproved'
  | 'DocumentRejected'
  | 'DocumentArchived'
  | 'DocumentExpiring30'
  | 'DocumentExpiring7'
  | 'DocumentExpiring1'
  | 'DocumentExpired'
  | 'DOCUMENT_APPROVAL_REQUIRED'
  | 'DOCUMENT_EXPIRED'
  | 'DOCUMENT_NEW_VERSION'
  | 'PROCESS_WITHOUT_PILOT'
  | 'PROCEDURE_WITHOUT_RESPONSIBLE'
  | 'NONCONFORMITY_CREATED'
  | 'NONCONFORMITY_CRITICAL'
  | 'CORRECTIVE_ACTION_ASSIGNED'
  | 'CORRECTIVE_ACTION_DUE_SOON'
  | 'CORRECTIVE_ACTION_OVERDUE'
  | 'INDICATOR_ALERT'
  | 'USER_CREATED'
  | 'USER_DISABLED'
  | 'ORGANIZATION_SUSPENDED'
  | 'SYSTEM_ALERT';

export interface NotificationQueryParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  isRead?: boolean;
  category?: NotificationCategory | '';
  priority?: NotificationPriority | '';
  type?: NotificationType | '';
  fromDate?: string;
  toDate?: string;
}

export interface NotificationListItemResponse {
  id: number;
  type: NotificationType;
  category: NotificationCategory;
  title: string;
  message: string;
  priority: NotificationPriority;
  isRead: boolean;
  isArchived: boolean;
  actionUrl?: string | null;
  referenceType?: string | null;
  referenceId?: string | null;
  sourceModule?: string | null;
  redirectUrl?: string | null;
  createdAt: string;
}

export interface NotificationResponse extends NotificationListItemResponse {
  organizationId?: number | null;
  userId: number;
  readAt?: string | null;
}

export interface CreateNotificationRequest {
  type: string;
  category: NotificationCategory;
  title: string;
  message: string;
  priority?: NotificationPriority;
  organizationId?: number | null;
  targetUserId?: number | null;
  targetRole?: string | null;
  sourceModule?: string | null;
  referenceId?: number | null;
  referenceType?: string | null;
  redirectUrl?: string | null;
  actionUrl?: string | null;
}

export interface RegisterWebPushSubscriptionRequest {
  endpoint: string;
  p256dh: string;
  auth: string;
}

export interface WebPushSubscriptionResponse {
  id: number;
  endpoint: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  lastUsedAt?: string | null;
}

export interface NotificationSignalRMessage extends NotificationResponse {}

export interface PagedNotificationResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: NotificationListItemResponse[];
}

export interface NotificationStatisticsResponse {
  total: number;
  unread: number;
  read: number;
  archived: number;
  critical: number;
  high: number;
  byCategory: Record<string, number>;
  byType: Record<string, number>;
}

export interface UnreadCountResponse {
  unreadCount: number;
}

export interface NotificationRecipientResponse {
  userId: number;
  fullName: string;
  email: string;
  role: string;
  roleType: 'Employee' | 'DepartmentManager' | 'QualityManager' | 'SuperAdmin' | string;
  departmentId?: number | null;
}

export interface NotificationRecipientsQueryRequest {
  eventType: string;
  documentId?: number;
  departmentId?: number;
}

export interface NotificationLogRequest {
  eventType: string;
  documentId: number;
  documentVersionId?: number | null;
  organizationId?: number | null;
  recipientUserId?: number | null;
  recipientRoleType?: string;
  channel?: string;
  subject: string;
  message: string;
  deliveryStatus?: string;
  externalMessageId?: string | null;
  payloadJson?: string | null;
  sentAt?: string | null;
}

export interface NotificationLogResponse {
  id: number;
  organizationId: number;
  documentId: number;
  documentVersionId?: number | null;
  eventType: string;
  recipientUserId?: number | null;
  recipientRoleType: string;
  channel: string;
  subject: string;
  message: string;
  deliveryStatus: string;
  externalMessageId?: string | null;
  sentAt?: string | null;
  createdAt: string;
}

export const NOTIFICATION_CATEGORY_OPTIONS: Array<{ value: NotificationCategory; label: string }> = [
  { value: 'INFO', label: 'Information' },
  { value: 'SUCCESS', label: 'Succes' },
  { value: 'WARNING', label: 'Avertissement' },
  { value: 'ERROR', label: 'Erreur' }
];

export const NOTIFICATION_PRIORITY_OPTIONS: Array<{ value: NotificationPriority; label: string }> = [
  { value: 'LOW', label: 'Basse' },
  { value: 'MEDIUM', label: 'Moyenne' },
  { value: 'HIGH', label: 'Haute' },
  { value: 'CRITICAL', label: 'Critique' }
];

export const NOTIFICATION_TYPE_OPTIONS: Array<{ value: NotificationType; label: string }> = [
  { value: 'DocumentCreated', label: 'Document créé' },
  { value: 'DocumentSubmitted', label: 'Document soumis' },
  { value: 'DocumentApproved', label: 'Document approuvé' },
  { value: 'DocumentRejected', label: 'Document rejeté' },
  { value: 'DocumentArchived', label: 'Document archivé' },
  { value: 'DocumentExpiring30', label: 'Document expire dans 30 jours' },
  { value: 'DocumentExpiring7', label: 'Document expire dans 7 jours' },
  { value: 'DocumentExpiring1', label: 'Document expire dans 1 jour' },
  { value: 'DocumentExpired', label: 'Document expiré' },
  { value: 'DOCUMENT_APPROVAL_REQUIRED', label: 'Document: validation requise' },
  { value: 'DOCUMENT_EXPIRED', label: 'Document: expire' },
  { value: 'DOCUMENT_NEW_VERSION', label: 'Document: nouvelle version' },
  { value: 'PROCESS_WITHOUT_PILOT', label: 'Processus sans pilote' },
  { value: 'PROCEDURE_WITHOUT_RESPONSIBLE', label: 'Procedure sans responsable' },
  { value: 'NONCONFORMITY_CREATED', label: 'Non-conformite creee' },
  { value: 'NONCONFORMITY_CRITICAL', label: 'Non-conformite critique' },
  { value: 'CORRECTIVE_ACTION_ASSIGNED', label: 'Action corrective assignee' },
  { value: 'CORRECTIVE_ACTION_DUE_SOON', label: 'Action corrective proche echeance' },
  { value: 'CORRECTIVE_ACTION_OVERDUE', label: 'Action corrective en retard' },
  { value: 'INDICATOR_ALERT', label: 'Indicateur en alerte' },
  { value: 'USER_CREATED', label: 'Utilisateur cree' },
  { value: 'USER_DISABLED', label: 'Utilisateur desactive' },
  { value: 'ORGANIZATION_SUSPENDED', label: 'Organisation suspendue' },
  { value: 'SYSTEM_ALERT', label: 'Alerte systeme' }
];
