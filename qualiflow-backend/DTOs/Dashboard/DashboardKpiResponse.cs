namespace DocApi.DTOs.Dashboard
{
    public class DashboardKpiResponse
    {
        public int TotalOrganizations { get; set; }
        public int ActiveOrganizations { get; set; }
        public int SuspendedOrganizations { get; set; }
        public int TotalUsers { get; set; }
        public int TotalOrganizationAdmins { get; set; }
        public int TotalProcesses { get; set; }
        public int TotalDocuments { get; set; }
        public int OpenNonConformities { get; set; }
        public int OverdueCorrectiveActions { get; set; }
        public int AlertIndicators { get; set; }
    }
}
