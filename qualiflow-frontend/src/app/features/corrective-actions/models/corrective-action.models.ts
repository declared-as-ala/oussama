export type CorrectiveActionType = 'CURATIVE' | 'CORRECTIVE' | 'RISQUE';
export type CorrectiveActionStatus = 'PLANIFIEE' | 'EN_COURS' | 'REALISEE' | 'VERIFIEE';

export interface GetCorrectiveActionsQueryRequest {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  status?: CorrectiveActionStatus | '';
  type?: CorrectiveActionType | '';
  responsibleUserId?: number | null;
  nonConformityId?: number | null;
  isOverdue?: boolean | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface CreateCorrectiveActionRequest {
  nonConformityId: number;
  type: CorrectiveActionType;
  title: string;
  description?: string | null;
  responsibleUserId: number;
  dueDate: string;
  status: CorrectiveActionStatus;
  proofRecordId?: number | null;
}

export interface UpdateCorrectiveActionRequest extends CreateCorrectiveActionRequest {
  completionDate?: string | null;
}

export interface UpdateCorrectiveActionStatusRequest {
  status: CorrectiveActionStatus;
  comment?: string | null;
}

export interface VerifyCorrectiveActionEffectivenessRequest {
  effectivenessVerified: boolean;
  effectivenessComment: string;
}

export interface CorrectiveActionListItemResponse {
  id: number;
  nonConformityId: number;
  nonConformityCode?: string | null;
  type: CorrectiveActionType;
  title: string;
  description?: string | null;
  responsibleUserId: number;
  responsibleFullName?: string | null;
  dueDate: string;
  status: CorrectiveActionStatus;
  isOverdue: boolean;
  completionDate?: string | null;
  createdAt: string;
}

export interface CorrectiveActionResponse {
  id: number;
  organizationId: number;
  nonConformityId: number;
  nonConformityCode?: string | null;
  type: CorrectiveActionType;
  title: string;
  description?: string | null;
  responsibleUserId: number;
  responsibleFullName?: string | null;
  dueDate: string;
  status: CorrectiveActionStatus;
  completionDate?: string | null;
  effectivenessVerified?: boolean | null;
  effectivenessComment?: string | null;
  proofRecordId?: number | null;
  isOverdue: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CorrectiveActionHistoryResponse {
  id: number;
  oldStatus?: CorrectiveActionStatus | null;
  newStatus: CorrectiveActionStatus;
  comment?: string | null;
  changedByUserId: number;
  changedByFullName?: string | null;
  changedAt: string;
}

export interface CorrectiveActionDetailsResponse {
  action: CorrectiveActionResponse;
  nonConformity: {
    id: number;
    code?: string | null;
    title?: string | null;
    description?: string | null;
  };
  responsible: {
    id: number;
    fullName?: string | null;
    email?: string | null;
  };
  proof?: {
    id: number;
    code?: string | null;
    title?: string | null;
    type?: string | null;
  } | null;
  history: CorrectiveActionHistoryResponse[];
}

export interface PagedCorrectiveActionResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: CorrectiveActionListItemResponse[];
}

export interface CorrectiveActionStatisticsResponse {
  total: number;
  planned: number;
  inProgress: number;
  completed: number;
  verified: number;
  overdue: number;
  byType: Record<string, number>;
  byResponsible: Record<string, number>;
  byNonConformity: Record<string, number>;
}

export const CORRECTIVE_ACTION_TYPE_OPTIONS: Array<{ value: CorrectiveActionType; label: string }> = [
  { value: 'CURATIVE', label: 'Curative' },
  { value: 'CORRECTIVE', label: 'Corrective' },
  { value: 'RISQUE', label: 'Risque' }
];

export const CORRECTIVE_ACTION_STATUS_OPTIONS: Array<{ value: CorrectiveActionStatus; label: string }> = [
  { value: 'PLANIFIEE', label: 'Planifiee' },
  { value: 'EN_COURS', label: 'En cours' },
  { value: 'REALISEE', label: 'Realisee' },
  { value: 'VERIFIEE', label: 'Verifiee' }
];
