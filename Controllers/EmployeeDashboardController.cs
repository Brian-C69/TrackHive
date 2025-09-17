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
    private const string LeaveMessageKey = "LeaveMessage";
    private const string LeaveErrorKey   = "LeaveError";

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
                    Reason        = r.Reason,
                    CreatedAt     = r.CreatedAt,
                    ReviewedAt    = r.ReviewedAt,
                    ReviewedByName= r.ReviewedBy?.Name
                })
                .ToList(),
            LeaveApplication = new ApplyLeaveViewModel
            {
                StartDate = today,
                EndDate   = today
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
    public async Task<IActionResult> ApplyLeave(ApplyLeaveViewModel model)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return RedirectToAction("Login", "Auth");

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
            Status    = LeaveRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.LeaveRequests.Add(request);

        try
        {
            await _db.SaveChangesAsync();
            TempData[LeaveMessageKey] = $"Leave request submitted for {totalDays} day(s).";
        }
        catch (DbUpdateException)
        {
            TempData[LeaveErrorKey] = "We couldn't submit your leave request. Please try again.";
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
}
