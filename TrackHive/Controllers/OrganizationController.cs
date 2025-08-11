using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackHive.Models.Dashboard;

namespace TrackHive.Controllers
{
    [Authorize(Roles = "IT")]
    public class OrganizationController : Controller
    {
        [HttpGet]
        public IActionResult Dashboard()
        {
            // TODO: Pull actual data from database
            var model = new ITDashboardViewModel
            {
                OrganizationName = "My Organization",
                HrsCount = 0,
                EmployeesCount = 0,
                CurrentPlan = "Free Tier (up to 5 employees)"
            };

            return View(model);
        }
    }
}
