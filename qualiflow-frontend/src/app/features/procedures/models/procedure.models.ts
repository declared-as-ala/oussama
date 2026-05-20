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
  processId: number;
  code: string;
  title: string;
  objective?: string | null;
  scope?: string | null;
  description?: string | null;
  responsibleUserId?: number | null;
  status: ProcedureStatus;
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
  createdAt: string;
  updatedAt?: string | null;
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
