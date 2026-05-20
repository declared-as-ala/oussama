export type NonConformityType = 'INTERNE' | 'EXTERNE';
export type NonConformitySeverity = 'MINEURE' | 'MAJEURE' | 'CRITIQUE';
export type NonConformityStatus = 'EN_ATTENTE_VALIDATION' | 'OUVERTE' | 'EN_COURS' | 'CLOTUREE';
export type CorrectiveActionStatus = 'A_FAIRE' | 'EN_COURS' | 'TERMINEE' | 'EN_RETARD';

export interface NonConformityQueryParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  status?: NonConformityStatus | '';
  severity?: NonConformitySeverity | '';
  processId?: number | null;
  responsibleUserId?: number | null;
  organizationId?: number | null;
}

export interface CreateNonConformityRequest {
  code: string;
  title: string;
  description?: string | null;
  type: NonConformityType;
  severity: NonConformitySeverity;
  processId?: number | null;
  procedureId?: number | null;
  detectedDate: string;
  responsibleUserId: number;
  /** Optional: omit for non-privileged roles — backend forces EN_ATTENTE_VALIDATION */
  status?: NonConformityStatus;
}

export type UpdateNonConformityRequest = CreateNonConformityRequest;

export interface UpdateNonConformityStatusRequest {
  status: NonConformityStatus;
}

export interface ValidateNonConformityRequest {
  code: string;
  responsibleUserId: number;
}

export interface CreateCorrectiveActionRequest {
  title: string;
  description?: string | null;
  responsibleUserId: number;
  dueDate: string;
  completionDate?: string | null;
  status: CorrectiveActionStatus;
}

export type UpdateCorrectiveActionRequest = CreateCorrectiveActionRequest;

export interface CorrectiveActionResponse {
  id: number;
  organizationId: number;
  nonConformityId: number;
  title: string;
  description?: string | null;
  responsibleUserId: number;
  responsibleFullName?: string | null;
  dueDate: string;
  completionDate?: string | null;
  status: CorrectiveActionStatus;
  isOverdue: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface NonConformityListItemResponse {
  id: number;
  organizationId: number;
  code: string;
  title: string;
  type: NonConformityType;
  severity: NonConformitySeverity;
  processId?: number | null;
  processCode?: string | null;
  procedureId?: number | null;
  procedureCode?: string | null;
  responsibleUserId: number;
  responsibleFullName?: string | null;
  detectedDate: string;
  status: NonConformityStatus;
  createdAt: string;
}

export interface NonConformityResponse {
  id: number;
  organizationId: number;
  code: string;
  title: string;
  description?: string | null;
  type: NonConformityType;
  severity: NonConformitySeverity;
  processId?: number | null;
  processCode?: string | null;
  processName?: string | null;
  procedureId?: number | null;
  procedureCode?: string | null;
  procedureTitle?: string | null;
  responsibleUserId: number;
  responsibleFullName?: string | null;
  detectedDate: string;
  status: NonConformityStatus;
  createdAt: string;
  updatedAt?: string | null;
}

export interface NonConformityAttachmentResponse {
  id: number;
  nonConformityId: number;
  organizationId: number;
  originalFileName: string;
  fileExtension?: string | null;
  mimeType?: string | null;
  fileSize?: number | null;
  createdAt: string;
}

export interface NonConformityDetailsResponse {
  nonConformity: NonConformityResponse;
  actions: CorrectiveActionResponse[];
  attachments: NonConformityAttachmentResponse[];
}

export interface PagedNonConformityResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: NonConformityListItemResponse[];
}

export interface NonConformityStatisticsResponse {
  total: number;
  pendingValidation: number;
  opened: number;
  inProgress: number;
  closed: number;
  critical: number;
  overdueActions: number;
  bySeverity: Record<string, number>;
  byStatus: Record<string, number>;
}

export const NON_CONFORMITY_TYPE_OPTIONS: Array<{ value: NonConformityType; label: string }> = [
  { value: 'INTERNE', label: 'Interne' },
  { value: 'EXTERNE', label: 'Externe' }
];

export const NON_CONFORMITY_SEVERITY_OPTIONS: Array<{ value: NonConformitySeverity; label: string }> = [
  { value: 'MINEURE', label: 'Mineure' },
  { value: 'MAJEURE', label: 'Majeure' },
  { value: 'CRITIQUE', label: 'Critique' }
];

export const NON_CONFORMITY_STATUS_OPTIONS: Array<{ value: NonConformityStatus; label: string }> = [
  { value: 'EN_ATTENTE_VALIDATION', label: 'En attente de validation' },
  { value: 'OUVERTE', label: 'Ouverte' },
  { value: 'EN_COURS', label: 'En cours' },
  { value: 'CLOTUREE', label: 'Cloturee' }
];

export const CORRECTIVE_ACTION_STATUS_OPTIONS: Array<{ value: CorrectiveActionStatus; label: string }> = [
  { value: 'A_FAIRE', label: 'A faire' },
  { value: 'EN_COURS', label: 'En cours' },
  { value: 'TERMINEE', label: 'Terminee' },
  { value: 'EN_RETARD', label: 'En retard' }
];
