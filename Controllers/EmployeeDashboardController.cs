using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;

namespace TrackHive.Controllers;

[Authorize(Roles = "Employee")]
public sealed class EmployeeDashboardController : Controller
{
    private readonly AppDbContext _db;
    public EmployeeDashboardController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Global filter will redirect if MustChangePassword == true
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return RedirectToAction("Login", "Auth");

        var user = await _db.Users.FindAsync(id);
        if (user is null) return RedirectToAction("Login", "Auth");

        var org = await _db.Organizations.FindAsync(user.OrganizationId);
        ViewData["OrgName"] = org?.Name ?? "Organization";
        ViewData["UserName"] = user.Name;
        return View();
    }
}
