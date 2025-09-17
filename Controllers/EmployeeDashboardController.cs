using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;

namespace TrackHive.Controllers;

[Authorize(Roles = "Employee")]
public sealed class EmployeeDashboardController : Controller
{
    private readonly AppDbContext _db;
    private const string LeaveMessageKey = "LeaveMessage";
    private const string LeaveErrorKey   = "LeaveError";
    private const string CertificateMessageKey = "CertificateMessage";
    private const string CertificateErrorKey   = "CertificateError";
    private const long MaxCertificateFileBytes = 5 * 1024 * 1024; // 5 MB per file
    private static readonly string[] AllowedCertificateExtensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".pdf", ".heic", ".heif"];
    private readonly IWebHostEnvironment _environment;
    private readonly EmailService _email;

    public EmployeeDashboardController(AppDbContext db, IWebHostEnvironment environment, EmailService email)
    {
        _db = db;
        _environment = environment;
        _email = email;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Global filter will redirect if MustChangePassword == true
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return RedirectToAction("Login", "Auth");

        var user = await _db.Users.FindAsync(id);
        if (user is null) return RedirectToAction("Login", "Auth");

        var org = await _db.Organizations.FindAsync(user.OrganizationId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var todayRecord = await _db.AttendanceRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == id && a.Date == today);

        var recentRecords = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.Date)
            .Take(14)
            .ToListAsync();

        // --- Leave: balance + requests
        var leaveBalance = await _db.LeaveBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == id);

        var leaveRequests = await _db.LeaveRequests
            .AsNoTracking()
            .Where(r => r.UserId == id)
            .Include(r => r.ReviewedBy)
            .Include(r => r.Documents)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();

        var pendingLeaveDays = leaveRequests
            .Where(r => r.Status == LeaveRequestStatus.Pending)
            .Sum(r => r.TotalDays);

        var viewModel = new EmployeeAttendanceViewModel
        {
            OrganizationName = org?.Name ?? "Organization",
            UserName = user.Name,
            Today = todayRecord is null
                ? null
                : new AttendanceDayViewModel
                {
                    Date = todayRecord.Date,
                    CheckInTime = todayRecord.CheckInTime,
                    CheckOutTime = todayRecord.CheckOutTime
                },
            RecentRecords = recentRecords
                .Select(r => new AttendanceDayViewModel
                {
                    Date = r.Date,
                    CheckInTime = r.CheckInTime,
                    CheckOutTime = r.CheckOutTime
                })
                .ToList(),
            CanCheckIn  = todayRecord is null || !todayRecord.CheckInTime.HasValue,
            CanCheckOut = todayRecord is not null && todayRecord.CheckInTime.HasValue && !todayRecord.CheckOutTime.HasValue,

            // --- Leave section for the dashboard
            LeaveBalance = new LeaveBalanceViewModel
            {
                AnnualEntitlement = leaveBalance?.AnnualEntitlement ?? LeaveBalance.DefaultAnnualEntitlement,
                UsedDays          = leaveBalance?.UsedDays ?? 0,
                PendingDays       = pendingLeaveDays
            },
            LeaveRequests = leaveRequests
                .Select(r => new LeaveRequestListItemViewModel
                {
                    Id            = r.Id,
                    StartDate     = r.StartDate,
                    EndDate       = r.EndDate,
                    TotalDays     = r.TotalDays,
                    Status        = r.Status,
                    Type          = r.Type,
                    Reason        = r.Reason,
                    CreatedAt     = r.CreatedAt,
                    ReviewedAt    = r.ReviewedAt,
                    ReviewedByName= r.ReviewedBy?.Name,
                    Documents     = r.Documents
                        .OrderBy(d => d.UploadedAt)
                        .Select(d => new LeaveDocumentViewModel
                        {
                            Id = d.Id,
                            FileName = d.OriginalFileName,
                            UploadedAt = d.UploadedAt,
                            DownloadAction = Url.Action("Download", "LeaveDocuments", new { id = d.Id }) ?? string.Empty
                        })
                        .ToList(),
                    RequiresMedicalCertificate = r.Type.RequiresMedicalCertificate()
                })
                .ToList(),
            LeaveApplication = new ApplyLeaveViewModel
            {
                StartDate = today,
                EndDate   = today,
                LeaveType = LeaveType.Annual
            }
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return RedirectToAction("Login", "Auth");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now   = DateTimeOffset.UtcNow;

        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(a => a.UserId == id && a.Date == today);

        if (record is not null && record.CheckInTime.HasValue)
        {
            TempData["AttendanceError"] = "You have already checked in for today.";
            return RedirectToAction(nameof(Index));
        }

        if (record is null)
        {
            record = new AttendanceRecord
            {
                UserId = id,
                Date = today,
                CheckInTime = now
            };
            _db.AttendanceRecords.Add(record);
        }
        else
        {
            record.CheckInTime = now;
        }

        try
        {
            await _db.SaveChangesAsync();
            TempData["AttendanceMessage"] = $"Check-in recorded at {now.ToLocalTime():g}.";
        }
        catch (DbUpdateException)
        {
            TempData["AttendanceError"] = "We couldn't record your check-in. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    // --- Leave application
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyLeave([Bind(Prefix = nameof(EmployeeAttendanceViewModel.LeaveApplication))] ApplyLeaveViewModel model)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return RedirectToAction("Login", "Auth");

        var employee = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (employee is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (model.StartDate is null || model.EndDate is null)
        {
            TempData[LeaveErrorKey] = "Start and end dates are required.";
            return RedirectToAction(nameof(Index));
        }

        var start = model.StartDate.Value;
        var end   = model.EndDate.Value;

        if (end < start)
        {
            TempData[LeaveErrorKey] = "End date cannot be before the start date.";
            return RedirectToAction(nameof(Index));
        }

        var totalDays = (end.DayNumber - start.DayNumber) + 1;
        if (totalDays <= 0)
        {
            TempData[LeaveErrorKey] = "The selected range is invalid.";
            return RedirectToAction(nameof(Index));
        }

        if (!Enum.IsDefined(typeof(LeaveType), model.LeaveType))
        {
            TempData[LeaveErrorKey] = "Please select a valid leave type.";
            return RedirectToAction(nameof(Index));
        }

        var balance = await _db.LeaveBalances.FirstOrDefaultAsync(l => l.UserId == id);
        if (balance is null)
        {
            balance = new LeaveBalance
            {
                UserId = id,
                AnnualEntitlement = LeaveBalance.DefaultAnnualEntitlement,
                UsedDays = 0,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.LeaveBalances.Add(balance);
        }

        var pendingDays = await _db.LeaveRequests
            .Where(r => r.UserId == id && r.Status == LeaveRequestStatus.Pending)
            .SumAsync(r => r.TotalDays);

        var available = balance.AnnualEntitlement - balance.UsedDays - pendingDays;
        if (available < totalDays)
        {
            TempData[LeaveErrorKey] = $"You only have {Math.Max(available, 0)} day(s) of leave remaining.";
            return RedirectToAction(nameof(Index));
        }

        balance.UpdatedAt = DateTimeOffset.UtcNow;

        var request = new LeaveRequest
        {
            UserId    = id,
            StartDate = start,
            EndDate   = end,
            TotalDays = totalDays,
            Reason    = string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim(),
            Type      = model.LeaveType,
            Status    = LeaveRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.LeaveRequests.Add(request);

        try
        {
            await _db.SaveChangesAsync();
            TempData[LeaveMessageKey] = $"Leave request submitted for {totalDays} day(s).";

            var hrError = await NotifyHrsAsync(employee.OrganizationId, hr =>
            {
                var subject = $"New leave request from {employee.Name}";
                var reviewUrl = Url.Action("Index", "HrDashboard", values: null, protocol: Request.Scheme, host: Request.Host.Value);
                var builder = new StringBuilder();
                builder.Append($"<p>Hi {System.Net.WebUtility.HtmlEncode(hr.Name)},</p>");
                builder.Append($"<p>{System.Net.WebUtility.HtmlEncode(employee.Name)} submitted a leave request.</p>");
                builder.Append("<p><strong>Leave details:</strong><br/>");
                builder.Append($"Type: {System.Net.WebUtility.HtmlEncode(FormatLeaveType(request.Type))}<br/>");
                builder.Append($"Dates: {request.StartDate:MMM d, yyyy} – {request.EndDate:MMM d, yyyy}<br/>");
                builder.Append($"Total days: {request.TotalDays}</p>");
                if (!string.IsNullOrWhiteSpace(request.Reason))
                {
                    builder.Append($"<p><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(request.Reason)}</p>");
                }

                if (!string.IsNullOrWhiteSpace(reviewUrl))
                {
                    builder.Append($"<p>Review in TrackHive: <a href=\"{reviewUrl}\">{reviewUrl}</a></p>");
                }

                builder.Append("<p>— TrackHive</p>");
                return (subject, builder.ToString());
            });

            if (!string.IsNullOrWhiteSpace(hrError))
            {
                TempData[LeaveErrorKey] = $"Leave request submitted, but we couldn't send notification email(s): {hrError}";
            }
        }
        catch (DbUpdateException)
        {
            TempData[LeaveErrorKey] = "We couldn't submit your leave request. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitMedicalCertificate(int requestId, List<IFormFile>? files)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return RedirectToAction("Login", "Auth");

        var request = await _db.LeaveRequests
            .Include(r => r.Documents)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId);

        if (request is null)
        {
            TempData[CertificateErrorKey] = "Leave request not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!request.Type.RequiresMedicalCertificate())
        {
            TempData[CertificateErrorKey] = "This leave does not require a medical certificate.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Status is not LeaveRequestStatus.ApprovedAwaitingCertificate and not LeaveRequestStatus.CertificateRejected)
        {
            TempData[CertificateErrorKey] = "You cannot submit documents for this request right now.";
            return RedirectToAction(nameof(Index));
        }

        var uploadFiles = files?
            .Where(f => f is not null && f.Length > 0)
            .ToList() ?? new List<IFormFile>();

        if (uploadFiles.Count == 0)
        {
            TempData[CertificateErrorKey] = "Please choose at least one file.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var file in uploadFiles)
        {
            if (file.Length > MaxCertificateFileBytes)
            {
                TempData[CertificateErrorKey] = "Each file must be 5 MB or smaller.";
                return RedirectToAction(nameof(Index));
            }

            if (!IsAllowedCertificateFile(file))
            {
                TempData[CertificateErrorKey] = "Only images or PDF files are allowed.";
                return RedirectToAction(nameof(Index));
            }
        }

        RemoveExistingDocuments(request);

        foreach (var file in uploadFiles)
        {
            var relativePath = await SaveCertificateFileAsync(request.Id, file);
            var document = new LeaveDocument
            {
                LeaveRequestId = request.Id,
                OriginalFileName = SanitizeOriginalFileName(file.FileName),
                StoredFilePath = relativePath,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType,
                UploadedAt = DateTimeOffset.UtcNow
            };

            _db.LeaveDocuments.Add(document);
        }

        request.Status = LeaveRequestStatus.AwaitingCertificateReview;

        try
        {
            await _db.SaveChangesAsync();
            TempData[CertificateMessageKey] = "Medical certificate submitted for HR review.";

            if (request.User is not null)
            {
                var hrError = await NotifyHrsAsync(request.User.OrganizationId, hr =>
                {
                    var subject = $"Medical certificate from {request.User.Name}";
                    var reviewUrl = Url.Action("Index", "HrDashboard", values: null, protocol: Request.Scheme, host: Request.Host.Value);
                    var builder = new StringBuilder();
                    builder.Append($"<p>Hi {System.Net.WebUtility.HtmlEncode(hr.Name)},</p>");
                    builder.Append($"<p>{System.Net.WebUtility.HtmlEncode(request.User.Name)} uploaded a medical certificate for their leave request.</p>");
                    builder.Append("<p><strong>Leave details:</strong><br/>");
                    builder.Append($"Type: {System.Net.WebUtility.HtmlEncode(FormatLeaveType(request.Type))}<br/>");
                    builder.Append($"Dates: {request.StartDate:MMM d, yyyy} – {request.EndDate:MMM d, yyyy}<br/>");
                    builder.Append($"Total days: {request.TotalDays}</p>");

                    if (!string.IsNullOrWhiteSpace(reviewUrl))
                    {
                        builder.Append($"<p>Review the documents: <a href=\"{reviewUrl}\">{reviewUrl}</a></p>");
                    }

                    builder.Append("<p>— TrackHive</p>");
                    return (subject, builder.ToString());
                });

                if (!string.IsNullOrWhiteSpace(hrError))
                {
                    TempData[CertificateErrorKey] = $"Medical certificate submitted, but we couldn't send notification email(s): {hrError}";
                }
            }
        }
        catch (DbUpdateException)
        {
            TempData[CertificateErrorKey] = "We couldn't save your documents. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return RedirectToAction("Login", "Auth");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now   = DateTimeOffset.UtcNow;

        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(a => a.UserId == id && a.Date == today);

        if (record is null || !record.CheckInTime.HasValue)
        {
            TempData["AttendanceError"] = "You need to check in before checking out.";
            return RedirectToAction(nameof(Index));
        }

        if (record.CheckOutTime.HasValue)
        {
            TempData["AttendanceError"] = "You have already checked out for today.";
            return RedirectToAction(nameof(Index));
        }

        record.CheckOutTime = now;

        try
        {
            await _db.SaveChangesAsync();
            TempData["AttendanceMessage"] = $"Check-out recorded at {now.ToLocalTime():g}.";
        }
        catch (DbUpdateException)
        {
            TempData["AttendanceError"] = "We couldn't record your check-out. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static bool IsAllowedCertificateFile(IFormFile file)
    {
        if (file.ContentType is string contentType)
        {
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension)) return false;

        return AllowedCertificateExtensions.Contains(extension.ToLowerInvariant());
    }

    private void RemoveExistingDocuments(LeaveRequest request)
    {
        if (request.Documents.Count == 0) return;

        var webRoot = GetWebRootPath();
        foreach (var document in request.Documents.ToList())
        {
            var physicalPath = Path.Combine(webRoot, document.StoredFilePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }
            catch
            {
                // ignore IO errors when cleaning up old files
            }

            _db.LeaveDocuments.Remove(document);
        }

        request.Documents.Clear();
    }

    private async Task<string> SaveCertificateFileAsync(int requestId, IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            extension = new string(extension.Take(10).ToArray());
        }
        else
        {
            extension = string.Empty;
        }

        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine("uploads", "leave-documents", requestId.ToString(), uniqueName)
            .Replace('\\', '/');

        var physicalPath = Path.Combine(GetWebRootPath(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = System.IO.File.Create(physicalPath);
        await file.CopyToAsync(stream);

        return relativePath;
    }

    private string GetWebRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_environment.WebRootPath))
        {
            return _environment.WebRootPath!;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string SanitizeOriginalFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "document";
        }

        var name = Path.GetFileName(fileName);
        if (name.Length <= 256)
        {
            return name;
        }

        return name[^256..];
    }

    private async Task<string?> NotifyHrsAsync(int organizationId, Func<AppUser, (string Subject, string Body)> messageFactory)
    {
        var hrUsers = await _db.Users
            .AsNoTracking()
            .Where(u => u.OrganizationId == organizationId && u.Role == RoleType.HR && u.IsActive)
            .ToListAsync();

        if (hrUsers.Count == 0)
        {
            return null;
        }

        var failures = new List<string>();

        foreach (var hr in hrUsers)
        {
            if (string.IsNullOrWhiteSpace(hr.Email))
            {
                continue;
            }

            var (subject, body) = messageFactory(hr);
            var (ok, error) = await _email.SendAsync(hr.Email, subject, body);
            if (!ok)
            {
                var detail = string.IsNullOrWhiteSpace(error) ? hr.Email : $"{hr.Email}: {error}";
                failures.Add(detail);
            }
        }

        return failures.Count == 0 ? null : string.Join("; ", failures);
    }

    private static string FormatLeaveType(LeaveType type) => type switch
    {
        LeaveType.Annual    => "Annual leave",
        LeaveType.Sick      => "Sick leave",
        LeaveType.Emergency => "Emergency leave",
        LeaveType.Unpaid    => "Unpaid leave",
        _                   => "Other leave"
    };
}
