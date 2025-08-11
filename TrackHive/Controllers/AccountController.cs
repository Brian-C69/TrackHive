using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Data;
using TrackHive.Models.Auth;
using TrackHive.Models.Core;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    public AccountController(AppDbContext db) => _db = db;

    // GET: /Account/Register
    [HttpGet]
    public IActionResult Register() => View(new RegisterOrganizationViewModel());

    // POST: /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterOrganizationViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Create org (Free plan by default, 5 employee limit)
        var org = new Organization
        {
            Name = vm.OrganizationName,
            Plan = "Free",
            EmployeeLimit = 5
        };
        _db.Organizations.Add(org);

        // Create first IT user (Super Admin)
        var user = new AppUser
        {
            Organization = org,
            Name = vm.AdminName,
            Email = vm.Email,
            PasswordHash = vm.Password, // TODO: Hash later
            Role = UserRole.IT,
            MustChangePassword = true
        };
        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        // Store orgId in cookie for session context
        Response.Cookies.Append("orgId", org.Id.ToString(),
            new CookieOptions { HttpOnly = true, IsEssential = true });

        return RedirectToAction("Dashboard", "Organization");
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await _db.Users
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Email == model.Email);

        if (user is not null && user.PasswordHash == model.Password) // TODO: Hash later
        {
            Response.Cookies.Append("orgId", user.OrganizationId.ToString(),
                new CookieOptions { HttpOnly = true, IsEssential = true });

            // If IT, go to IT Dashboard
            if (user.Role == UserRole.IT)
                return RedirectToAction("Dashboard", "Organization");

            // If HR, go to HR Dashboard (later)
            if (user.Role == UserRole.HR)
                return RedirectToAction("Dashboard", "Hr");

            // If Employee, go to Employee Dashboard (later)
            if (user.Role == UserRole.Employee)
                return RedirectToAction("Dashboard", "Employee");
        }

        ViewData["AuthError"] = "Incorrect email or password.";
        return View(model);
    }

    [HttpGet]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("orgId");
        return RedirectToAction("Index", "Home");
    }
}
