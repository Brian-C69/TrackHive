// File: Controllers/DashboardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrackHive.Models;

namespace TrackHive.Controllers;

[Authorize(Roles = "IT,HR")]
public sealed class DashboardController : Controller
{
    [HttpGet]
    public IActionResult My()
    {
        if (User.IsInRole("IT")) return RedirectToAction("Index", "Dashboard");
        if (User.IsInRole("HR")) return RedirectToAction("Index", "HrDashboard");
        if (User.IsInRole("Employee")) return RedirectToAction("Index", "EmployeeDashboard");
        return RedirectToAction("Index", "Home");
    }

    private readonly AppDbContext _db;
    private readonly EmailService _email;

    public DashboardController(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    [Authorize(Roles = "IT")]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var orgId = GetOrgId();
        var org = await _db.Organizations.FindAsync(orgId);
        ViewData["OrgName"] = org?.Name ?? "Organization";
        return View(new InviteHRViewModel());
    }

    [Authorize(Roles = "IT")]
    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> InviteHR(InviteHRViewModel model)
    {
        var orgId = GetOrgId();
        var org = await _db.Organizations.FindAsync(orgId);

        if (!ModelState.IsValid || org == null)
        {
            model.ErrorMessage = org == null ? "Organization not found." : null;
            ViewData["OrgName"] = org?.Name ?? "Organization";
            return View("Index", model);
        }

        var emailLower = model.Email.Trim().ToLower();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
        if (exists)
        {
            ModelState.AddModelError(nameof(InviteHRViewModel.Email), "This email is already registered.");
            ViewData["OrgName"] = org.Name;
            return View("Index", model);
        }

        var tempPassword = GenerateTempPassword();

        var hr = new AppUser
        {
            Name = model.Name.Trim(),
            Email = model.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(tempPassword),
            Role = RoleType.HR,
            OrganizationId = org.Id,
            MustChangePassword = true,
            IsActive = true
        };

        _db.Users.Add(hr);
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var loginUrl = $"{baseUrl}/Auth/Login";

        var subject = $"You're invited as HR to {org.Name} (TrackHive)";
        var body = $@"
        <p>Hi {System.Net.WebUtility.HtmlEncode(hr.Name)},</p>
        <p>You have been invited as <strong>HR</strong> to <strong>{System.Net.WebUtility.HtmlEncode(org.Name)}</strong> on TrackHive.</p>
        <p><strong>Login:</strong> <a href=""{loginUrl}"">{loginUrl}</a><br/>
        <strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(hr.Email)}<br/>
        <strong>Temporary Password:</strong> {System.Net.WebUtility.HtmlEncode(tempPassword)}</p>
        <p>For security, you'll be asked to change this password after sign-in.</p>
        <p>— TrackHive</p>";

        var (ok, error) = await _email.SendAsync(hr.Email, subject, body);
        if (!ok)
        {
            model.ErrorMessage = $"HR created, but email failed to send: {error}";
        }
        else
        {
            model.SuccessMessage = $"Invited HR '{hr.Name}' at {hr.Email}.";
            ModelState.Clear();
            model = new InviteHRViewModel { SuccessMessage = model.SuccessMessage };
        }

