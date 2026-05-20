export type ProcessType = 'PILOTAGE' | 'REALISATION' | 'SUPPORT';
export type ProcessStatus = 'ACTIF' | 'INACTIF';
export type ProcessActorType = 'PILOTE' | 'COPILOTE' | 'CONTRIBUTEUR' | 'OBSERVATEUR';

export interface ProcessQueryParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  type?: ProcessType | '';
  status?: ProcessStatus | '';
  pilotUserId?: number | null;
  organizationId?: number | null;
}

export interface CreateProcessRequest {
  code: string;
  name: string;
  description?: string | null;
  type: ProcessType;
  finalities: string[];
  scope: string[];
  suppliers: string[];
  clients: string[];
  inputData: string[];
  outputData: string[];
  objectives: string[];
  pilotUserId?: number | null;
  status: ProcessStatus;
}

export type UpdateProcessRequest = CreateProcessRequest;

export interface UpdateProcessPilotRequest {
  pilotUserId?: number | null;
}

export interface AssignProcessActorItem {
  userId: number;
  actorType: ProcessActorType;
}

export interface AssignProcessActorsRequest {
  actors: AssignProcessActorItem[];
}

export interface ProcessListItemResponse {
  id: number;
  code: string;
  name: string;
  type: ProcessType;
  status: ProcessStatus;
  pilotUserId?: number | null;
  pilotFullName?: string | null;
  organizationId: number;
  createdAt: string;
}

export interface ProcessResponse {
  id: number;
  organizationId: number;
  code: string;
  name: string;
  description?: string | null;
  type: ProcessType;
  finalities: string[];
  scope: string[];
  suppliers: string[];
  clients: string[];
  inputData: string[];
  outputData: string[];
  objectives: string[];
  pilotUserId?: number | null;
  pilotFullName?: string | null;
  status: ProcessStatus;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ProcessActorResponse {
  userId: number;
  fullName: string;
  email: string;
  function?: string | null;
  department?: string | null;
  actorType: ProcessActorType;
  assignedAt: string;
}

export interface ProcessDetailsResponse {
  process: ProcessResponse;
  actors: ProcessActorResponse[];
}

export interface PagedProcessResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: ProcessListItemResponse[];
}

export interface ProcessMapResponse {
  pilotageProcesses: ProcessListItemResponse[];
  realisationProcesses: ProcessListItemResponse[];
  supportProcesses: ProcessListItemResponse[];
}

export interface ProcessStatisticsResponse {
  total: number;
  active: number;
  inactive: number;
  byType: Record<string, number>;
  withPilot: number;
  withoutPilot: number;
}

export const PROCESS_TYPE_OPTIONS: Array<{ value: ProcessType; label: string }> = [
  { value: 'PILOTAGE', label: 'Pilotage' },
  { value: 'REALISATION', label: 'Realisation' },
  { value: 'SUPPORT', label: 'Support' }
];

export const PROCESS_STATUS_OPTIONS: Array<{ value: ProcessStatus; label: string }> = [
  { value: 'ACTIF', label: 'Actif' },
  { value: 'INACTIF', label: 'Inactif' }
];

export const PROCESS_ACTOR_TYPE_OPTIONS: Array<{ value: ProcessActorType; label: string }> = [
  { value: 'PILOTE', label: 'Pilote' },
  { value: 'COPILOTE', label: 'Copilote' },
  { value: 'CONTRIBUTEUR', label: 'Contributeur' },
  { value: 'OBSERVATEUR', label: 'Observateur' }
];
