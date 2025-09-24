using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;
using TrackHive.Services;

namespace TrackHive.Controllers;

[Authorize(Roles = "HR")]
public sealed class HrDashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly SubscriptionUsageService _subscriptionUsage;
    private const string LeaveActionMessageKey = "LeaveActionMessage";
    private const string LeaveActionErrorKey   = "LeaveActionError";
    private const string CertificateActionMessageKey = "CertificateActionMessage";
    private const string CertificateActionErrorKey   = "CertificateActionError";
    private static string FormatLeaveType(LeaveType type) => type switch
    {
        LeaveType.Annual    => "Annual leave",
        LeaveType.Sick      => "Sick leave",
        LeaveType.Emergency => "Emergency leave",
        LeaveType.Unpaid    => "Unpaid leave",
        _                   => "Other leave"
    };
    public HrDashboardController(AppDbContext db, EmailService email, SubscriptionUsageService subscriptionUsage)
    {
        _db = db;
        _email = email;
        _subscriptionUsage = subscriptionUsage;
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

        var limitCheck = await _subscriptionUsage.CheckCanAddUserAsync(org.Id, RoleType.Employee, HttpContext.RequestAborted);
        if (!limitCheck.CanAdd)
        {
            var message = limitCheck.BlockReason
                ?? "Invite blocked: your plan has reached the employee seat limit. Visit Billing to upgrade.";
            model.ErrorMessage = message;
            TempData["UpgradePrompt"] = message;
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
            TempData[LeaveActionErrorKey] = "Leave request not found.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Status != LeaveRequestStatus.Pending)
        {
            TempData[LeaveActionErrorKey] = "This leave request has already been processed.";
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
            TempData[LeaveActionErrorKey] = $"{request.User.Name} has exceeded their entitlement.";
            return RedirectToAction(nameof(Index));
        }

        var now = DateTimeOffset.UtcNow;
        request.ReviewedAt = now;
        request.ReviewedById = hr.Id;

        var requiresCertificate = request.Type.RequiresMedicalCertificate();
        request.Status = requiresCertificate
            ? LeaveRequestStatus.ApprovedAwaitingCertificate
            : LeaveRequestStatus.Approved;

        balance.UsedDays += request.TotalDays;
        balance.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            var message = requiresCertificate
                ? $"Approved {request.User.Name}'s leave ({request.TotalDays} day(s)). Awaiting medical certificate."
                : $"Approved {request.User.Name}'s leave ({request.TotalDays} day(s)).";
            TempData[LeaveActionMessageKey] = message;

            var subject = "Leave request approved";
            var intro = "Good news! Your leave request has been approved.";
            var additional = requiresCertificate
                ? "Please upload your medical certificate so HR can complete the approval."
                : null;

            var (emailOk, emailError) = await SendEmployeeLeaveEmailAsync(request, subject, intro, additional);
            if (!emailOk)
            {
                var errorMessage = string.IsNullOrWhiteSpace(emailError)
                    ? "Leave approved, but the email notification could not be sent."
                    : $"Leave approved, but the email notification could not be sent: {emailError}";
                TempData[LeaveActionErrorKey] = errorMessage;
            }
        }
        catch (DbUpdateException)
        {
            TempData[LeaveActionErrorKey] = "We couldn't approve this request. Please try again.";
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
            TempData[LeaveActionErrorKey] = "Leave request not found.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Status != LeaveRequestStatus.Pending)
        {
            TempData[LeaveActionErrorKey] = "This leave request has already been processed.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = LeaveRequestStatus.Rejected;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedById = hr.Id;

        try
        {
            await _db.SaveChangesAsync();
            TempData[LeaveActionMessageKey] = $"Rejected {request.User.Name}'s leave request.";

            var (emailOk, emailError) = await SendEmployeeLeaveEmailAsync(
                request,
                "Leave request rejected",
                "We're sorry to let you know that your leave request was rejected.");

            if (!emailOk)
            {
                var errorMessage = string.IsNullOrWhiteSpace(emailError)
                    ? "Leave rejected, but the email notification could not be sent."
                    : $"Leave rejected, but the email notification could not be sent: {emailError}";
                TempData[LeaveActionErrorKey] = errorMessage;
            }
        }
        catch (DbUpdateException)
        {
            TempData[LeaveActionErrorKey] = "We couldn't reject this request. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCertificate(int id)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var request = await _db.LeaveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request is null || request.User is null || request.User.OrganizationId != hr.OrganizationId)
        {
            TempData[CertificateActionErrorKey] = "Leave request not found.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Status != LeaveRequestStatus.AwaitingCertificateReview)
        {
            TempData[CertificateActionErrorKey] = "This request is not awaiting certificate review.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = LeaveRequestStatus.Approved;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedById = hr.Id;

        try
        {
            await _db.SaveChangesAsync();
            TempData[CertificateActionMessageKey] = $"Approved medical certificate for {request.User.Name}.";

            var (emailOk, emailError) = await SendEmployeeLeaveEmailAsync(
                request,
                "Medical certificate approved",
                "Your medical certificate has been reviewed and your leave is fully approved.");

            if (!emailOk)
            {
                var errorMessage = string.IsNullOrWhiteSpace(emailError)
                    ? "Certificate approved, but the email notification could not be sent."
                    : $"Certificate approved, but the email notification could not be sent: {emailError}";
                TempData[CertificateActionErrorKey] = errorMessage;
            }
        }
        catch (DbUpdateException)
        {
            TempData[CertificateActionErrorKey] = "We couldn't approve this certificate. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCertificate(int id)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var request = await _db.LeaveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request is null || request.User is null || request.User.OrganizationId != hr.OrganizationId)
        {
            TempData[CertificateActionErrorKey] = "Leave request not found.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Status != LeaveRequestStatus.AwaitingCertificateReview)
        {
            TempData[CertificateActionErrorKey] = "This request is not awaiting certificate review.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = LeaveRequestStatus.CertificateRejected;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.ReviewedById = hr.Id;

        try
        {
            await _db.SaveChangesAsync();
            TempData[CertificateActionMessageKey] = $"Requested a new medical certificate from {request.User.Name}.";

            var (emailOk, emailError) = await SendEmployeeLeaveEmailAsync(
                request,
                "Medical certificate requires attention",
                "We reviewed your medical certificate but couldn't approve it.",
                "Please upload a new document so we can complete the approval.");

            if (!emailOk)
            {
                var errorMessage = string.IsNullOrWhiteSpace(emailError)
                    ? "Certificate update saved, but the email notification could not be sent."
                    : $"Certificate update saved, but the email notification could not be sent: {emailError}";
                TempData[CertificateActionErrorKey] = errorMessage;
            }
        }
        catch (DbUpdateException)
        {
            TempData[CertificateActionErrorKey] = "We couldn't update this certificate review. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return null;
        return await _db.Users.FindAsync(id);
    }

    private async Task<(bool ok, string? error)> SendEmployeeLeaveEmailAsync(
        LeaveRequest request,
        string subject,
        string introParagraph,
        string? additionalParagraph = null)
    {
        var employee = request.User ?? await _db.Users.FindAsync(request.UserId);
        if (employee is null || string.IsNullOrWhiteSpace(employee.Email))
        {
            return (false, "Employee email address could not be determined.");
        }

        var typeLabel = FormatLeaveType(request.Type);
        var reasonHtml = string.IsNullOrWhiteSpace(request.Reason)
            ? "<em>No reason provided.</em>"
            : System.Net.WebUtility.HtmlEncode(request.Reason);

        var builder = new StringBuilder();
        builder.Append($"<p>Hi {System.Net.WebUtility.HtmlEncode(employee.Name)},</p>");
        builder.Append($"<p>{introParagraph}</p>");

        if (!string.IsNullOrWhiteSpace(additionalParagraph))
        {
            builder.Append($"<p>{additionalParagraph}</p>");
        }

        builder.Append("<p><strong>Leave details:</strong><br/>");
        builder.Append($"Type: {System.Net.WebUtility.HtmlEncode(typeLabel)}<br/>");
        builder.Append($"Dates: {request.StartDate:MMM d, yyyy} – {request.EndDate:MMM d, yyyy}<br/>");
        builder.Append($"Total days: {request.TotalDays}<br/>");
        builder.Append($"Reason: {reasonHtml}</p>");

        var dashboardUrl = Url.Action("Index", "EmployeeDashboard", values: null, protocol: Request.Scheme, host: Request.Host.Value);
        if (!string.IsNullOrWhiteSpace(dashboardUrl))
        {
            builder.Append($"<p>View your leave requests: <a href=\"{dashboardUrl}\">{dashboardUrl}</a></p>");
        }

        builder.Append("<p>— TrackHive</p>");

        return await _email.SendAsync(employee.Email, subject, builder.ToString());
    }

    private async Task<HrDashboardViewModel> BuildDashboardViewModelAsync(AppUser hr, InviteEmployeeViewModel? inviteOverride)
    {
        var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == hr.OrganizationId);
        var invite = inviteOverride ?? new InviteEmployeeViewModel();
        var plan = org?.Plan ?? OrganizationPlan.Free;
        var canViewAnalytics = PlanHelper.CanViewAnalytics(plan);

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

        var certificatePendingRequests = await _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Documents)
            .Where(r => employeeIds.Contains(r.UserId) && r.Status == LeaveRequestStatus.AwaitingCertificateReview)
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.EndDate)
            .ToListAsync();

        var awaitingEmployeeCertificates = await _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => employeeIds.Contains(r.UserId) && r.Status == LeaveRequestStatus.ApprovedAwaitingCertificate)
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

        var certificateViewModels = certificatePendingRequests
            .Select(r =>
            {
                var documents = r.Documents
                    .OrderBy(d => d.UploadedAt)
                    .Select(d => new LeaveDocumentViewModel
                    {
                        Id = d.Id,
                        FileName = d.OriginalFileName,
                        UploadedAt = d.UploadedAt,
                        DownloadAction = Url.Action("Download", "LeaveDocuments", new { id = d.Id }) ?? string.Empty
                    })
                    .ToList();

                var submittedAt = documents.Count > 0
                    ? documents.Max(d => d.UploadedAt)
                    : r.CreatedAt;

                return new LeaveCertificateReviewViewModel
                {
                    RequestId = r.Id,
                    EmployeeName = r.User?.Name ?? "Employee",
                    Type = r.Type,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    TotalDays = r.TotalDays,
                    Reason = r.Reason,
                    SubmittedAt = submittedAt,
                    Documents = documents
                };
            })
            .ToList();

        var notifications = new List<DashboardNotificationViewModel>();
        var employeeNameLookup = employees.ToDictionary(e => e.Id, e => e.Name);

        if (pendingViewModels.Count > 0)
        {
            var oldestPending = pendingRequests.Min(r => r.CreatedAt);
            notifications.Add(new DashboardNotificationViewModel
            {
                Category = "Leave",
                Title = "Pending leave approvals",
                Message = $"You have {pendingViewModels.Count} leave request(s) waiting for review.",
                CreatedAt = oldestPending
            });
        }

        if (certificateViewModels.Count > 0)
        {
            var latestSubmission = certificatePendingRequests
                .SelectMany(r => r.Documents.Select(d => d.UploadedAt))
                .DefaultIfEmpty(DateTimeOffset.UtcNow)
                .Max();

            notifications.Add(new DashboardNotificationViewModel
            {
                Category = "Leave",
                Title = "Certificates pending review",
                Message = $"You have {certificateViewModels.Count} medical certificate(s) awaiting approval.",
                CreatedAt = latestSubmission
            });
        }

        if (awaitingEmployeeCertificates.Count > 0)
        {
            var mostRecentApproval = awaitingEmployeeCertificates
                .Select(r => r.ReviewedAt ?? r.CreatedAt)
                .DefaultIfEmpty(DateTimeOffset.UtcNow)
                .Max();

            notifications.Add(new DashboardNotificationViewModel
            {
                Category = "Leave",
                Title = "Waiting on medical certificates",
                Message = $"{awaitingEmployeeCertificates.Count} approved leave(s) still need employee documents.",
                CreatedAt = mostRecentApproval
            });
        }

        if (employeeIds.Count > 0)
        {
            var statusHistoryCutoff = DateTimeOffset.UtcNow.AddDays(-7);
            var recentDecisions = await _db.LeaveRequests
                .AsNoTracking()
                .Include(r => r.User)
                .Where(r => employeeIds.Contains(r.UserId)
                    && (r.Status == LeaveRequestStatus.Approved
                        || r.Status == LeaveRequestStatus.Rejected
                        || r.Status == LeaveRequestStatus.CertificateRejected)
                    && r.ReviewedAt != null
                    && r.ReviewedAt >= statusHistoryCutoff)
                .OrderByDescending(r => r.ReviewedAt)
                .Take(5)
                .ToListAsync();

            foreach (var decision in recentDecisions)
            {
                var status = decision.Status switch
                {
                    LeaveRequestStatus.Approved => "approved",
                    LeaveRequestStatus.CertificateRejected => "certificate rejected",
                    _ => "rejected"
                };
                var reviewedAt = decision.ReviewedAt!.Value;
                var employeeName = decision.User?.Name ?? "Employee";
                var message = decision.Status == LeaveRequestStatus.CertificateRejected
                    ? $"{employeeName}'s medical certificate was rejected on {reviewedAt.ToLocalTime():MMM d}."
                    : $"{employeeName}'s {decision.TotalDays} day(s) of leave were {status} on {reviewedAt.ToLocalTime():MMM d}.";

                notifications.Add(new DashboardNotificationViewModel
                {
                    Category = "Leave",
                    Title = $"{employeeName}'s leave {status}",
                    Message = message,
                    CreatedAt = reviewedAt
                });
            }

            var lateThreshold = new TimeOnly(9, 30).ToTimeSpan();
            var attendanceWindowStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6));
            var recentAttendance = await _db.AttendanceRecords
                .AsNoTracking()
                .Where(a => employeeIds.Contains(a.UserId)
                    && a.Date >= attendanceWindowStart
                    && a.CheckInTime != null)
                .Select(a => new { a.UserId, a.Date, a.CheckInTime })
                .ToListAsync();

            static bool IsLate(DateTimeOffset checkInLocal, TimeSpan threshold) => checkInLocal.TimeOfDay > threshold;

            var lateArrivals = recentAttendance
                .Select(a => new
                {
                    a.UserId,
                    a.Date,
                    CheckInLocal = a.CheckInTime!.Value.ToLocalTime()
                })
                .Where(a => IsLate(a.CheckInLocal, lateThreshold))
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.CheckInLocal)
                .Take(5)
                .ToList();

            foreach (var record in lateArrivals)
            {
                if (!employeeNameLookup.TryGetValue(record.UserId, out var employeeName))
                {
                    employeeName = "Employee";
                }

                var checkInLocal = record.CheckInLocal.ToLocalTime();
                notifications.Add(new DashboardNotificationViewModel
                {
                    Category = "Attendance",
                    Title = "Late arrival alert",
                    Message = $"{employeeName} checked in at {checkInLocal:t} on {record.Date:MMM d}.",
                    CreatedAt = record.CheckInLocal
                });
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var monthAttendance = await _db.AttendanceRecords
                .AsNoTracking()
                .Where(a => employeeIds.Contains(a.UserId)
                    && a.Date >= monthStart
                    && a.Date < nextMonth)
                .Select(a => new { a.UserId, a.Date, a.CheckInTime })
                .ToListAsync();

            if (monthAttendance.Count > 0)
            {
                var totalCheckIns = monthAttendance.Count(a => a.CheckInTime != null);
                var employeesActive = monthAttendance.Select(a => a.UserId).Distinct().Count();
                var lateCount = monthAttendance
                    .Where(a => a.CheckInTime != null)
                    .Select(a => a.CheckInTime!.Value.ToLocalTime())
                    .Count(local => IsLate(local, lateThreshold));

                notifications.Add(new DashboardNotificationViewModel
                {
                    Category = "Reports",
                    Title = $"{monthStart:MMMM yyyy} attendance summary",
                    Message = $"{totalCheckIns} check-in(s) recorded across {employeesActive} employee(s) with {lateCount} late arrival(s).",
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        var orderedNotifications = notifications
            .OrderByDescending(n => n.CreatedAt ?? DateTimeOffset.MinValue)
            .Take(10)
            .ToList();

        var leavesReviewedThisMonth = 0;
        IReadOnlyList<MonthlyLeaveTrendViewModel> monthlyTrends = Array.Empty<MonthlyLeaveTrendViewModel>();
        IReadOnlyList<LeaveTypeBreakdownViewModel> leaveTypeBreakdown = Array.Empty<LeaveTypeBreakdownViewModel>();

        if (canViewAnalytics)
        {
            var currentMonthUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthRange = Enumerable.Range(0, 6)
                .Select(offset => currentMonthUtc.AddMonths(offset - 5))
                .Select(date => new
                {
                    Date = date,
                    Label = date.ToString("MMM yyyy", CultureInfo.InvariantCulture)
                })
                .ToList();

            var trendList = new List<MonthlyLeaveTrendViewModel>();
            var breakdownList = new List<LeaveTypeBreakdownViewModel>();

            if (employeeIds.Count > 0)
            {
                var earliestMonth = monthRange.First().Date;
                var leaveHistory = await _db.LeaveRequests
                    .AsNoTracking()
                    .Where(r => employeeIds.Contains(r.UserId) && r.CreatedAt >= earliestMonth)
                    .Select(r => new { r.CreatedAt, r.Status })
                    .ToListAsync();

                var monthlyLookup = leaveHistory
                    .GroupBy(r => new DateTime(r.CreatedAt.Year, r.CreatedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc))
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var pending = g.Count(x => x.Status == LeaveRequestStatus.Pending || x.Status == LeaveRequestStatus.AwaitingCertificateReview);
                            var approved = g.Count(x => x.Status == LeaveRequestStatus.Approved || x.Status == LeaveRequestStatus.ApprovedAwaitingCertificate);
                            var rejected = g.Count(x => x.Status == LeaveRequestStatus.Rejected || x.Status == LeaveRequestStatus.CertificateRejected);
                            return (Pending: pending, Approved: approved, Rejected: rejected);
                        });

                foreach (var info in monthRange)
                {
                    if (monthlyLookup.TryGetValue(info.Date, out var counts))
                    {
                        trendList.Add(new MonthlyLeaveTrendViewModel
                        {
                            MonthLabel = info.Label,
                            Pending = counts.Pending,
                            Approved = counts.Approved,
                            Rejected = counts.Rejected
                        });

                        if (info.Date == currentMonthUtc)
                        {
                            leavesReviewedThisMonth = counts.Approved + counts.Rejected;
                        }
                    }
                    else
                    {
                        trendList.Add(new MonthlyLeaveTrendViewModel
                        {
                            MonthLabel = info.Label,
                            Pending = 0,
                            Approved = 0,
                            Rejected = 0
                        });
                    }
                }

                var typeCounts = await _db.LeaveRequests
                    .AsNoTracking()
                    .Where(r => employeeIds.Contains(r.UserId))
                    .GroupBy(r => r.Type)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                breakdownList = typeCounts
                    .OrderByDescending(t => t.Count)
                    .Select(t => new LeaveTypeBreakdownViewModel
                    {
                        Type = FormatLeaveType(t.Type),
                        Count = t.Count
                    })
                    .ToList();
            }

            if (trendList.Count == 0)
            {
                trendList = monthRange
                    .Select(info => new MonthlyLeaveTrendViewModel
                    {
                        MonthLabel = info.Label,
                        Pending = 0,
                        Approved = 0,
                        Rejected = 0
                    })
                    .ToList();
            }

            monthlyTrends = trendList;
            leaveTypeBreakdown = breakdownList;
        }

        var metrics = new DashboardMetricsViewModel
        {
            TotalEmployees = employees.Count,
            PendingLeaveApprovals = pendingViewModels.Count,
            PendingCertificateReviews = certificateViewModels.Count,
            AwaitingEmployeeCertificates = awaitingEmployeeCertificates.Count,
            LeavesReviewedThisMonth = leavesReviewedThisMonth,
            LeaveTrends = monthlyTrends,
            LeaveTypeBreakdown = leaveTypeBreakdown
        };

        return new HrDashboardViewModel
        {
            OrganizationName = org?.Name ?? "Organization",
            Invite = invite,
            PendingLeaveRequests = pendingViewModels,
            PendingCertificateRequests = certificateViewModels,
            LeaveSummaries = leaveSummaries,
            Notifications = orderedNotifications,
            Metrics = metrics,
            Plan = plan,
            CanViewAnalytics = canViewAnalytics
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