        ViewData["OrgName"] = org.Name;
        return View("Index", model);
    }

    // IT can invite HR/Employee; HR can invite Employee only
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteUser(InviteUserViewModel model, string returnTab = "employees")
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please correct the highlighted fields.";
            return RedirectToAction(nameof(People), new { tab = returnTab });
        }

        var orgId = GetOrgId();
        var isHR = User.IsInRole("HR");
        if (isHR) model.Role = RoleType.Employee; // enforce
        if (model.Role != RoleType.HR && model.Role != RoleType.Employee)
        {
            TempData["Error"] = "Invalid role.";
            return RedirectToAction(nameof(People), new { tab = returnTab });
        }

        var emailLower = model.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
        if (exists)
        {
            TempData["Error"] = "Email is already registered.";
            return RedirectToAction(nameof(People), new { tab = returnTab });
        }

        var tempPassword = GenerateTempPassword();

        var user = new AppUser
        {
            Name = model.Name.Trim(),
            Email = model.Email.Trim(),
            Role = model.Role,
            OrganizationId = orgId,
            PasswordHash = PasswordHasher.Hash(tempPassword),
            MustChangePassword = true,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var org = await _db.Organizations.FindAsync(orgId);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var loginUrl = $"{baseUrl}/Auth/Login";
        var subject = model.Role == RoleType.HR
            ? $"You're invited as HR to {org?.Name ?? "your organization"} (TrackHive)"
            : $"You're invited to {org?.Name ?? "your organization"} (TrackHive)";
        var body = $@"
<p>Hi {System.Net.WebUtility.HtmlEncode(user.Name)},</p>
<p>You have been invited as <strong>{user.Role}</strong> to <strong>{System.Net.WebUtility.HtmlEncode(org?.Name ?? "your organization")}</strong> on TrackHive.</p>
<p><strong>Login:</strong> <a href=""{loginUrl}"">{loginUrl}</a><br/>
<strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(user.Email)}<br/>
<strong>Temporary Password:</strong> {System.Net.WebUtility.HtmlEncode(tempPassword)}</p>
<p>You'll be asked to change this password after sign-in.</p>
<p>— TrackHive</p>";
        var (ok, err) = await _email.SendAsync(user.Email, subject, body);
        TempData[ok ? "Toast" : "Error"] = ok
            ? $"Invited {user.Role} '{user.Name}' at {user.Email}."
            : $"User created, but email failed to send: {err}";

        var tab = user.Role == RoleType.HR ? "hr" : "employees";
        return RedirectToAction(nameof(People), new { tab });
    }

    // Search + Pagination: /Dashboard/People?tab=employees|hr&q=...&page=1&pageSize=15
    [HttpGet]
    public async Task<IActionResult> People(string? tab = null, string? q = null, int page = 1, int pageSize = 15,
                                        string? sortBy = "created", string? sortDir = "desc")
    {
        var orgId = GetOrgId();
        var isIT = User.IsInRole("IT");
        var active = isIT && (tab?.ToLowerInvariant() == "hr") ? "hr" : "employees";

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 15 : pageSize;

        sortBy = (sortBy ?? "created").ToLowerInvariant();
        if (sortBy is not ("name" or "email" or "created")) sortBy = "created";
        sortDir = (sortDir ?? "desc").ToLowerInvariant();
        if (sortDir is not ("asc" or "desc")) sortDir = "desc";

        IQueryable<AppUser> query = _db.Users.AsNoTracking()
            .Where(u => u.OrganizationId == orgId);

        query = active == "hr"
            ? query.Where(u => u.Role == RoleType.HR)
            : query.Where(u => u.Role == RoleType.Employee);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(u => EF.Functions.Like(u.Name, pattern) ||
                                     EF.Functions.Like(u.Email, pattern));
        }

        // Sorting
        query = (sortBy, sortDir) switch
        {
            ("name", "asc") => query.OrderBy(u => u.Name).ThenByDescending(u => u.Id),
            ("name", "desc") => query.OrderByDescending(u => u.Name).ThenByDescending(u => u.Id),
            ("email", "asc") => query.OrderBy(u => u.Email).ThenByDescending(u => u.Id),
            ("email", "desc") => query.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id),
            ("created", "asc") => query.OrderBy(u => u.CreatedAt).ThenByDescending(u => u.Id),
            _ => query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var vm = new PeopleListViewModel
        {
            ActiveTab = active,
            Query = q,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            SortBy = sortBy,
            SortDir = sortDir,
            Employees = active == "employees" ? items : new(),
            HRs = active == "hr" ? items : new()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var orgId = GetOrgId();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId);
        if (u is null) return NotFound();
        if (User.IsInRole("HR") && u.Role != RoleType.Employee) return Forbid();

        var vm = new EditUserViewModel
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Role = u.Role
        };
        return View("EditUser", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!ModelState.IsValid) return View("EditUser", model);

        var orgId = GetOrgId();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == model.Id && x.OrganizationId == orgId);
        if (u is null) return NotFound();
        if (User.IsInRole("HR") && u.Role != RoleType.Employee) return Forbid();

        var newEmailLower = model.Email.Trim().ToLowerInvariant();
        var emailUsed = await _db.Users.AnyAsync(x => x.Email.ToLower() == newEmailLower && x.Id != u.Id);
        if (emailUsed)
        {
            ModelState.AddModelError(nameof(EditUserViewModel.Email), "Email is already used by another user.");
            return View("EditUser", model);
        }

        u.Name = model.Name.Trim();
        u.Email = model.Email.Trim();
        await _db.SaveChangesAsync();

        TempData["Toast"] = "User updated.";
        var tab = u.Role == RoleType.HR ? "hr" : "employees";
        return RedirectToAction(nameof(People), new { tab });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, string returnTab = "employees")
    {
        var orgId = GetOrgId();
        var meIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = int.TryParse(meIdStr, out var meId);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == orgId);
        if (user is null) return NotFound();
        if (user.Id == meId)
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(People), new { tab = returnTab });
        }
        if (User.IsInRole("HR") && user.Role != RoleType.Employee) return Forbid();

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        TempData["Toast"] = user.IsActive ? "Account reactivated." : "Account deactivated.";
        return RedirectToAction(nameof(People), new { tab = returnTab });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id, string returnTab = "employees")
    {
        var orgId = GetOrgId();
        var meIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = int.TryParse(meIdStr, out var meId);

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId);
        if (u is null) return NotFound();
        if (u.Id == meId)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(People), new { tab = returnTab });
        }
        if (User.IsInRole("HR") && u.Role != RoleType.Employee) return Forbid();

        _db.Users.Remove(u);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "User deleted.";
        return RedirectToAction(nameof(People), new { tab = returnTab });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkStatus(
    string cmd, int[] ids,
    string returnTab = "employees",
    string? q = null, string? sortBy = "created", string? sortDir = "desc",
    int page = 1, int pageSize = 15)
    {
        if (ids is null || ids.Length == 0)
        {
            TempData["Error"] = "No users selected.";
            return RedirectToAction(nameof(People), new { tab = returnTab, q, sortBy, sortDir, page, pageSize });
        }

        cmd = (cmd ?? "").ToLowerInvariant();
        if (cmd is not ("activate" or "deactivate"))
        {
            TempData["Error"] = "Unknown bulk action.";
            return RedirectToAction(nameof(People), new { tab = returnTab, q, sortBy, sortDir, page, pageSize });
        }

        var orgId = GetOrgId();
        var meIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = int.TryParse(meIdStr, out var meId);
        var isHR = User.IsInRole("HR");

        var list = await _db.Users
            .Where(u => u.OrganizationId == orgId && ids.Contains(u.Id))
            .ToListAsync();

        int changed = 0, skipped = 0;
        foreach (var u in list)
        {
            // Why: never allow modifying self; HR can only touch Employees.
            if (u.Id == meId || (isHR && u.Role != RoleType.Employee))
            {
                skipped++;
                continue;
            }

            bool newState = cmd == "activate";
            if (u.IsActive != newState)
            {
                u.IsActive = newState;
                changed++;
            }
        }

        if (changed > 0) await _db.SaveChangesAsync();

        TempData["Toast"] = $"{(cmd == "activate" ? "Activated" : "Deactivated")} {changed} user(s)."
                            + (skipped > 0 ? $" Skipped {skipped}." : "");

        return RedirectToAction(nameof(People), new { tab = returnTab, q, sortBy, sortDir, page, pageSize });
    }

    private int GetOrgId()
    {
        var orgIdStr = User.FindFirstValue("OrgId") ?? "0";
        return int.TryParse(orgIdStr, out var id) ? id : 0;
    }

    private static string GenerateTempPassword(int length = 12)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        var all = upper + lower + digits + symbols;

        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        string Pick(string s)
        {
            var b = new byte[4];
            rng.GetBytes(b);
            var idx = BitConverter.ToUInt32(b, 0) % (uint)s.Length;
            return s[(int)idx].ToString();
        }

        var chars = new List<char>
        {
            Pick(upper)[0], Pick(lower)[0], Pick(digits)[0], Pick(symbols)[0]
        };
        while (chars.Count < length)
            chars.Add(Pick(all)[0]);

        for (int i = chars.Count - 1; i > 0; i--)
        {
            var b = new byte[4];
            rng.GetBytes(b);
            var j = (int)(BitConverter.ToUInt32(b, 0) % (uint)(i + 1));
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }
}
