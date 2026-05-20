export type IndicatorStatus = 'ACTIF' | 'INACTIF';
export type MeasurementFrequency = 'QUOTIDIEN' | 'HEBDOMADAIRE' | 'MENSUEL' | 'TRIMESTRIEL' | 'ANNUEL';

export interface GetIndicatorsQueryRequest {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  status?: IndicatorStatus | '';
  processId?: number | null;
  measurementFrequency?: MeasurementFrequency | '';
  responsibleUserId?: number | null;
  isInAlert?: boolean | null;
}

export interface CreateIndicatorRequest {
  processId: number;
  code: string;
  name: string;
  description?: string | null;
  calculationMethod?: string | null;
  unit?: string | null;
  targetValue: number;
  alertThreshold: number;
  measurementFrequency: MeasurementFrequency;
  responsibleUserId: number;
  status: IndicatorStatus;
}

export type UpdateIndicatorRequest = CreateIndicatorRequest;

export interface CreateIndicatorValueRequest {
  periodLabel: string;
  measuredValue: number;
  comment?: string | null;
  measuredAt: string;
}

export type UpdateIndicatorValueRequest = CreateIndicatorValueRequest;

export interface IndicatorListItemResponse {
  id: number;
  processId: number;
  processName?: string | null;
  code: string;
  name: string;
  unit?: string | null;
  targetValue: number;
  alertThreshold: number;
  latestValue?: number | null;
  latestMeasuredAt?: string | null;
  status: IndicatorStatus;
  responsibleFullName?: string | null;
  isInAlert: boolean;
  createdAt: string;
}

export interface IndicatorResponse {
  id: number;
  organizationId: number;
  processId: number;
  processCode?: string | null;
  processName?: string | null;
  code: string;
  name: string;
  description?: string | null;
  calculationMethod?: string | null;
  unit?: string | null;
  targetValue: number;
  alertThreshold: number;
  measurementFrequency: MeasurementFrequency;
  responsibleUserId: number;
  responsibleFullName?: string | null;
  status: IndicatorStatus;
  latestValue?: number | null;
  latestMeasuredAt?: string | null;
  isInAlert: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface IndicatorValueResponse {
  id: number;
  indicatorId: number;
  periodLabel: string;
  measuredValue: number;
  comment?: string | null;
  measuredAt: string;
  enteredByUserId: number;
  enteredByFullName?: string | null;
  createdAt: string;
}

export interface IndicatorAlertResponse {
  indicatorId: number;
  indicatorCode?: string | null;
  indicatorName?: string | null;
  message?: string | null;
  measuredValue: number;
  targetValue: number;
  alertThreshold: number;
  measuredAt: string;
}

export interface IndicatorDetailsResponse {
  indicator: IndicatorResponse;
  process: {
    id: number;
    code?: string | null;
    name?: string | null;
  };
  responsible: {
    id: number;
    fullName?: string | null;
    email?: string | null;
  };
  latestValue?: IndicatorValueResponse | null;
  isInAlert: boolean;
  valuesHistory: IndicatorValueResponse[];
  alerts: IndicatorAlertResponse[];
}

export interface PagedIndicatorResponse {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: IndicatorListItemResponse[];
}

export interface IndicatorStatisticsResponse {
  total: number;
  active: number;
  inactive: number;
  inAlert: number;
  byFrequency: Record<string, number>;
  byProcess: Record<string, number>;
}

export interface IndicatorChartResponse {
  labels: string[];
  values: number[];
  targetValue: number;
  thresholdValue: number;
}

export const INDICATOR_STATUS_OPTIONS: Array<{ value: IndicatorStatus; label: string }> = [
  { value: 'ACTIF', label: 'Actif' },
  { value: 'INACTIF', label: 'Inactif' }
];

export const INDICATOR_FREQUENCY_OPTIONS: Array<{ value: MeasurementFrequency; label: string }> = [
  { value: 'QUOTIDIEN', label: 'Quotidien' },
  { value: 'HEBDOMADAIRE', label: 'Hebdomadaire' },
  { value: 'MENSUEL', label: 'Mensuel' },
  { value: 'TRIMESTRIEL', label: 'Trimestriel' },
  { value: 'ANNUEL', label: 'Annuel' }
];
