using System.ComponentModel.DataAnnotations.Schema;

namespace TrackHive.Models;

public sealed class PayrollRecord
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlySalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal StandardHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WorkedHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AutoOvertimeHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AdditionalOvertimeHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalOvertimeHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OvertimeMultiplier { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AttendancePay { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OvertimePay { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Deductions { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossPay { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetPay { get; set; }

    public int WorkingDays { get; set; }
    public int PresentDays { get; set; }

    public DateTimeOffset CalculatedAt { get; set; }
}
