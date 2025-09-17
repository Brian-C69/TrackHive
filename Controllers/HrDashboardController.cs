using System;
using System.Linq;
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

        var viewModel = await BuildDashboardViewModelAsync(user, new InviteEmployeeViewModel());
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteEmployee([Bind(Prefix = "Invite")] InviteEmployeeViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction("Login", "Auth");
        if (user.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var org = await _db.Organizations.FindAsync(user.OrganizationId);
        if (!ModelState.IsValid || org is null)
        {
            if (org is null)
            {
                ModelState.AddModelError(string.Empty, "Organization not found.");
                model.ErrorMessage = "Organization not found.";
            }

            return View("Index", await BuildDashboardViewModelAsync(user, model));
        }

        var emailLower = model.Email.Trim().ToLower();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
        if (exists)
        {
            ModelState.AddModelError("Invite." + nameof(InviteEmployeeViewModel.Email), "This email is already registered.");
            return View("Index", await BuildDashboardViewModelAsync(user, model));
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
            IsActive = true,
            MonthlySalary = model.MonthlySalary
        };

        _db.Users.Add(employee);
        await _db.SaveChangesAsync();

        var balance = new LeaveBalance
        {
            UserId = employee.Id,
            AnnualEntitlement = LeaveBalance.DefaultAnnualEntitlement,
            UsedDays = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.LeaveBalances.Add(balance);
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

        if (!ok)
        {
            model.ErrorMessage = $"Employee created, but email failed: {error}";
            return View("Index", await BuildDashboardViewModelAsync(user, model));
        }

        ModelState.Clear();
        return View("Index", await BuildDashboardViewModelAsync(user, new InviteEmployeeViewModel
        {
            SuccessMessage = $"Invited employee '{employee.Name}' at {employee.Email}."
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveLeave(int id)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var request = await _db.LeaveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request is null || request.User is null || request.User.OrganizationId != hr.OrganizationId)
        {
            TempData["LeaveActionError"] = "Leave request not found.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Status != LeaveRequestStatus.Pending)
        {
            TempData["LeaveActionError"] = "This leave request has already been processed.";
            return RedirectToAction(nameof(Index));
        }

        var balance = await _db.LeaveBalances.FirstOrDefaultAsync(b => b.UserId == request.UserId);
        if (balance is null)
        {
            balance = new LeaveBalance
            {
                UserId = request.UserId,
                AnnualEntitlement = LeaveBalance.DefaultAnnualEntitlement,
                UsedDays = 0,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.LeaveBalances.Add(balance);
        }

        var pendingDays = await _db.LeaveRequests
            .Where(r => r.UserId == request.UserId && r.Status == LeaveRequestStatus.Pending)
            .SumAsync(r => r.TotalDays);

        var available = balance.AnnualEntitlement - balance.UsedDays - pendingDays;
        if (available < 0)
        {
            TempData["LeaveActionError"] = $"{request.User.Name} has exceeded their entitlement.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = LeaveRequestStatus.Approved;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedById = hr.Id;

        balance.UsedDays += request.TotalDays;
        balance.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            TempData["LeaveActionMessage"] = $"Approved {request.User.Name}'s leave ({request.TotalDays} day(s)).";
        }
        catch (DbUpdateException)
        {
            TempData["LeaveActionError"] = "We couldn't approve this request. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectLeave(int id)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var request = await _db.LeaveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request is null || request.User is null || request.User.OrganizationId != hr.OrganizationId)
        {
            TempData["LeaveActionError"] = "Leave request not found.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Status != LeaveRequestStatus.Pending)
        {
            TempData["LeaveActionError"] = "This leave request has already been processed.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = LeaveRequestStatus.Rejected;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedById = hr.Id;

        try
        {
            await _db.SaveChangesAsync();
            TempData["LeaveActionMessage"] = $"Rejected {request.User.Name}'s leave request.";
        }
        catch (DbUpdateException)
        {
            TempData["LeaveActionError"] = "We couldn't reject this request. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return null;
        return await _db.Users.FindAsync(id);
    }

    private async Task<HrDashboardViewModel> BuildDashboardViewModelAsync(AppUser hr, InviteEmployeeViewModel? inviteOverride)
    {
        var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == hr.OrganizationId);
        var invite = inviteOverride ?? new InviteEmployeeViewModel();

        var employees = await _db.Users
            .AsNoTracking()
            .Where(u => u.OrganizationId == hr.OrganizationId && u.Role == RoleType.Employee)
            .Select(u => new { u.Id, u.Name })
            .OrderBy(u => u.Name)
            .ToListAsync();

        var employeeIds = employees.Select(e => e.Id).ToList();

        var balances = await _db.LeaveBalances
            .AsNoTracking()
            .Where(b => employeeIds.Contains(b.UserId))
            .ToDictionaryAsync(b => b.UserId);

        var pendingRequests = await _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => employeeIds.Contains(r.UserId) && r.Status == LeaveRequestStatus.Pending)
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.EndDate)
            .ToListAsync();

        var pendingTotals = pendingRequests
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalDays));

        var leaveSummaries = employees
            .Select(e =>
            {
                balances.TryGetValue(e.Id, out var balance);
                var annual = balance?.AnnualEntitlement ?? LeaveBalance.DefaultAnnualEntitlement;
                var used = balance?.UsedDays ?? 0;
                pendingTotals.TryGetValue(e.Id, out var pending);
                return new LeaveBalanceSummaryViewModel
                {
                    EmployeeName = e.Name,
                    AnnualEntitlement = annual,
                    UsedDays = used,
                    PendingDays = pending
                };
            })
            .OrderBy(s => s.EmployeeName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pendingViewModels = pendingRequests
            .Select(r =>
            {
                balances.TryGetValue(r.UserId, out var balance);
                var annual = balance?.AnnualEntitlement ?? LeaveBalance.DefaultAnnualEntitlement;
                var used = balance?.UsedDays ?? 0;
                pendingTotals.TryGetValue(r.UserId, out var pending);
                return new LeaveRequestReviewViewModel
                {
                    RequestId = r.Id,
                    EmployeeName = r.User?.Name ?? "Employee",
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    TotalDays = r.TotalDays,
                    Reason = r.Reason,
                    RequestedAt = r.CreatedAt,
                    AnnualEntitlement = annual,
                    AvailableDays = Math.Max(0, annual - used - pending)
                };
            })
            .ToList();

        return new HrDashboardViewModel
        {
            OrganizationName = org?.Name ?? "Organization",
            Invite = invite,
            PendingLeaveRequests = pendingViewModels,
            LeaveSummaries = leaveSummaries
        };
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
