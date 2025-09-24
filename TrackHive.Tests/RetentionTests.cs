using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TrackHive.Controllers;
using TrackHive.Models;
using TrackHive.Services;

namespace TrackHive.Tests;

[TestClass]
public sealed class RetentionTests
{
    private static DbContextOptions<AppDbContext> CreateOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static async Task<AppDbContext> CreateInitializedContextAsync(DbContextOptions<AppDbContext> options)
    {
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [TestMethod]
    public async Task Cleanup_PrunesFreePlanRecords()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        var now = DateTimeOffset.UtcNow;

        int freeEmployeeId;
        int paidEmployeeId;

        await using (var seedContext = await CreateInitializedContextAsync(options))
        {
            var freeOrg = new Organization { Name = "Free Org", CreatedByEmail = "free@example.com", Plan = OrganizationPlan.Free };
            var paidOrg = new Organization { Name = "Pro Org", CreatedByEmail = "pro@example.com", Plan = OrganizationPlan.Pro };
            seedContext.Organizations.AddRange(freeOrg, paidOrg);
            await seedContext.SaveChangesAsync();

            var freeHr = new AppUser
            {
                Name = "Free HR",
                Email = "freehr@example.com",
                PasswordHash = "hash",
                Role = RoleType.HR,
                OrganizationId = freeOrg.Id,
                ThemePreference = "light",
                LanguagePreference = "en"
            };
            var freeEmployee = new AppUser
            {
                Name = "Free Employee",
                Email = "freeemp@example.com",
                PasswordHash = "hash",
                Role = RoleType.Employee,
                OrganizationId = freeOrg.Id,
                ThemePreference = "light",
                LanguagePreference = "en",
                MonthlySalary = 1200m
            };
            var paidEmployee = new AppUser
            {
                Name = "Paid Employee",
                Email = "paidemp@example.com",
                PasswordHash = "hash",
                Role = RoleType.Employee,
                OrganizationId = paidOrg.Id,
                ThemePreference = "light",
                LanguagePreference = "en",
                MonthlySalary = 1500m
            };

            seedContext.Users.AddRange(freeHr, freeEmployee, paidEmployee);
            await seedContext.SaveChangesAsync();

            freeEmployeeId = freeEmployee.Id;
            paidEmployeeId = paidEmployee.Id;

            seedContext.AttendanceRecords.AddRange(
                new AttendanceRecord
                {
                    UserId = freeEmployeeId,
                    Date = DateOnly.FromDateTime(now.AddDays(-100).UtcDateTime),
                    CheckInTime = now.AddDays(-100),
                    CheckOutTime = now.AddDays(-100).AddHours(8)
                },
                new AttendanceRecord
                {
                    UserId = freeEmployeeId,
                    Date = DateOnly.FromDateTime(now.AddDays(-10).UtcDateTime),
                    CheckInTime = now.AddDays(-10),
                    CheckOutTime = now.AddDays(-10).AddHours(8)
                },
                new AttendanceRecord
                {
                    UserId = paidEmployeeId,
                    Date = DateOnly.FromDateTime(now.AddDays(-120).UtcDateTime),
                    CheckInTime = now.AddDays(-120),
                    CheckOutTime = now.AddDays(-120).AddHours(8)
                });

            var oldLeave = new LeaveRequest
            {
                UserId = freeEmployeeId,
                StartDate = DateOnly.FromDateTime(now.AddDays(-110).UtcDateTime),
                EndDate = DateOnly.FromDateTime(now.AddDays(-105).UtcDateTime),
                TotalDays = 5,
                Reason = "Old", 
                Type = LeaveType.Sick,
                Status = LeaveRequestStatus.Pending,
                CreatedAt = now.AddDays(-120)
            };
            var newLeave = new LeaveRequest
            {
                UserId = freeEmployeeId,
                StartDate = DateOnly.FromDateTime(now.AddDays(-20).UtcDateTime),
                EndDate = DateOnly.FromDateTime(now.AddDays(-18).UtcDateTime),
                TotalDays = 3,
                Reason = "New",
                Type = LeaveType.Annual,
                Status = LeaveRequestStatus.Pending,
                CreatedAt = now.AddDays(-30)
            };
            var paidLeave = new LeaveRequest
            {
                UserId = paidEmployeeId,
                StartDate = DateOnly.FromDateTime(now.AddDays(-150).UtcDateTime),
                EndDate = DateOnly.FromDateTime(now.AddDays(-148).UtcDateTime),
                TotalDays = 3,
                Reason = "Paid",
                Type = LeaveType.Annual,
                Status = LeaveRequestStatus.Approved,
                CreatedAt = now.AddDays(-150)
            };

            seedContext.LeaveRequests.AddRange(oldLeave, newLeave, paidLeave);
            await seedContext.SaveChangesAsync();

            seedContext.LeaveDocuments.Add(new LeaveDocument
            {
                LeaveRequestId = oldLeave.Id,
                OriginalFileName = "medical.pdf",
                StoredFilePath = "medical.pdf",
                UploadedAt = now.AddDays(-120)
            });
            await seedContext.SaveChangesAsync();

            seedContext.PayrollRecords.AddRange(
                new PayrollRecord
                {
                    UserId = freeEmployeeId,
                    Year = now.Year,
                    Month = now.Month,
                    MonthlySalary = 1200m,
                    StandardHours = 160m,
                    WorkedHours = 160m,
                    AutoOvertimeHours = 0m,
                    AdditionalOvertimeHours = 0m,
                    TotalOvertimeHours = 0m,
                    HourlyRate = 15m,
                    OvertimeMultiplier = 1.5m,
                    AttendancePay = 1200m,
                    OvertimePay = 0m,
                    Deductions = 0m,
                    GrossPay = 1200m,
                    NetPay = 1200m,
                    WorkingDays = 20,
                    PresentDays = 20,
                    CalculatedAt = now.AddDays(-10)
                },
                new PayrollRecord
                {
                    UserId = freeEmployeeId,
                    Year = now.AddMonths(-4).Year,
                    Month = now.AddMonths(-4).Month,
                    MonthlySalary = 1200m,
                    StandardHours = 160m,
                    WorkedHours = 160m,
                    AutoOvertimeHours = 0m,
                    AdditionalOvertimeHours = 0m,
                    TotalOvertimeHours = 0m,
                    HourlyRate = 15m,
                    OvertimeMultiplier = 1.5m,
                    AttendancePay = 1200m,
                    OvertimePay = 0m,
                    Deductions = 0m,
                    GrossPay = 1200m,
                    NetPay = 1200m,
                    WorkingDays = 20,
                    PresentDays = 20,
                    CalculatedAt = now.AddDays(-120)
                },
                new PayrollRecord
                {
                    UserId = paidEmployeeId,
                    Year = now.AddMonths(-5).Year,
                    Month = now.AddMonths(-5).Month,
                    MonthlySalary = 1500m,
                    StandardHours = 160m,
                    WorkedHours = 160m,
                    AutoOvertimeHours = 0m,
                    AdditionalOvertimeHours = 0m,
                    TotalOvertimeHours = 0m,
                    HourlyRate = 18.75m,
                    OvertimeMultiplier = 1.5m,
                    AttendancePay = 1500m,
                    OvertimePay = 0m,
                    Deductions = 0m,
                    GrossPay = 1500m,
                    NetPay = 1500m,
                    WorkingDays = 20,
                    PresentDays = 20,
                    CalculatedAt = now.AddDays(-150)
                });

            await seedContext.SaveChangesAsync();
        }

        await using (var cleanupContext = new AppDbContext(options))
        {
            var cleanup = new ScopedDataRetentionCleanup(cleanupContext);
            var removed = await cleanup.ApplyAsync(now);
            Assert.AreEqual(4, removed);
        }

        await using var verificationContext = new AppDbContext(options);

        var freeAttendanceDates = await verificationContext.AttendanceRecords
            .Where(a => a.UserId == freeEmployeeId)
            .Select(a => a.Date)
            .ToListAsync();
        Assert.AreEqual(1, freeAttendanceDates.Count);
        foreach (var date in freeAttendanceDates)
        {
            Assert.IsTrue(date.ToDateTime(TimeOnly.MinValue) >= now.AddDays(-RetentionPolicy.FreePlanRetentionDays).UtcDateTime);
        }

        var freeLeaveRequests = await verificationContext.LeaveRequests
            .Where(r => r.UserId == freeEmployeeId)
            .ToListAsync();
        Assert.AreEqual(1, freeLeaveRequests.Count);
        Assert.IsTrue(freeLeaveRequests[0].CreatedAt >= now.AddDays(-RetentionPolicy.FreePlanRetentionDays));

        var freePayrollRecords = await verificationContext.PayrollRecords
            .Where(r => r.UserId == freeEmployeeId)
            .ToListAsync();
        Assert.AreEqual(1, freePayrollRecords.Count);

        var paidAttendanceCount = await verificationContext.AttendanceRecords
            .CountAsync(a => a.UserId == paidEmployeeId);
        Assert.AreEqual(1, paidAttendanceCount);

        var paidPayrollCount = await verificationContext.PayrollRecords
            .CountAsync(r => r.UserId == paidEmployeeId);
        Assert.AreEqual(1, paidPayrollCount);
    }

