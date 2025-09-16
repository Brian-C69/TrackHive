using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;

namespace TrackHive.Controllers;

[Authorize(Roles = "HR")]
public sealed class HrDashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    public HrDashboardController(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction("Login", "Auth");
        if (user.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var org = await _db.Organizations.FindAsync(user.OrganizationId);
        ViewData["OrgName"] = org?.Name ?? "Organization";
        return View(new InviteEmployeeViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteEmployee(InviteEmployeeViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction("Login", "Auth");
        if (user.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var org = await _db.Organizations.FindAsync(user.OrganizationId);
        if (!ModelState.IsValid || org is null)
        {
            if (org is null) model.ErrorMessage = "Organization not found.";
            ViewData["OrgName"] = org?.Name ?? "Organization";
            return View("Index", model);
        }

        var emailLower = model.Email.Trim().ToLower();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
        if (exists)
        {
            ModelState.AddModelError(nameof(InviteEmployeeViewModel.Email), "This email is already registered.");
            ViewData["OrgName"] = org.Name;
            return View("Index", model);
        }

        var tempPassword = GenerateTempPassword();

        var employee = new AppUser
        {
            Name = model.Name.Trim(),
            Email = model.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(tempPassword),
            Role = RoleType.Employee,
            OrganizationId = org.Id,
            MustChangePassword = true,
            IsActive = true
        };

        _db.Users.Add(employee);
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var loginUrl = $"{baseUrl}/Auth/Login";
        var subject = $"You're invited to {org.Name} (TrackHive)";
        var body = $@"
<p>Hi {System.Net.WebUtility.HtmlEncode(employee.Name)},</p>
<p>You have been invited as <strong>Employee</strong> to <strong>{System.Net.WebUtility.HtmlEncode(org.Name)}</strong> on TrackHive.</p>
<p><strong>Login:</strong> <a href=""{loginUrl}"">{loginUrl}</a><br/>
<strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(employee.Email)}<br/>
<strong>Temporary Password:</strong> {System.Net.WebUtility.HtmlEncode(tempPassword)}</p>
<p>Please change this password after your first login.</p>
<p>— TrackHive</p>";

        var (ok, error) = await _email.SendAsync(employee.Email, subject, body);

        ViewData["OrgName"] = org.Name;
        if (!ok)
        {
            model.ErrorMessage = $"Employee created, but email failed: {error}";
            return View("Index", model);
        }

        ModelState.Clear();
        return View("Index", new InviteEmployeeViewModel
        {
            SuccessMessage = $"Invited employee '{employee.Name}' at {employee.Email}."
        });
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return null;
        return await _db.Users.FindAsync(id);
    }

    private static string GenerateTempPassword(int length = 12)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        var all = upper + lower + digits + symbols;

        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        char Pick(string s)
        {
            var b = new byte[4]; rng.GetBytes(b);
            var idx = (int)(BitConverter.ToUInt32(b, 0) % (uint)s.Length);
            return s[idx];
        }
        var chars = new List<char> { Pick(upper), Pick(lower), Pick(digits), Pick(symbols) };
        while (chars.Count < length) chars.Add(Pick(all));
        for (int i = chars.Count - 1; i > 0; i--)
        {
            var b = new byte[4]; rng.GetBytes(b);
            var j = (int)(BitConverter.ToUInt32(b, 0) % (uint)(i + 1));
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }
}