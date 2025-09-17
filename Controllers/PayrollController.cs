using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;

namespace TrackHive.Controllers;

[Authorize(Roles = "HR")]
public sealed class PayrollController : Controller
{
    private readonly AppDbContext _db;
    private const decimal StandardDailyHours = 8m;
    private const decimal DefaultOvertimeMultiplier = 1.5m;

    public PayrollController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(int? employeeId = null, int? year = null, int? month = null)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var employees = await LoadEmployeesAsync(hr.OrganizationId);
        var now = DateTime.UtcNow;
        var form = new PayrollCalculationForm
        {
            SelectedEmployeeId = employeeId,
            Year = year ?? now.Year,
            Month = month ?? now.Month,
            ManualDeductions = 0m,
            AdditionalOvertimeHours = 0m
        };

        PayrollCalculationResult? result = null;
        IReadOnlyList<PastPayrollRecordViewModel> history = Array.Empty<PastPayrollRecordViewModel>();
        string? alertMessage = null;
        string alertType = "info";

        if (form.SelectedEmployeeId.HasValue && employees.Any(e => e.Id == form.SelectedEmployeeId.Value))
        {
            result = await BuildCalculationAsync(hr.OrganizationId, form.SelectedEmployeeId.Value, form.Year, form.Month, form.ManualDeductions, form.AdditionalOvertimeHours);
            history = await LoadHistoryAsync(form.SelectedEmployeeId.Value);
            if (result is not null && result.MonthlySalary <= 0)
            {
                alertMessage = "This employee has no monthly salary on file. Update their profile to ensure accurate payroll.";
                alertType = "warning";
            }
        }

        var viewModel = new PayrollIndexViewModel
        {
            Employees = employees,
            Form = form,
            Result = result,
            History = history,
            AlertMessage = alertMessage,
            AlertType = alertType
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(PayrollIndexViewModel postedModel)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var form = postedModel.Form ?? new PayrollCalculationForm();
        postedModel.Form = form;
        var employees = await LoadEmployeesAsync(hr.OrganizationId);
        PayrollCalculationResult? result = null;
        IReadOnlyList<PastPayrollRecordViewModel> history = Array.Empty<PastPayrollRecordViewModel>();
        string? alertMessage = null;
        var alertType = "info";

        if (form.SelectedEmployeeId is null)
        {
            ModelState.AddModelError("Form.SelectedEmployeeId", "Select an employee.");
        }
        else if (!employees.Any(e => e.Id == form.SelectedEmployeeId.Value))
        {
            ModelState.AddModelError("Form.SelectedEmployeeId", "Employee not found.");
        }

        if (form.Year < 2000 || form.Year > 2100)
        {
            ModelState.AddModelError("Form.Year", "Enter a valid year.");
        }

        if (form.Month < 1 || form.Month > 12)
        {
            ModelState.AddModelError("Form.Month", "Enter a month between 1 and 12.");
        }

        if (form.ManualDeductions < 0)
        {
            ModelState.AddModelError("Form.ManualDeductions", "Deductions cannot be negative.");
        }

        if (form.AdditionalOvertimeHours < 0)
        {
            ModelState.AddModelError("Form.AdditionalOvertimeHours", "Overtime hours cannot be negative.");
        }

        if (ModelState.IsValid && form.SelectedEmployeeId.HasValue)
        {
            result = await BuildCalculationAsync(hr.OrganizationId, form.SelectedEmployeeId.Value, form.Year, form.Month, form.ManualDeductions, form.AdditionalOvertimeHours);
            if (result is null)
            {
                ModelState.AddModelError(string.Empty, "Unable to calculate payroll for the selected employee.");
            }
            else
            {
                if (form.SaveRecord)
                {
                    await SavePayrollRecordAsync(result, form.ManualDeductions, form.AdditionalOvertimeHours);
                    alertMessage = $"Payroll for {result.EmployeeName} ({result.PeriodLabel}) saved.";
                    alertType = "success";
                }

                history = await LoadHistoryAsync(result.EmployeeId);

                if (result.MonthlySalary <= 0)
                {
                    alertMessage = "This employee has no monthly salary on file. Update their profile to ensure accurate payroll.";
                    alertType = "warning";
                }
            }
        }
        else if (form.SelectedEmployeeId.HasValue)
        {
            history = await LoadHistoryAsync(form.SelectedEmployeeId.Value);
        }

        var viewModel = new PayrollIndexViewModel
        {
            Employees = employees,
            Form = form,
            Result = result,
            History = history,
            AlertMessage = alertMessage,
            AlertType = alertType
        };

        return View("Index", viewModel);
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return null;
        return await _db.Users.FindAsync(id);
    }

