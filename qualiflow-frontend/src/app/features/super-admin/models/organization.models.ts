export type OrganizationType = 'UNIVERSITE' | 'INSTITUT' | 'CENTRE' | 'ENTREPRISE';
export type OrganizationStatus = 'ACTIF' | 'SUSPENDUE';

export interface OrganizationListQueryParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  type?: OrganizationType | '';
  status?: OrganizationStatus | '';
  sortBy?: string;
  sortDirection?: 'ASC' | 'DESC';
}

export interface CreateOrganizationAdminRequest {
  firstName: string;
  lastName: string;
  email: string;
  temporaryPassword: string;
}

export interface CreateOrganizationUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  role: string;
  function?: string | null;
}

export interface CreateOrganizationRequest {
  name: string;
  code: string;
  description?: string | null;
  type: OrganizationType;
  address?: string | null;
  email?: string | null;
  phone?: string | null;
  status: OrganizationStatus;
  subscriptionDaysRemaining?: number | null;
  subscriptionMonitorEnabled?: boolean | null;
  firstAdmin?: CreateOrganizationAdminRequest | null;
}

export interface UpdateOrganizationRequest {
  name: string;
  description?: string | null;
  type: OrganizationType;
  address?: string | null;
  email?: string | null;
  phone?: string | null;
  status: OrganizationStatus;
  subscriptionDaysRemaining?: number | null;
  subscriptionMonitorEnabled?: boolean | null;
}

export interface ToggleOrganizationStatusRequest {
  status?: OrganizationStatus;
}

export interface OrganizationAdminSummaryResponse {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  createdAt: string;
}

export interface OrganizationUserSummaryResponse {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt: string;
}

export interface OrganizationUsersListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: OrganizationUserSummaryResponse[];
}

export interface OrganizationListItemResponse {
  id: number;
  name: string;
  code: string;
  type?: OrganizationType | string | null;
  status: OrganizationStatus | string;
  email?: string | null;
  phone?: string | null;
  logoPath?: string | null;
  subscriptionDaysRemaining: number;
  subscriptionMonitorEnabled: boolean;
  usersCount: number;
  adminsCount: number;
  createdAt: string;
}

export interface OrganizationResponse {
  id: number;
  name: string;
  code: string;
  description?: string | null;
  type?: OrganizationType | string | null;
  address?: string | null;
  email?: string | null;
  phone?: string | null;
  logoPath?: string | null;
  status: OrganizationStatus | string;
  subscriptionDaysRemaining: number;
  subscriptionMonitorEnabled: boolean;
  usersCount: number;
  adminsCount: number;
  admins: OrganizationAdminSummaryResponse[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface PagedOrganizationsResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: OrganizationListItemResponse[];
}

export const ORGANIZATION_TYPE_OPTIONS: Array<{ value: OrganizationType; label: string }> = [
  { value: 'UNIVERSITE', label: 'Universite' },
  { value: 'INSTITUT', label: 'Institut' },
  { value: 'CENTRE', label: 'Centre' },
  { value: 'ENTREPRISE', label: 'Entreprise' }
];

export const ORGANIZATION_STATUS_OPTIONS: Array<{ value: OrganizationStatus; label: string }> = [
  { value: 'ACTIF', label: 'Actif' },
  { value: 'SUSPENDUE', label: 'Suspendue' }
];