    [TestMethod]
    public async Task HrDashboard_FreePlanOmitsStalePendingRequests()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        var now = DateTimeOffset.UtcNow;

        int hrId;
        int currentRequestId;

        await using (var seedContext = await CreateInitializedContextAsync(options))
        {
            var org = new Organization { Name = "Free Org", CreatedByEmail = "hr@free.com", Plan = OrganizationPlan.Free };
            seedContext.Organizations.Add(org);
            await seedContext.SaveChangesAsync();

            var hr = new AppUser
            {
                Name = "HR",
                Email = "hr@free.com",
                PasswordHash = "hash",
                Role = RoleType.HR,
                OrganizationId = org.Id,
                ThemePreference = "light",
                LanguagePreference = "en"
            };
            var employee = new AppUser
            {
                Name = "Employee",
                Email = "employee@free.com",
                PasswordHash = "hash",
                Role = RoleType.Employee,
                OrganizationId = org.Id,
                ThemePreference = "light",
                LanguagePreference = "en",
                MonthlySalary = 1000m
            };

            seedContext.Users.AddRange(hr, employee);
            await seedContext.SaveChangesAsync();

            hrId = hr.Id;

            seedContext.LeaveBalances.Add(new LeaveBalance
            {
                UserId = employee.Id,
                AnnualEntitlement = LeaveBalance.DefaultAnnualEntitlement,
                UsedDays = 0,
                UpdatedAt = now
            });

            seedContext.LeaveRequests.AddRange(
                new LeaveRequest
                {
                    UserId = employee.Id,
                    StartDate = DateOnly.FromDateTime(now.AddDays(-100).UtcDateTime),
                    EndDate = DateOnly.FromDateTime(now.AddDays(-97).UtcDateTime),
                    TotalDays = 4,
                    Type = LeaveType.Annual,
                    Status = LeaveRequestStatus.Pending,
                    CreatedAt = now.AddDays(-120)
                },
                new LeaveRequest
                {
                    UserId = employee.Id,
                    StartDate = DateOnly.FromDateTime(now.AddDays(-15).UtcDateTime),
                    EndDate = DateOnly.FromDateTime(now.AddDays(-13).UtcDateTime),
                    TotalDays = 3,
                    Type = LeaveType.Sick,
                    Status = LeaveRequestStatus.Pending,
                    CreatedAt = now.AddDays(-20)
                });

            await seedContext.SaveChangesAsync();

            currentRequestId = await seedContext.LeaveRequests
                .Where(r => r.CreatedAt >= now.AddDays(-30))
                .Select(r => r.Id)
                .FirstAsync();
        }

