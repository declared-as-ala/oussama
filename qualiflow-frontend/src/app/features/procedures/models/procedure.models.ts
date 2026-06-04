export type ProcedureStatus = 'ACTIF' | 'INACTIF';

export interface ProcedureQueryParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  processId?: number | null;
  status?: ProcedureStatus | '';
  responsibleUserId?: number | null;
  organizationId?: number | null;
}

export interface CreateProcedureRequest {
  processIds: number[];
  code: string;
  title: string;
  objective?: string | null;
  scope?: string | null;
  description?: string | null;
  responsibleUserId?: number | null;
  status: ProcedureStatus;
  versionNumber?: string | null;
  revisionComment?: string | null;
}

export type UpdateProcedureRequest = CreateProcedureRequest;

export interface CreateInstructionRequest {
  code: string;
  title: string;
  description?: string | null;
  status: ProcedureStatus;
  orderIndex?: number | null;
}

export type UpdateInstructionRequest = CreateInstructionRequest;

export interface ProcedureListItemResponse {
  id: number;
  organizationId: number;
  processId: number;
  processCode: string;
  processName: string;
  code: string;
  title: string;
  responsibleUserId?: number | null;
  responsibleFullName?: string | null;
  status: ProcedureStatus;
  versionNumber?: string | null;
  createdAt: string;
}

export interface LinkedDocumentRef {
  id: number;
  organizationId: number;
  code: string;
  title: string;
  type: string;
  processId?: number | null;
  processCode?: string | null;
  processName?: string | null;
  procedureId?: number | null;
  procedureCode?: string | null;
  status: string;
  versionNumber?: string | null;
  ownerUserId?: number | null;
  ownerFullName?: string | null;
  fileName?: string | null;
  isActive: boolean;
  updatedAt: string;
}

export interface LinkedProcessRef {
  id: number;
  organizationId: number;
  code: string;
  name: string;
  type: string;
  status: string;
  versionNumber?: string | null;
  pilotUserId?: number | null;
  createdAt: string;
}

export interface ProcedureResponse {
  id: number;
  organizationId: number;
  processId: number;
  processCode?: string | null;
  processName?: string | null;
  code: string;
  title: string;
  objective?: string | null;
  scope?: string | null;
  description?: string | null;
  responsibleUserId?: number | null;
  responsibleFullName?: string | null;
  status: ProcedureStatus;
  versionNumber?: string | null;
  revisionComment?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  documents: LinkedDocumentRef[];
  processes: LinkedProcessRef[];
}

export interface InstructionResponse {
  id: number;
  procedureId: number;
  organizationId: number;
  code: string;
  title: string;
  description?: string | null;
  status: ProcedureStatus;
  orderIndex: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ProcedureDetailsResponse {
  procedure: ProcedureResponse;
  instructions: InstructionResponse[];
}

export interface ProcedureActionLogResponse {
  id: number;
  organizationId: number;
  procedureId: number;
  actionType: string;
  oldValue?: string | null;
  newValue?: string | null;
  comment?: string | null;
  performedByUserId: number;
  performedByFullName?: string | null;
  performedAt: string;
}

export interface PagedProcedureResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: ProcedureListItemResponse[];
}

export interface ProcedureStatisticsResponse {
  total: number;
  active: number;
  inactive: number;
  withResponsible: number;
  withoutResponsible: number;
  byStatus: Record<string, number>;
}

export const PROCEDURE_STATUS_OPTIONS: Array<{ value: ProcedureStatus; label: string }> = [
  { value: 'ACTIF', label: 'Actif' },
  { value: 'INACTIF', label: 'Inactif' }
];
