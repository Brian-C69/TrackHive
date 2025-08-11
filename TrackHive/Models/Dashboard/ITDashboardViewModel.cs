using Microsoft.AspNetCore.Mvc;
using TrackHive.Models.Dashboard;

namespace TrackHive.Models.Dashboard
{
    public class ITDashboardViewModel
    {
        public string OrganizationName { get; set; } = string.Empty;
        public int HrsCount { get; set; }
        public int EmployeesCount { get; set; }
        public string CurrentPlan { get; set; } = string.Empty;

        // Show upgrade banner when at/over the free limit
        public int EmployeeLimit { get; set; } = 5;
    }
}