        await using var context = new AppDbContext(options);
        var hrUser = await context.Users.FindAsync(hrId);
        Assert.IsNotNull(hrUser);

        var emailService = new EmailService(Options.Create(new SmtpOptions
        {
            Host = "localhost",
            Port = 25,
            User = "noreply@example.com",
            Pass = "password",
            Name = "TrackHive"
        }));

        var controller = new HrDashboardController(context, emailService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionContext = new ActionContext(controller.ControllerContext.HttpContext!, new RouteData(), new ActionDescriptor());
        controller.Url = new UrlHelper(actionContext);

        var method = typeof(HrDashboardController)
            .GetMethod("BuildDashboardViewModelAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<HrDashboardViewModel>)method!.Invoke(controller, new object?[] { hrUser!, null })!;
        var viewModel = await task;

        Assert.AreEqual(1, viewModel.PendingLeaveRequests.Count);
        Assert.AreEqual(currentRequestId, viewModel.PendingLeaveRequests[0].RequestId);
    }

    [TestMethod]
    public async Task HrDashboard_PaidPlanKeepsFullHistory()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        var now = DateTimeOffset.UtcNow;

        int hrId;

        await using (var seedContext = await CreateInitializedContextAsync(options))
        {
            var org = new Organization { Name = "Pro Org", CreatedByEmail = "hr@pro.com", Plan = OrganizationPlan.Pro };
            seedContext.Organizations.Add(org);
            await seedContext.SaveChangesAsync();

            var hr = new AppUser
            {
                Name = "HR",
                Email = "hr@pro.com",
                PasswordHash = "hash",
                Role = RoleType.HR,
                OrganizationId = org.Id,
                ThemePreference = "light",
                LanguagePreference = "en"
            };
            var employee = new AppUser
            {
                Name = "Employee",
                Email = "employee@pro.com",
                PasswordHash = "hash",
                Role = RoleType.Employee,
                OrganizationId = org.Id,
                ThemePreference = "light",
                LanguagePreference = "en",
                MonthlySalary = 1000m
            };

            seedContext.Users.AddRange(hr, employee);
            await seedContext.SaveChangesAsync();

            hrId = hr.Id;

            seedContext.LeaveRequests.AddRange(
                new LeaveRequest
                {
                    UserId = employee.Id,
                    StartDate = DateOnly.FromDateTime(now.AddDays(-150).UtcDateTime),
                    EndDate = DateOnly.FromDateTime(now.AddDays(-148).UtcDateTime),
                    TotalDays = 3,
                    Type = LeaveType.Annual,
                    Status = LeaveRequestStatus.Pending,
                    CreatedAt = now.AddDays(-150)
                },
                new LeaveRequest
                {
                    UserId = employee.Id,
                    StartDate = DateOnly.FromDateTime(now.AddDays(-10).UtcDateTime),
                    EndDate = DateOnly.FromDateTime(now.AddDays(-8).UtcDateTime),
                    TotalDays = 3,
                    Type = LeaveType.Sick,
                    Status = LeaveRequestStatus.Pending,
                    CreatedAt = now.AddDays(-10)
                });

            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var hrUser = await context.Users.FindAsync(hrId);
        Assert.IsNotNull(hrUser);

        var emailService = new EmailService(Options.Create(new SmtpOptions
        {
            Host = "localhost",
            Port = 25,
            User = "noreply@example.com",
            Pass = "password",
            Name = "TrackHive"
        }));

        var controller = new HrDashboardController(context, emailService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        var actionContext = new ActionContext(controller.ControllerContext.HttpContext!, new RouteData(), new ActionDescriptor());
        controller.Url = new UrlHelper(actionContext);

        var method = typeof(HrDashboardController)
            .GetMethod("BuildDashboardViewModelAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<HrDashboardViewModel>)method!.Invoke(controller, new object?[] { hrUser!, null })!;
        var viewModel = await task;

        Assert.AreEqual(2, viewModel.PendingLeaveRequests.Count);
    }

    [TestMethod]
    public async Task PayrollHistory_FreePlanExcludesStaleRecords()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        var now = DateTimeOffset.UtcNow;

        int employeeId;

        await using (var seedContext = await CreateInitializedContextAsync(options))
        {
            var org = new Organization { Name = "Free Org", CreatedByEmail = "hr@free.com", Plan = OrganizationPlan.Free };
            seedContext.Organizations.Add(org);
            await seedContext.SaveChangesAsync();

            var employee = new AppUser
            {
                Name = "Employee",
                Email = "employee@free.com",
                PasswordHash = "hash",
                Role = RoleType.Employee,
                OrganizationId = org.Id,
                ThemePreference = "light",
                LanguagePreference = "en",
                MonthlySalary = 1000m
            };

            seedContext.Users.Add(employee);
            await seedContext.SaveChangesAsync();

            employeeId = employee.Id;

            seedContext.PayrollRecords.AddRange(
                new PayrollRecord
                {
                    UserId = employeeId,
                    Year = now.Year,
                    Month = now.Month,
                    MonthlySalary = 1000m,
                    StandardHours = 160m,
                    WorkedHours = 160m,
                    AutoOvertimeHours = 0m,
                    AdditionalOvertimeHours = 0m,
                    TotalOvertimeHours = 0m,
                    HourlyRate = 12.5m,
                    OvertimeMultiplier = 1.5m,
                    AttendancePay = 1000m,
                    OvertimePay = 0m,
                    Deductions = 0m,
                    GrossPay = 1000m,
                    NetPay = 1000m,
                    WorkingDays = 20,
                    PresentDays = 20,
                    CalculatedAt = now.AddDays(-15)
                },
                new PayrollRecord
                {
                    UserId = employeeId,
                    Year = now.AddMonths(-4).Year,
                    Month = now.AddMonths(-4).Month,
                    MonthlySalary = 1000m,
                    StandardHours = 160m,
                    WorkedHours = 160m,
                    AutoOvertimeHours = 0m,
                    AdditionalOvertimeHours = 0m,
                    TotalOvertimeHours = 0m,
                    HourlyRate = 12.5m,
                    OvertimeMultiplier = 1.5m,
                    AttendancePay = 1000m,
                    OvertimePay = 0m,
                    Deductions = 0m,
                    GrossPay = 1000m,
                    NetPay = 1000m,
                    WorkingDays = 20,
                    PresentDays = 20,
                    CalculatedAt = now.AddDays(-150)
                });

            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);

        var controller = new PayrollController(context, new PayrollPdfGenerator());
        var method = typeof(PayrollController)
            .GetMethod("LoadHistoryAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<System.Collections.Generic.List<PastPayrollRecordViewModel>>)method!
            .Invoke(controller, new object?[] { employeeId, OrganizationPlan.Free })!;
        var history = await task;

        Assert.AreEqual(1, history.Count);
        Assert.AreEqual(now.Month, history[0].Month);
        Assert.AreEqual(now.Year, history[0].Year);
    }

    [TestMethod]
    public async Task PayrollHistory_PaidPlanKeepsAllRecords()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        var now = DateTimeOffset.UtcNow;

        int employeeId;

        await using (var seedContext = await CreateInitializedContextAsync(options))
        {
            var org = new Organization { Name = "Pro Org", CreatedByEmail = "hr@pro.com", Plan = OrganizationPlan.Pro };
            seedContext.Organizations.Add(org);
            await seedContext.SaveChangesAsync();

            var employee = new AppUser
            {
                Name = "Employee",
                Email = "employee@pro.com",
                PasswordHash = "hash",
                Role = RoleType.Employee,
                OrganizationId = org.Id,
                ThemePreference = "light",
                LanguagePreference = "en",
                MonthlySalary = 1000m
            };

            seedContext.Users.Add(employee);
            await seedContext.SaveChangesAsync();

            employeeId = employee.Id;

            seedContext.PayrollRecords.AddRange(
                new PayrollRecord
                {
                    UserId = employeeId,
                    Year = now.Year,
                    Month = now.Month,
                    MonthlySalary = 1000m,
                    StandardHours = 160m,
                    WorkedHours = 160m,
                    AutoOvertimeHours = 0m,
                    AdditionalOvertimeHours = 0m,
                    TotalOvertimeHours = 0m,
                    HourlyRate = 12.5m,
                    OvertimeMultiplier = 1.5m,
                    AttendancePay = 1000m,
                    OvertimePay = 0m,
                    Deductions = 0m,
                    GrossPay = 1000m,
                    NetPay = 1000m,
                    WorkingDays = 20,
                    PresentDays = 20,
                    CalculatedAt = now.AddDays(-15)
                },
                new PayrollRecord
                {
                    UserId = employeeId,
                    Year = now.AddMonths(-5).Year,
                    Month = now.AddMonths(-5).Month,
                    MonthlySalary = 1000m,
                    StandardHours = 160m,
                    WorkedHours = 160m,
                    AutoOvertimeHours = 0m,
                    AdditionalOvertimeHours = 0m,
                    TotalOvertimeHours = 0m,
                    HourlyRate = 12.5m,
                    OvertimeMultiplier = 1.5m,
                    AttendancePay = 1000m,
                    OvertimePay = 0m,
                    Deductions = 0m,
                    GrossPay = 1000m,
                    NetPay = 1000m,
                    WorkingDays = 20,
                    PresentDays = 20,
                    CalculatedAt = now.AddDays(-160)
                });

            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);

        var controller = new PayrollController(context, new PayrollPdfGenerator());
        var method = typeof(PayrollController)
            .GetMethod("LoadHistoryAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<System.Collections.Generic.List<PastPayrollRecordViewModel>>)method!
            .Invoke(controller, new object?[] { employeeId, OrganizationPlan.Pro })!;
        var history = await task;

        Assert.AreEqual(2, history.Count);
    }
}
