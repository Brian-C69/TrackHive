using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace TrackHive.Models;

public sealed class PayrollEmployeeOption
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public decimal MonthlySalary { get; init; }
}

public sealed class PayrollCalculationForm
{
    [Display(Name = "Employee")]
    public int? SelectedEmployeeId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Display(Name = "Manual deductions"), Range(0, 1_000_000)]
    public decimal ManualDeductions { get; set; }

    [Display(Name = "Additional overtime hours"), Range(0, 1_000)]
    public decimal AdditionalOvertimeHours { get; set; }

    [Display(Name = "Save payroll record")]
    public bool SaveRecord { get; set; }
}

public sealed class PayrollCalculationResult
{
    public required int EmployeeId { get; init; }
    public required string EmployeeName { get; init; }
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required string PeriodLabel { get; init; }
    public required decimal MonthlySalary { get; init; }
    public required int WorkingDays { get; init; }
    public required int PresentDays { get; init; }
    public required int AbsentDays { get; init; }
    public required decimal StandardHours { get; init; }
    public required decimal WorkedHours { get; init; }
    public required decimal HourlyRate { get; init; }
    public required decimal AutoOvertimeHours { get; init; }
    public required decimal ManualOvertimeHours { get; init; }
    public required decimal TotalOvertimeHours { get; init; }
    public required decimal OvertimeMultiplier { get; init; }
    public required decimal AttendancePay { get; init; }
    public required decimal OvertimePay { get; init; }
    public required decimal Deductions { get; init; }
    public required decimal GrossPay { get; init; }
    public required decimal NetPay { get; init; }

    public string FormattedMonthlySalary => FormatCurrency(MonthlySalary);
    public string FormattedAttendancePay => FormatCurrency(AttendancePay);
    public string FormattedOvertimePay => FormatCurrency(OvertimePay);
    public string FormattedGrossPay => FormatCurrency(GrossPay);
    public string FormattedNetPay => FormatCurrency(NetPay);
    public string FormattedDeductions => FormatCurrency(Deductions);
    public string FormattedHourlyRate => FormatCurrency(HourlyRate);

    private static string FormatCurrency(decimal value) => string.Format(CultureInfo.InvariantCulture, "${0:N2}", value);
}

public sealed class PastPayrollRecordViewModel
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required decimal NetPay { get; init; }
    public required decimal GrossPay { get; init; }
    public required decimal Deductions { get; init; }
    public required DateTimeOffset CalculatedAt { get; init; }

    public string PeriodLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);
}

public sealed class PayrollIndexViewModel
{
    public IReadOnlyList<PayrollEmployeeOption> Employees { get; init; } = Array.Empty<PayrollEmployeeOption>();
    public PayrollCalculationForm Form { get; set; } = new();
    public PayrollCalculationResult? Result { get; init; }
    public IReadOnlyList<PastPayrollRecordViewModel> History { get; init; } = Array.Empty<PastPayrollRecordViewModel>();
    public string? AlertMessage { get; init; }
    public string AlertType { get; init; } = "info";

    public bool HasEmployees => Employees.Count > 0;
    public SubscriptionPlan Plan { get; init; } = SubscriptionPlan.Free;
    public bool CanUsePayroll { get; init; } = true;
    public bool CanExportPdf { get; init; } = true;
}
