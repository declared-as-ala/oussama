export type DocumentType =
  | 'MANUEL'
  | 'PROCEDURE'
  | 'ENREGISTREMENT'
  | 'FORMULAIRE'
  | 'INSTRUCTION'
  | 'POLITIQUE'
  | 'AUTRE';

export type DocumentStatus =
  | 'BROUILLON'
  | 'EN_REVISION'
  | 'APPROUVE'
  | 'PUBLIE'
  | 'REJETE'
  | 'PERIME'
  | 'ARCHIVE';

export interface DocumentQueryParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  type?: DocumentType | '';
  status?: DocumentStatus | '';
  processId?: number | null;
  procedureId?: number | null;
  ownerUserId?: number | null;
  pendingValidationOnly?: boolean;
}

export interface CreateDocumentRequest {
  processId?: number | null;
  procedureId?: number | null;
  code: string;
  title: string;
  type: DocumentType;
  description?: string | null;
  category?: string | null;
  keywords?: string | null;
  signature?: string | null;
  ownerUserId?: number | null;
  isActive: boolean;
}

export type UpdateDocumentRequest = CreateDocumentRequest;

export interface CreateDocumentVersionRequest {
  versionNumber: string;
  status: DocumentStatus;
  revisionComment?: string | null;
  effectiveDate?: string | null;
  expiryDate?: string | null;
  signature?: string | null;
}

export interface UpdateDocumentVersionStatusRequest {
  status: DocumentStatus;
  revisionComment?: string | null;
}

export interface DocumentListItemResponse {
  id: number;
  organizationId: number;
  code: string;
  title: string;
  type: DocumentType;
  processId?: number | null;
  processCode?: string | null;
  processName?: string | null;
  procedureId?: number | null;
  procedureCode?: string | null;
  status: DocumentStatus;
  versionNumber?: string | null;
  expiryDate?: string | null;
  daysToExpiry?: number | null;
  expirationState?: 'VALID' | 'EXPIRING_SOON' | 'EXPIRED';
  updatedAt: string;
  ownerUserId?: number | null;
  ownerFullName?: string | null;
  fileName?: string | null;
  isActive: boolean;
  deletedAt?: string | null;
  daysUntilPermanentDelete?: number | null;
}

export interface DocumentResponse {
  id: number;
  organizationId: number;
  processId?: number | null;
  processCode?: string | null;
  processName?: string | null;
  procedureId?: number | null;
  procedureCode?: string | null;
  procedureTitle?: string | null;
  code: string;
  title: string;
  type: DocumentType;
  description?: string | null;
  category?: string | null;
  keywords?: string | null;
  signature?: string | null;
  ownerUserId?: number | null;
  ownerFullName?: string | null;
  currentVersionId?: number | null;
  currentVersionNumber?: string | null;
  currentVersionStatus?: DocumentStatus | null;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DocumentVersionResponse {
  id: number;
  documentId: number;
  organizationId: number;
  versionNumber: string;
  status: DocumentStatus;
  fileName?: string | null;
  originalFileName?: string | null;
  mimeType?: string | null;
  fileSize?: number | null;
  revisionComment?: string | null;
  signature?: string | null;
  effectiveDate?: string | null;
  expiryDate?: string | null;
  isCurrent: boolean;
  establishedByUserId: number;
  establishedByUser?: string | null;
  establishedAt: string;
  verifiedByUserId?: number | null;
  verifiedByUser?: string | null;
  verifiedAt?: string | null;
  validatedByUserId?: number | null;
  validatedByUser?: string | null;
  validatedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DocumentDetailsResponse {
  document: DocumentResponse;
  currentVersion?: DocumentVersionResponse | null;
  versions: DocumentVersionResponse[];
}

export interface DocumentActionLogResponse {
  id: number;
  organizationId: number;
  documentId: number;
  documentVersionId?: number | null;
  versionNumber?: string | null;
  actionType: string;
  oldValue?: string | null;
  newValue?: string | null;
  comment?: string | null;
  performedByUserId: number;
  performedByFullName?: string | null;
  performedAt: string;
}

export interface PagedDocumentResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: DocumentListItemResponse[];
}

export interface DocumentStatisticsResponse {
  total: number;
  approved: number;
  inReview: number;
  expired: number;
  draft: number;
  archived: number;
  recentlyUpdated: number;
  byType: Record<string, number>;
  byProcess: Record<string, number>;
}

export interface DocumentExpiringResponse {
  id: number;
  organizationId: number;
  code: string;
  title: string;
  status: DocumentStatus | string;
  versionNumber?: string | null;
  expiryDate?: string | null;
  daysToExpiry: number;
  expirationState: 'VALID' | 'EXPIRING_SOON' | 'EXPIRED';
  ownerUserId?: number | null;
  ownerFullName?: string | null;
}

export interface UpdateDocumentStatusRequest {
  status: DocumentStatus;
  revisionComment?: string | null;
}

export const DOCUMENT_TYPE_OPTIONS: Array<{ value: DocumentType; label: string }> = [
  { value: 'MANUEL', label: 'Manuel' },
  { value: 'PROCEDURE', label: 'Procedure' },
  { value: 'ENREGISTREMENT', label: 'Enregistrement' },
  { value: 'FORMULAIRE', label: 'Formulaire' },
  { value: 'INSTRUCTION', label: 'Instruction' },
  { value: 'POLITIQUE', label: 'Politique' },
  { value: 'AUTRE', label: 'Autre' }
];

export const DOCUMENT_STATUS_OPTIONS: Array<{ value: DocumentStatus; label: string }> = [
  { value: 'BROUILLON', label: 'Brouillon' },
  { value: 'EN_REVISION', label: 'En revision' },
  { value: 'APPROUVE', label: 'Approuve' },
  { value: 'PUBLIE', label: 'Publie' },
  { value: 'REJETE', label: 'Rejete' },
  { value: 'PERIME', label: 'Perime' },
  { value: 'ARCHIVE', label: 'Archive' }
];
