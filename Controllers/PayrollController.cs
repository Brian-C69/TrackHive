using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;
using TrackHive.Services;

namespace TrackHive.Controllers;

[Authorize(Roles = "HR")]
public sealed class PayrollController : Controller
{
    private readonly AppDbContext _db;
    private readonly PayrollPdfGenerator _pdfGenerator;
    private const decimal StandardDailyHours = 8m;
    private const decimal DefaultOvertimeMultiplier = 1.5m;

    public PayrollController(AppDbContext db, PayrollPdfGenerator pdfGenerator)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? employeeId = null, int? year = null, int? month = null)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var employees = await LoadEmployeesAsync(hr.OrganizationId);
        var plan = await GetSubscriptionPlanAsync(hr.OrganizationId);
        var canUsePayroll = PlanHelper.CanAccessPayroll(plan);
        var canExportPdf = PlanHelper.CanExportPdf(plan);

        var now = DateTime.UtcNow;
        var form = new PayrollCalculationForm
        {
            SelectedEmployeeId = employeeId,
            Year = year ?? now.Year,
            Month = month ?? now.Month,
            ManualDeductions = 0m,
            AdditionalOvertimeHours = 0m
        };

        if (!canUsePayroll)
        {
            var restrictedModel = new PayrollIndexViewModel
            {
                Employees = employees,
                Plan = plan,
                CanUsePayroll = false,
                CanExportPdf = canExportPdf,
                Form = form
            };

            return View(restrictedModel);
        }

        PayrollCalculationResult? result = null;
        IReadOnlyList<PastPayrollRecordViewModel> history = Array.Empty<PastPayrollRecordViewModel>();
        string? alertMessage = null;
        string alertType = "info";

        if (form.SelectedEmployeeId.HasValue && employees.Any(e => e.Id == form.SelectedEmployeeId.Value))
        {
            result = await BuildCalculationAsync(hr.OrganizationId, form.SelectedEmployeeId.Value, form.Year, form.Month, form.ManualDeductions, form.AdditionalOvertimeHours);
            history = await LoadHistoryAsync(form.SelectedEmployeeId.Value, plan);
            if (result is not null && result.MonthlySalary <= 0)
            {
                alertMessage = "This employee has no monthly salary on file. Update their profile to ensure accurate payroll.";
                alertType = "warning";
            }
        }

        ApplyTempAlert(ref alertMessage, ref alertType);

        var viewModel = new PayrollIndexViewModel
        {
            Employees = employees,
            Form = form,
            Result = result,
            History = history,
            AlertMessage = alertMessage,
            AlertType = alertType,
            Plan = plan,
            CanUsePayroll = true,
            CanExportPdf = canExportPdf
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
        var plan = await GetSubscriptionPlanAsync(hr.OrganizationId);
        var canUsePayroll = PlanHelper.CanAccessPayroll(plan);
        var canExportPdf = PlanHelper.CanExportPdf(plan);

        var employees = await LoadEmployeesAsync(hr.OrganizationId);

        if (!canUsePayroll)
        {
            var restrictedModel = new PayrollIndexViewModel
            {
                Employees = employees,
                Plan = plan,
                CanUsePayroll = false,
                CanExportPdf = canExportPdf,
                Form = form
            };

            return View("Index", restrictedModel);
        }
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

                history = await LoadHistoryAsync(result.EmployeeId, plan);

                if (result.MonthlySalary <= 0)
                {
                    alertMessage = "This employee has no monthly salary on file. Update their profile to ensure accurate payroll.";
                    alertType = "warning";
                }
            }
        }
        else if (form.SelectedEmployeeId.HasValue)
        {
            history = await LoadHistoryAsync(form.SelectedEmployeeId.Value, plan);
        }

        ApplyTempAlert(ref alertMessage, ref alertType);

        var viewModel = new PayrollIndexViewModel
        {
            Employees = employees,
            Form = form,
            Result = result,
            History = history,
            AlertMessage = alertMessage,
            AlertType = alertType,
            Plan = plan,
            CanUsePayroll = true,
            CanExportPdf = canExportPdf
        };

        return View("Index", viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadPayslip(int employeeId, int year, int month)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var plan = await GetSubscriptionPlanAsync(hr.OrganizationId);
        var canUsePayroll = PlanHelper.CanAccessPayroll(plan);
        var canExportPdf = PlanHelper.CanExportPdf(plan);
        var routeValues = new { employeeId, year, month };

        if (!canUsePayroll)
        {
            SetTempAlert($"Upgrade to the {PlanHelper.GetDisplayName(PlanHelper.PayrollRequiredPlan)} plan to manage payroll.", "info");
            return RedirectToAction("Index", routeValues);
        }

        if (!canExportPdf)
        {
            SetTempAlert($"PDF exports are available on the {PlanHelper.GetDisplayName(PlanHelper.PdfRequiredPlan)} plan.", "info");
            return RedirectToAction("Index", routeValues);
        }

        var form = new PayrollCalculationForm
        {
            SelectedEmployeeId = employeeId,
            Year = year,
            Month = month,
            ManualDeductions = 0m,
            AdditionalOvertimeHours = 0m
        };

        var result = await TryCreatePayslipAsync(hr, form, allowRecalculation: false);
        if (result.Document is null)
        {
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                SetTempAlert(result.ErrorMessage, result.AlertType);
            }

            return RedirectToAction("Index", routeValues);
        }

        return CreatePayslipFile(result.Document, year, month);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadPayslip(PayrollCalculationForm form)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var plan = await GetSubscriptionPlanAsync(hr.OrganizationId);
        var canUsePayroll = PlanHelper.CanAccessPayroll(plan);
        var canExportPdf = PlanHelper.CanExportPdf(plan);
        var routeValues = new { employeeId = form.SelectedEmployeeId, year = form.Year, month = form.Month };

        if (!canUsePayroll)
        {
            SetTempAlert($"Upgrade to the {PlanHelper.GetDisplayName(PlanHelper.PayrollRequiredPlan)} plan to manage payroll.", "info");
            return RedirectToAction("Index", routeValues);
        }

        if (!canExportPdf)
        {
            SetTempAlert($"PDF exports are available on the {PlanHelper.GetDisplayName(PlanHelper.PdfRequiredPlan)} plan.", "info");
            return RedirectToAction("Index", routeValues);
        }

        var result = await TryCreatePayslipAsync(hr, form, allowRecalculation: true);
        if (result.Document is null)
        {
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                SetTempAlert(result.ErrorMessage, result.AlertType);
            }

            return RedirectToAction("Index", routeValues);
        }

        return CreatePayslipFile(result.Document, form.Year, form.Month);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReport(int year, int month)
    {
        var hr = await GetCurrentUserAsync();
        if (hr is null) return RedirectToAction("Login", "Auth");
        if (hr.MustChangePassword) return RedirectToAction("ChangePassword", "Auth");

        var plan = await GetSubscriptionPlanAsync(hr.OrganizationId);
        var canUsePayroll = PlanHelper.CanAccessPayroll(plan);
        var canExportPdf = PlanHelper.CanExportPdf(plan);

        if (!canUsePayroll)
        {
            SetTempAlert($"Upgrade to the {PlanHelper.GetDisplayName(PlanHelper.PayrollRequiredPlan)} plan to manage payroll.", "info");
            return RedirectToAction("Index", new { year, month });
        }

        if (!canExportPdf)
        {
            SetTempAlert($"PDF exports are available on the {PlanHelper.GetDisplayName(PlanHelper.PdfRequiredPlan)} plan.", "info");
            return RedirectToAction("Index", new { year, month });
        }

        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            SetTempAlert("Choose a valid month and year to export the payroll report.", "warning");
            return RedirectToAction("Index", new { year, month });
        }

        var organization = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == hr.OrganizationId);
        if (organization is null)
        {
            return NotFound();
        }

        var retentionCutoff = RetentionPolicy.GetCutoff(organization.CurrentPlan, DateTimeOffset.UtcNow);

        var recordsQuery = _db.PayrollRecords
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.Year == year && r.Month == month && r.User != null && r.User.OrganizationId == hr.OrganizationId);

        if (retentionCutoff is DateTimeOffset cutoff)
        {
            recordsQuery = recordsQuery.Where(r => r.CalculatedAt >= cutoff);
        }

        var records = await recordsQuery
            .OrderBy(r => r.User!.Name)
            .ToListAsync();

        if (records.Count == 0)
        {
            var label = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            SetTempAlert($"No payroll records found for {label}.", "warning");
            return RedirectToAction("Index", new { year, month });
        }

        var entries = records
            .Select(r => new PayrollReportEntry(
                r.User!.Name,
                r.User.Email,
                r.GrossPay,
                r.NetPay,
                r.Deductions,
                r.OvertimePay,
                r.TotalOvertimeHours,
                r.WorkingDays,
                r.PresentDays,
                r.CalculatedAt))
            .ToList();

        var model = new PayrollReportDocumentModel(organization.Name, year, month, DateTimeOffset.UtcNow, entries);

        var pdf = _pdfGenerator.GenerateMonthlyReport(model);
        var safeOrg = SanitizeFileName(model.OrganizationName);
        var fileName = $"{safeOrg}_{year}-{month:00}_PayrollReport.pdf";

        return File(pdf, "application/pdf", fileName);
    }

    private void SetTempAlert(string message, string type)
    {
        TempData["PayrollAlert.Message"] = message;
        TempData["PayrollAlert.Type"] = type;
    }

    private void ApplyTempAlert(ref string? message, ref string alertType)
    {
        if (TempData.TryGetValue("PayrollAlert.Message", out var storedMessageObj) && storedMessageObj is string storedMessage && !string.IsNullOrWhiteSpace(storedMessage))
        {
            message ??= storedMessage;
            if (TempData.TryGetValue("PayrollAlert.Type", out var storedTypeObj) && storedTypeObj is string storedType && !string.IsNullOrWhiteSpace(storedType))
            {
                alertType = storedType;
            }
        }
    }

    private FileContentResult CreatePayslipFile(PayslipDocumentModel document, int year, int month)
    {
        var pdf = _pdfGenerator.GeneratePayslip(document);
        var safeOrg = SanitizeFileName(document.OrganizationName);
        var safeEmployee = SanitizeFileName(document.EmployeeName);
        var fileName = $"{safeOrg}_{safeEmployee}_{year}-{month:00}_Payslip.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    private async Task<PayslipGenerationResult> TryCreatePayslipAsync(AppUser hr, PayrollCalculationForm form, bool allowRecalculation)
    {
        if (form.SelectedEmployeeId is null)
        {
            return new PayslipGenerationResult(null, "Select an employee to generate a payslip.", "warning");
        }

        if (form.Year < 2000 || form.Year > 2100 || form.Month < 1 || form.Month > 12)
        {
            return new PayslipGenerationResult(null, "Choose a valid month and year for the payslip.", "warning");
        }

        if (form.ManualDeductions < 0 || form.AdditionalOvertimeHours < 0)
        {
            return new PayslipGenerationResult(null, "Deductions and overtime hours cannot be negative.", "warning");
        }

        var employee = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == form.SelectedEmployeeId.Value && u.OrganizationId == hr.OrganizationId && u.Role == RoleType.Employee);

        if (employee is null)
        {
            return new PayslipGenerationResult(null, "Employee not found.", "danger");
        }

        var organization = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == hr.OrganizationId);
        if (organization is null)
        {
            return new PayslipGenerationResult(null, "Organization not found.", "danger");
        }

        var now = DateTimeOffset.UtcNow;
        var retentionCutoff = RetentionPolicy.GetCutoff(organization.CurrentPlan, now);

        var periodLabel = new DateTime(form.Year, form.Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        var recordQuery = _db.PayrollRecords
            .AsNoTracking()
            .Where(r => r.UserId == employee.Id && r.Year == form.Year && r.Month == form.Month);

        if (retentionCutoff is DateTimeOffset cutoff)
        {
            recordQuery = recordQuery.Where(r => r.CalculatedAt >= cutoff);
        }

        var record = await recordQuery.FirstOrDefaultAsync();

        if (record is not null)
        {
            var absentDays = Math.Max(record.WorkingDays - record.PresentDays, 0);
            var document = new PayslipDocumentModel(
                organization.Name,
                employee.Name,
                employee.Email,
                periodLabel,
                record.CalculatedAt,
                record.MonthlySalary,
                record.WorkingDays,
                record.PresentDays,
                absentDays,
                record.StandardHours,
                record.WorkedHours,
                record.HourlyRate,
                record.AutoOvertimeHours,
                record.AdditionalOvertimeHours,
                record.TotalOvertimeHours,
                record.OvertimeMultiplier,
                record.AttendancePay,
                record.OvertimePay,
                record.Deductions,
                record.GrossPay,
                record.NetPay);

            return new PayslipGenerationResult(document, null, "success");
        }

        if (!allowRecalculation)
        {
            return new PayslipGenerationResult(null, $"No saved payroll record found for {periodLabel}.", "warning");
        }

        var calculation = await BuildCalculationAsync(hr.OrganizationId, employee.Id, form.Year, form.Month, form.ManualDeductions, form.AdditionalOvertimeHours);
        if (calculation is null)
        {
            return new PayslipGenerationResult(null, "Unable to calculate the payslip for the selected period.", "danger");
        }

        var calculatedDocument = new PayslipDocumentModel(
            organization.Name,
            calculation.EmployeeName,
            employee.Email,
            calculation.PeriodLabel,
            now,
            calculation.MonthlySalary,
            calculation.WorkingDays,
            calculation.PresentDays,
            calculation.AbsentDays,
            calculation.StandardHours,
            calculation.WorkedHours,
            calculation.HourlyRate,
            calculation.AutoOvertimeHours,
            calculation.ManualOvertimeHours,
            calculation.TotalOvertimeHours,
            calculation.OvertimeMultiplier,
            calculation.AttendancePay,
            calculation.OvertimePay,
            calculation.Deductions,
            calculation.GrossPay,
            calculation.NetPay);

        return new PayslipGenerationResult(calculatedDocument, null, "success");
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "document";
        }

        var sanitized = value.Replace(' ', '_');
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '_');
        }

        return sanitized;
    }

    private sealed record PayslipGenerationResult(PayslipDocumentModel? Document, string? ErrorMessage, string AlertType);

    private async Task<SubscriptionPlan> GetSubscriptionPlanAsync(int organizationId)
    {
        return await _db.Organizations
            .AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => o.CurrentPlan)
            .FirstOrDefaultAsync();
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

    private async Task<List<PastPayrollRecordViewModel>> LoadHistoryAsync(int employeeId, SubscriptionPlan plan)
    {
        var now = DateTimeOffset.UtcNow;
        var retentionCutoff = RetentionPolicy.GetCutoff(plan, now);

        var query = _db.PayrollRecords
            .AsNoTracking()
            .Where(r => r.UserId == employeeId);

        if (retentionCutoff is DateTimeOffset cutoff)
        {
            query = query.Where(r => r.CalculatedAt >= cutoff);
        }

        return await query
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
