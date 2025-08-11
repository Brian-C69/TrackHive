using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Data;
using TrackHive.Models.Core;
using TrackHive.Models.Dashboard;

public class OrganizationController : Controller
{
    private readonly AppDbContext _db;
    public OrganizationController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        if (!Request.Cookies.TryGetValue("orgId", out var orgCookie) || !Guid.TryParse(orgCookie, out var orgId))
            return RedirectToAction("Index", "Home");

        var org = await _db.Organizations
            .Include(o => o.Users)
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org is null) return RedirectToAction("Index", "Home");

        var model = new ITDashboardViewModel
        {
            OrganizationName = org.Name,
            CurrentPlan = $"Free Tier (up to {org.EmployeeLimit} employees)",
            HrsCount = org.Users.Count(u => u.Role == UserRole.HR),
            EmployeesCount = org.Users.Count(u => u.Role == UserRole.Employee)
        };

        return View(model);
    }
}
