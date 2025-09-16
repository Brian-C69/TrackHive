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
            CanCheckIn = todayRecord is null || !todayRecord.CheckInTime.HasValue,
            CanCheckOut = todayRecord is not null && todayRecord.CheckInTime.HasValue && !todayRecord.CheckOutTime.HasValue
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
        var now = DateTimeOffset.UtcNow;

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return RedirectToAction("Login", "Auth");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

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
