using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TrackHive.Services;

public sealed class PayrollPdfGenerator
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public byte[] GeneratePayslip(PayslipDocumentModel data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(11));

                page.Header().Element(header => BuildPayslipHeader(header, data));
                page.Content().Element(content => BuildPayslipContent(content, data));
                page.Footer().AlignCenter()
                    .DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Medium))
                    .Text(text =>
                    {
                        text.Span($"Generated on {FormatDate(data.GeneratedAt)} · Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateMonthlyReport(PayrollReportDocumentModel data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Element(header => BuildReportHeader(header, data));
                page.Content().Element(content => BuildReportContent(content, data));
                page.Footer().AlignCenter()
                    .DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Medium))
                    .Text(text =>
                    {
                        text.Span($"Generated on {FormatDate(data.GeneratedAt)} · Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildPayslipHeader(IContainer container, PayslipDocumentModel data)
    {
        container.Row(row =>
        {
            row.RelativeColumn().Column(column =>
            {
                column.Item().Text(data.OrganizationName)
                    .FontSize(20)
                    .SemiBold();

                column.Item().Text("Official payslip")
                    .FontSize(12)
                    .FontColor(Colors.Grey.Darken2);

                column.Item().Text(data.PeriodLabel)
                    .FontSize(12)
                    .FontColor(Colors.Grey.Darken1);
            });

            row.ConstantColumn(160).AlignRight().Column(column =>
            {
                column.Item().Text("Net pay")
                    .FontSize(11)
                    .SemiBold()
                    .FontColor(Colors.Grey.Darken2);

                column.Item().Text(FormatCurrency(data.NetPay))
                    .FontSize(22)
                    .Bold()
                    .FontColor(Colors.Green.Darken2);
            });
        });
    }

    private static void BuildPayslipContent(IContainer container, PayslipDocumentModel data)
    {
        container.Column(column =>
        {
            column.Spacing(14);

            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(14).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Employee details").SemiBold();
                section.Item().Text(data.EmployeeName);

                if (!string.IsNullOrWhiteSpace(data.EmployeeEmail))
                {
                    section.Item().Text(data.EmployeeEmail)
                        .FontColor(Colors.Grey.Darken1)
                        .FontSize(10);
                }

                section.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeColumn().Text("Pay period");
                    row.ConstantColumn(200).AlignRight().Text(data.PeriodLabel);
                });

                section.Item().Row(row =>
                {
                    row.RelativeColumn().Text("Generated");
                    row.ConstantColumn(200).AlignRight().Text(FormatDate(data.GeneratedAt));
                });
            });

            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(14).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Attendance summary").SemiBold();
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(120);
                    });

                    AddTableRow(table, "Working days", data.WorkingDays.ToString(Culture));
                    AddTableRow(table, "Present days", data.PresentDays.ToString(Culture));
                    AddTableRow(table, "Absent days", data.AbsentDays.ToString(Culture));
                    AddTableRow(table, "Standard hours", FormatHours(data.StandardHours));
                    AddTableRow(table, "Worked hours", FormatHours(data.WorkedHours));
                    AddTableRow(table, "Hourly rate", FormatCurrency(data.HourlyRate));
                });
            });

            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(14).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Overtime breakdown").SemiBold();
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(120);
                    });

                    AddTableRow(table, "Automatic overtime (hrs)", FormatHours(data.AutoOvertimeHours));
                    AddTableRow(table, "Manual overtime (hrs)", FormatHours(data.ManualOvertimeHours));
                    AddTableRow(table, "Total overtime (hrs)", FormatHours(data.TotalOvertimeHours));
                    AddTableRow(table, "Overtime multiplier", $"{data.OvertimeMultiplier:N2}×");
                    AddTableRow(table, "Overtime pay", FormatCurrency(data.OvertimePay));
                });
            });

            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(14).Column(section =>
            {
                section.Spacing(6);
                section.Item().Text("Compensation").SemiBold();
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(120);
                    });

                    AddTableRow(table, "Monthly salary", FormatCurrency(data.MonthlySalary));
                    AddTableRow(table, "Attendance pay", FormatCurrency(data.AttendancePay));
                    AddTableRow(table, "Overtime pay", FormatCurrency(data.OvertimePay));
                    AddTableRow(table, "Deductions", FormatCurrency(data.Deductions));
                    AddTableRow(table, "Gross pay", FormatCurrency(data.GrossPay));
                });
            });

            column.Item().Background(Colors.Green.Lighten5).Border(1).BorderColor(Colors.Green.Lighten2).Padding(16).Row(row =>
            {
                row.RelativeColumn().Column(section =>
                {
                    section.Item().Text("Net pay").FontSize(12).SemiBold();
                    section.Item().Text(FormatCurrency(data.NetPay)).FontSize(20).Bold();
                });

                row.ConstantColumn(200).AlignRight().Column(section =>
                {
                    section.Item().Text("Take-home summary")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);

                    section.Item().Text(text =>
                    {
                        text.Span("Gross").FontColor(Colors.Grey.Darken1).FontSize(10);
                        text.Span($": {FormatCurrency(data.GrossPay)}").FontSize(10);
                        text.Line($"Deductions: {FormatCurrency(data.Deductions)}").FontSize(10);
                    });
                });
            });
        });
    }

    private static void BuildReportHeader(IContainer container, PayrollReportDocumentModel data)
    {
        container.Column(column =>
        {
            column.Item().Text(data.OrganizationName)
                .FontSize(20)
                .SemiBold();

            column.Item().Text($"Payroll report · {data.PeriodLabel}")
                .FontSize(12)
                .FontColor(Colors.Grey.Darken2);

            column.Item().Text($"Employees included: {data.Records.Count}")
                .FontSize(10)
                .FontColor(Colors.Grey.Darken1);
        });
    }

    private static void BuildReportContent(IContainer container, PayrollReportDocumentModel data)
    {
        var grossTotal = data.Records.Sum(r => r.GrossPay);
        var netTotal = data.Records.Sum(r => r.NetPay);
        var deductionTotal = data.Records.Sum(r => r.Deductions);
        var overtimeTotal = data.Records.Sum(r => r.OvertimePay);

        container.Column(column =>
        {
            column.Spacing(12);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellHeaderStyle).Text("Employee");
                    header.Cell().Element(CellHeaderStyle).AlignRight().Text("Gross");
                    header.Cell().Element(CellHeaderStyle).AlignRight().Text("Net");
                    header.Cell().Element(CellHeaderStyle).AlignRight().Text("Deductions");
                    header.Cell().Element(CellHeaderStyle).AlignRight().Text("Overtime");
                    header.Cell().Element(CellHeaderStyle).Text("Generated");
                });

                foreach (var record in data.Records)
                {
                    table.Cell().Element(CellStyle).Text(text =>
                    {
                        text.Span(record.EmployeeName).SemiBold();
                        if (!string.IsNullOrWhiteSpace(record.EmployeeEmail))
                        {
                            text.Line(record.EmployeeEmail).FontSize(9).FontColor(Colors.Grey.Darken1);
                        }

                        text.Line($"Attendance: {record.PresentDays}/{record.WorkingDays} days")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });

                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(record.GrossPay));
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(record.NetPay));
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(record.Deductions));
                    table.Cell().Element(CellStyle).AlignRight().Text(text =>
                    {
                        text.Span(FormatCurrency(record.OvertimePay));
                        text.Line($"{record.TotalOvertimeHours:N2} hrs").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    table.Cell().Element(CellStyle).Text(FormatDate(record.CalculatedAt));
                }
            });

            column.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(8).Row(row =>
            {
                row.RelativeColumn().Text("Totals").SemiBold();
                row.RelativeColumn().AlignRight().Text(FormatCurrency(grossTotal));
                row.RelativeColumn().AlignRight().Text(FormatCurrency(netTotal));
                row.RelativeColumn().AlignRight().Text(FormatCurrency(deductionTotal));
                row.RelativeColumn().AlignRight().Text(FormatCurrency(overtimeTotal));
                row.RelativeColumn(2).Text($"Report generated: {FormatDate(data.GeneratedAt)}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void AddTableRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(CellStyle).Text(label);
        table.Cell().Element(CellStyle).AlignRight().Text(value);
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten4)
            .PaddingVertical(6);
    }

    private static IContainer CellHeaderStyle(IContainer container)
    {
        return container.Background(Colors.Grey.Lighten4)
            .PaddingVertical(6)
            .PaddingHorizontal(4)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2);
    }

    private static string FormatCurrency(decimal value) => string.Format(Culture, "${0:N2}", value);

    private static string FormatHours(decimal value) => value.ToString("N2", Culture);

    private static string FormatDate(DateTimeOffset value) => value.ToLocalTime().ToString("f", Culture);
}

public sealed record PayslipDocumentModel(
    string OrganizationName,
    string EmployeeName,
    string EmployeeEmail,
    string PeriodLabel,
    DateTimeOffset GeneratedAt,
    decimal MonthlySalary,
    int WorkingDays,
    int PresentDays,
    int AbsentDays,
    decimal StandardHours,
    decimal WorkedHours,
    decimal HourlyRate,
    decimal AutoOvertimeHours,
    decimal ManualOvertimeHours,
    decimal TotalOvertimeHours,
    decimal OvertimeMultiplier,
    decimal AttendancePay,
    decimal OvertimePay,
    decimal Deductions,
    decimal GrossPay,
    decimal NetPay);

public sealed record PayrollReportDocumentModel(
    string OrganizationName,
    int Year,
    int Month,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PayrollReportEntry> Records)
{
    public string PeriodLabel => new DateTime(Year, Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
}

public sealed record PayrollReportEntry(
    string EmployeeName,
    string EmployeeEmail,
    decimal GrossPay,
    decimal NetPay,
    decimal Deductions,
    decimal OvertimePay,
    decimal TotalOvertimeHours,
    int WorkingDays,
    int PresentDays,
    DateTimeOffset CalculatedAt);