    private async Task<List<PayrollEmployeeOption>> LoadEmployeesAsync(int organizationId)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.OrganizationId == organizationId && u.Role == RoleType.Employee && u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new PayrollEmployeeOption
            {
                Id = u.Id,
                Name = u.Name,
                MonthlySalary = u.MonthlySalary
            })
            .ToListAsync();
    }

    private async Task<PayrollCalculationResult?> BuildCalculationAsync(int organizationId, int employeeId, int year, int month, decimal manualDeductions, decimal additionalOvertimeHours)
    {
        AppUser? employee = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == employeeId && u.OrganizationId == organizationId && u.Role == RoleType.Employee);

        if (employee is null)
        {
            return null;
        }

        DateOnly start;
        try
        {
            start = new DateOnly(year, month, 1);
        }
        catch
        {
            return null;
        }

        var end = start.AddMonths(1).AddDays(-1);

        var records = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.UserId == employee.Id && r.Date >= start && r.Date <= end)
            .ToListAsync();

        decimal workedHours = 0m;
        int presentDays = 0;

        foreach (var record in records)
        {
            if (record.CheckInTime.HasValue)
            {
                presentDays++;
            }

            if (record.CheckInTime.HasValue && record.CheckOutTime.HasValue)
            {
                var duration = record.CheckOutTime.Value - record.CheckInTime.Value;
                if (duration.TotalMinutes <= 0)
                {
                    continue;
                }

                var hours = (decimal)duration.TotalHours;
                if (hours > 24m)
                {
                    hours = 24m;
                }

                workedHours += hours;
            }
        }

        var workingDays = CountBusinessDays(start, end);
        var standardHours = workingDays * StandardDailyHours;
        var hourlyRate = standardHours > 0m && employee.MonthlySalary > 0m
            ? decimal.Round(employee.MonthlySalary / standardHours, 2, MidpointRounding.AwayFromZero)
            : 0m;

        if (workedHours < 0m)
        {
            workedHours = 0m;
        }

        var autoOvertimeHours = Math.Max(workedHours - standardHours, 0m);
        var totalOvertimeHours = autoOvertimeHours + additionalOvertimeHours;
        var regularHours = workedHours - autoOvertimeHours;

        var attendancePay = decimal.Round(regularHours * hourlyRate, 2, MidpointRounding.AwayFromZero);
        var overtimePay = decimal.Round(totalOvertimeHours * hourlyRate * DefaultOvertimeMultiplier, 2, MidpointRounding.AwayFromZero);
        var grossPay = attendancePay + overtimePay;
        var deductions = decimal.Round(manualDeductions, 2, MidpointRounding.AwayFromZero);
        var netPay = grossPay - deductions;

        var result = new PayrollCalculationResult
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Year = year,
            Month = month,
            PeriodLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            MonthlySalary = decimal.Round(employee.MonthlySalary, 2, MidpointRounding.AwayFromZero),
            WorkingDays = workingDays,
            PresentDays = presentDays,
            AbsentDays = Math.Max(workingDays - presentDays, 0),
            StandardHours = decimal.Round(standardHours, 2, MidpointRounding.AwayFromZero),
            WorkedHours = decimal.Round(workedHours, 2, MidpointRounding.AwayFromZero),
            HourlyRate = hourlyRate,
            AutoOvertimeHours = decimal.Round(autoOvertimeHours, 2, MidpointRounding.AwayFromZero),
            ManualOvertimeHours = decimal.Round(additionalOvertimeHours, 2, MidpointRounding.AwayFromZero),
            TotalOvertimeHours = decimal.Round(totalOvertimeHours, 2, MidpointRounding.AwayFromZero),
            OvertimeMultiplier = DefaultOvertimeMultiplier,
            AttendancePay = attendancePay,
            OvertimePay = overtimePay,
            Deductions = deductions,
            GrossPay = decimal.Round(grossPay, 2, MidpointRounding.AwayFromZero),
            NetPay = decimal.Round(netPay, 2, MidpointRounding.AwayFromZero)
        };

        return result;
    }

    private async Task SavePayrollRecordAsync(PayrollCalculationResult result, decimal manualDeductions, decimal additionalOvertimeHours)
    {
        var record = await _db.PayrollRecords.FirstOrDefaultAsync(r => r.UserId == result.EmployeeId && r.Year == result.Year && r.Month == result.Month);
        if (record is null)
        {
            record = new PayrollRecord
            {
                UserId = result.EmployeeId,
                Year = result.Year,
                Month = result.Month
            };
            _db.PayrollRecords.Add(record);
        }

        record.MonthlySalary = result.MonthlySalary;
        record.StandardHours = result.StandardHours;
        record.WorkedHours = result.WorkedHours;
        record.AutoOvertimeHours = result.AutoOvertimeHours;
        record.AdditionalOvertimeHours = decimal.Round(additionalOvertimeHours, 2, MidpointRounding.AwayFromZero);
        record.TotalOvertimeHours = result.TotalOvertimeHours;
        record.HourlyRate = result.HourlyRate;
        record.OvertimeMultiplier = result.OvertimeMultiplier;
        record.AttendancePay = result.AttendancePay;
        record.OvertimePay = result.OvertimePay;
        record.Deductions = decimal.Round(manualDeductions, 2, MidpointRounding.AwayFromZero);
        record.GrossPay = result.GrossPay;
        record.NetPay = result.NetPay;
        record.WorkingDays = result.WorkingDays;
        record.PresentDays = result.PresentDays;
        record.CalculatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
    }

    private async Task<List<PastPayrollRecordViewModel>> LoadHistoryAsync(int employeeId)
    {
        return await _db.PayrollRecords
            .AsNoTracking()
            .Where(r => r.UserId == employeeId)
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Month)
            .Take(12)
            .Select(r => new PastPayrollRecordViewModel
            {
                Year = r.Year,
                Month = r.Month,
                NetPay = r.NetPay,
                GrossPay = r.GrossPay,
                Deductions = r.Deductions,
                CalculatedAt = r.CalculatedAt
            })
            .ToListAsync();
    }

    private static int CountBusinessDays(DateOnly start, DateOnly end)
    {
        if (end < start) return 0;

        var count = 0;
        var current = start;
        while (current <= end)
        {
            if (current.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                count++;
            }

            current = current.AddDays(1);
        }

        return count;
    }
}
