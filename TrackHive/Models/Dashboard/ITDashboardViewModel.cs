namespace TrackHive.Models.Dashboard
{
    public class ITDashboardViewModel
    {
        public string OrganizationName { get; set; }
        public int HrsCount { get; set; }
        public int EmployeesCount { get; set; }
        public string CurrentPlan { get; set; }
    }
}
