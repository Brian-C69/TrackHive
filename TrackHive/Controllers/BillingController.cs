using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Data;
using TrackHive.Models.Billing;

namespace TrackHive.Controllers
{
    // Later you can add [Authorize] here
    public class BillingController : Controller
    {
        private readonly AppDbContext _db;
        public BillingController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Plans()
        {
            if (!Request.Cookies.TryGetValue("orgId", out var orgCookie) || !Guid.TryParse(orgCookie, out var orgId))
                return RedirectToAction("Index", "Home");

            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId);
            if (org is null) return RedirectToAction("Index", "Home");

            var vm = new BillingPlansViewModel
            {
                OrganizationName = org.Name,
                CurrentPlan = org.Plan,              // e.g., "Free", "Starter", "Pro", "Enterprise"
                EmployeeLimit = org.EmployeeLimit    // e.g., 5 for Free
            };

            ViewData["Title"] = "Plans";
            return View(vm);
        }
    }
}

