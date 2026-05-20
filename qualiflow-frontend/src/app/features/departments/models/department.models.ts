export type DepartmentStatus = 'ACTIF' | 'INACTIF';

export interface DepartmentQueryParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  status?: DepartmentStatus | '';
  managerUserId?: number | null;
  userId?: number | null;
}

export interface CreateDepartmentRequest {
  name: string;
  code: string;
  description?: string | null;
  managerUserId?: number | null;
  status: DepartmentStatus;
}

export interface UpdateDepartmentRequest extends CreateDepartmentRequest {}

export interface AssignManagerRequest {
  managerUserId?: number | null;
}

export interface AssignUsersRequest {
  userIds: number[];
}

export interface LinkDepartmentDocumentsRequest {
  documentIds: number[];
}

export interface DepartmentListItemResponse {
  id: number;
  organizationId: number;
  name: string;
  code: string;
  description?: string | null;
  managerUserId?: number | null;
  managerFullName?: string | null;
  status: DepartmentStatus;
  usersCount: number;
  documentsCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DepartmentResponse {
  id: number;
  organizationId: number;
  name: string;
  code: string;
  description?: string | null;
  managerUserId?: number | null;
  managerFullName?: string | null;
  managerRole?: string | null;
  status: DepartmentStatus;
  usersCount: number;
  documentsCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DepartmentUserResponse {
  id: number;
  organizationId?: number | null;
  departmentId?: number | null;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  function?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DepartmentDocumentResponse {
  id: number;
  organizationId: number;
  departmentId?: number | null;
  code: string;
  title: string;
  type: string;
  status?: string | null;
  ownerUserId?: number | null;
  ownerFullName?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DepartmentStatisticsResponse {
  total: number;
  active: number;
  inactive: number;
  withManager: number;
  usersCount: number;
  documentsCount: number;
}

export interface DepartmentDetailsResponse {
  department: DepartmentResponse;
  users: DepartmentUserResponse[];
  documents: DepartmentDocumentResponse[];
  statistics: DepartmentStatisticsResponse;
}

export interface PagedDepartmentResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: DepartmentListItemResponse[];
}

export const DEPARTMENT_STATUS_OPTIONS: Array<{ value: DepartmentStatus; label: string }> = [
  { value: 'ACTIF', label: 'Actif' },
  { value: 'INACTIF', label: 'Inactif' }
];
