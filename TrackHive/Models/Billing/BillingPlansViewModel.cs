using Microsoft.AspNetCore.Mvc;

namespace TrackHive.Models.Billing
{
    public class BillingPlansViewModel
    {
        public string OrganizationName { get; set; } = string.Empty;
        public string CurrentPlan { get; set; } = string.Empty; // e.g. "Free", "Pro"
        public int EmployeeLimit { get; set; } // e.g. 5 for Free
    }
}

