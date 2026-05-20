export interface DashboardQueryParams {
  period?: string;
  organizationId?: number | null;
  status?: string | null;
  type?: string | null;
}

export interface DashboardKpiResponse {
  totalOrganizations: number;
  activeOrganizations: number;
  suspendedOrganizations: number;
  totalUsers: number;
  totalOrganizationAdmins: number;
  totalProcesses: number;
  totalDocuments: number;
  openNonConformities: number;
  overdueCorrectiveActions: number;
  alertIndicators: number;
}

export interface DashboardChartDataPointResponse {
  label: string;
  value: number;
}

export interface DashboardMonthlyTrendPointResponse {
  period: string;
  value: number;
}

export interface TopOrganizationResponse {
  organizationId: number;
  organizationName: string;
  usersCount: number;
  documentsCount: number;
  nonConformitiesCount: number;
}

export interface DashboardChartResponse {
  organizationsByStatus: DashboardChartDataPointResponse[];
  organizationsByType: DashboardChartDataPointResponse[];
  usersByRole: DashboardChartDataPointResponse[];
  topOrganizationsByUsers: TopOrganizationResponse[];
  topOrganizationsByDocuments: TopOrganizationResponse[];
  topOrganizationsByNonConformities: TopOrganizationResponse[];
  monthlyOrganizationsCreated: DashboardMonthlyTrendPointResponse[];
  monthlyUsersCreated: DashboardMonthlyTrendPointResponse[];
  monthlyNonConformities: DashboardMonthlyTrendPointResponse[];
  alertIndicatorsByOrganization: DashboardChartDataPointResponse[];
}

export interface DashboardAlertResponse {
  type: string;
  title: string;
  description: string;
  severity: 'LOW' | 'MEDIUM' | 'HIGH' | 'INFO' | string;
  referenceId?: string | null;
  createdAt: string;
}

export interface DashboardRecentActivityResponse {
  type: string;
  title: string;
  description: string;
  createdAt: string;
  actorName?: string | null;
}

export interface SuperAdminDashboardResponse {
  kpis: DashboardKpiResponse;
  charts: DashboardChartResponse;
  alerts: DashboardAlertResponse[];
  recentActivities: DashboardRecentActivityResponse[];
  topOrganizations: TopOrganizationResponse[];
}
